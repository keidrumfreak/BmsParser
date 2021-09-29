using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BmsParser
{
    class BmsonDecoder : ChartDecoder
    {
        new public BmsModel Decode(string path)
        {
            return Decode(path, File.ReadAllBytes(path));
        }

        public override BmsModel Decode(ChartInformation info)
        {
            LNType = info.LNType;
            return Decode(info.Path);
        }

        public BmsModel Decode(string path, byte[] bin)
        {
            string input;
            using (var mem = new MemoryStream(bin))
            using (var reader = new StreamReader(mem))
                input = new StreamReader(new MemoryStream(bin)).ReadToEnd();
            var bmson = JsonSerializer.Deserialize<Bmson>(input);
            var model = new BmsModel();
            var sha256 = SHA256.Create();
            var arr = sha256.ComputeHash(bin);
            model.Sha256 = BitConverter.ToString(arr).ToLower().Replace("-", "");
            model.Title = bmson.Info.Title;
            model.Subtitle = (bmson.Info.Subtitle != null ? bmson.Info.Subtitle : "")
                    + (bmson.Info.Subtitle != null && bmson.Info.Subtitle.Length > 0 && bmson.Info.ChartName != null
                            && bmson.Info.ChartName.Length > 0 ? " " : "")
                    + (bmson.Info.ChartName != null && bmson.Info.ChartName.Length > 0
                            ? "[" + bmson.Info.ChartName + "]" : "");
            model.Artist = bmson.Info.Artist;
            model.SubArtist = string.Join(",", bmson.Info.SubArtists);
            model.Genre = bmson.Info.Genre;

            if (bmson.Info.JudgeRank < 0)
            {
                logs.Add(new DecodeLog(State.Warning, "judge_rankが0以下です。judge_rank = " + bmson.Info.JudgeRank));
            }
            else if (bmson.Info.JudgeRank < 5)
            {
                model.JudgeRank = (int)bmson.Info.JudgeRank;
                logs.Add(new DecodeLog(State.Warning, "judge_rankの定義が仕様通りでない可能性があります。judge_rank = " + bmson.Info.JudgeRank));
                model.JudgeRankType = JudgeRankType.BmsRank;
            }
            else
            {
                model.JudgeRank = (int)bmson.Info.JudgeRank;
                model.JudgeRankType = JudgeRankType.BmsonJudgeRank;
            }

            if (bmson.Info.Total > 0)
            {
                model.Total = bmson.Info.Total;
                model.TotalType = TotalType.Bmson;
            }
            else
            {
                logs.Add(new DecodeLog(State.Warning, "totalが0以下です。total = " + bmson.Info.Total));
            }

            model.Bpm = bmson.Info.InitBpm;
            model.PlayLevel = bmson.Info.Level.ToString();
            model.Mode = Mode.GetMode(bmson.Info.ModeHint) ?? Mode.Beat7K;

            if (bmson.Info.LNType > 0 && bmson.Info.LNType <= 3)
            {
                model.LNMode = (LNMode)bmson.Info.LNType;
            }
            int[] keyassign;
            if (model.Mode == Mode.Beat5K)
            {
                keyassign = new int[] { 0, 1, 2, 3, 4, -1, -1, 5 };
            }
            else if (model.Mode == Mode.Beat10K)
            {
                keyassign = new int[] { 0, 1, 2, 3, 4, -1, -1, 5, 6, 7, 8, 9, 10, -1, -1, 11 };
            }
            else
            {
                keyassign = new int[model.Mode.Key];
                for (int i = 0; i < keyassign.Length; i++)
                {
                    keyassign[i] = i;
                }
            }
            List<LongNote>[] lnlist = new List<LongNote>[model.Mode.Key];
            Dictionary<BmsonNote, LongNote> lnup = new();

            model.Banner = bmson.Info.BannerImage;
            model.BackBmp = bmson.Info.BackImage;
            model.StageFile = bmson.Info.EyecatchImage;
            model.Preview = bmson.Info.PreviewMusic;
            TimeLine basetl = new TimeLine(0, 0, model.Mode.Key);
            basetl.Bpm = model.Bpm;
            var tlcache = new SortedDictionary<int, TimeLineCache>();
            tlcache.Put(0, new TimeLineCache(0.0, basetl));

            if (bmson.BpmEvents == null)
            {
                bmson.BpmEvents = new BpmEvent[0];
            }
            if (bmson.StopEvents == null)
            {
                bmson.StopEvents = new StopEvent[0];
            }
            if (bmson.ScrollEvents == null)
            {
                bmson.ScrollEvents = new ScrollEvent[0];
            }

            var resolution = bmson.Info.Resolution > 0 ? bmson.Info.Resolution * 4 : 960;

            int bpmpos = 0;
            int stoppos = 0;
            int scrollpos = 0;
            // bpmNotes, stopNotes処理
            bmson.BpmEvents = bmson.BpmEvents.OrderBy(e => e.Y).ToArray();
            bmson.StopEvents = bmson.StopEvents.OrderBy(e => e.Y).ToArray();
            bmson.ScrollEvents = bmson.ScrollEvents.OrderBy(e => e.Y).ToArray();

            while (bpmpos < bmson.BpmEvents.Length || stoppos < bmson.StopEvents.Length || scrollpos < bmson.ScrollEvents.Length)
            {
                var bpmy = bpmpos < bmson.BpmEvents.Length ? bmson.BpmEvents[bpmpos].Y : int.MaxValue;
                var stopy = stoppos < bmson.StopEvents.Length ? bmson.StopEvents[stoppos].Y : int.MaxValue;
                var scrolly = scrollpos < bmson.ScrollEvents.Length ? bmson.ScrollEvents[scrollpos].Y : int.MaxValue;
                if (scrolly <= stopy && scrolly <= bpmy)
                {
                    getTimeLine(scrolly, resolution, tlcache, model).Scroll = (bmson.ScrollEvents[scrollpos].rate);
                    scrollpos++;
                }
                else if (bpmy <= stopy)
                {
                    getTimeLine(bpmy, resolution, tlcache, model).Bpm = (bmson.BpmEvents[bpmpos].Bpm);
                    bpmpos++;
                }
                else if (stopy != int.MaxValue)
                {
                    var tl6 = getTimeLine(stopy, resolution, tlcache, model);
                    tl6.StopMicrosecond = ((long)((1000.0 * 1000 * 60 * 4 * bmson.StopEvents[stoppos].Duration)
                            / (tl6.Bpm * resolution)));
                    stoppos++;
                }
            }
            // lines処理(小節線)
            if (bmson.Lines != null)
            {
                foreach (var bl in bmson.Lines)
                {
                    getTimeLine(bl.Y, resolution, tlcache, model).IsSectionLine = true;
                }
            }

            var wavmap = new string[bmson.SoundChannels.Length + bmson.KeyChannels.Length + bmson.MineChannels.Length];
            int id = 0;
            long starttime = 0;
            foreach (var sc in bmson.SoundChannels)
            {
                wavmap[id] = sc.Name;
                sc.Notes = sc.Notes.OrderBy(n => n.Y).ToArray();
                var length = sc.Notes.Length;
                for (int i = 0; i < length; i++)
                {
                    var n = sc.Notes[i];
                    BmsonNote next = null;
                    for (int j = i + 1; j < length; j++)
                    {
                        if (sc.Notes[j].Y > n.Y)
                        {
                            next = sc.Notes[j];
                            break;
                        }
                    }
                    long duration = 0;
                    if (!n.C)
                    {
                        starttime = 0;
                    }
                    var tl = getTimeLine(n.Y, resolution, tlcache, model);
                    if (next != null && next.C)
                    {
                        duration = getTimeLine(next.Y, resolution, tlcache, model).TimeMicrosecond - tl.TimeMicrosecond;
                    }

                    var key = n.X > 0 && n.X <= keyassign.Length ? keyassign[n.X - 1] : -1;
                    if (key < 0)
                    {
                        // BGノート
                        tl.AddBackGroundNote(new NormalNote(id, starttime, duration));
                    }
                    else if (n.Up)
                    {
                        // LN終端音定義
                        var assigned = false;
                        if (lnlist[key] != null)
                        {
                            var section = (n.Y / resolution);
                            foreach (var ln in lnlist[key])
                            {
                                if (section == ln.Pair.Section)
                                {
                                    ln.Pair.Wav = id;
                                    ln.Pair.StartTimeMicrosecond = starttime;
                                    ln.Pair.DurationMicrosecond = duration;
                                    assigned = true;
                                    break;
                                }
                            }
                            if (!assigned)
                            {
                                lnup.Put(n, new LongNote(id, starttime, duration));
                            }
                        }
                    }
                    else
                    {
                        var insideln = false;
                        if (lnlist[key] != null)
                        {
                            var section = (n.Y / resolution);
                            foreach (var ln in lnlist[key])
                            {
                                if (ln.Section < section && section <= ln.Pair.Section)
                                {
                                    insideln = true;
                                    break;
                                }
                            }
                        }

                        if (insideln)
                        {
                            logs.Add(new DecodeLog(State.Warning,
                                    "LN内にノートを定義しています - x :  " + n.X + " y : " + n.Y));
                            tl.AddBackGroundNote(new NormalNote(id, starttime, duration));
                        }
                        else
                        {
                            if (n.L > 0)
                            {
                                // ロングノート
                                TimeLine end = getTimeLine(n.Y + n.L, resolution, tlcache, model);
                                LongNote ln = new LongNote(id, starttime, duration);
                                if (tl.GetNote(key) != null)
                                {
                                    // レイヤーノート判定
                                    var en = tl.GetNote(key);
                                    if (en is LongNote && end.GetNote(key) == ((LongNote)en).Pair)
                                    {
                                        en.AddLayeredNote(ln);
                                    }
                                    else
                                    {
                                        logs.Add(new DecodeLog(State.Warning,
                                            "同一の位置にノートが複数定義されています - x :  " + n.X + " y : " + n.Y));
                                    }
                                }
                                else
                                {
                                    var existNote = false;
                                    foreach (var tl3 in tlcache.Where(k => n.Y < k.Key && k.Key <= (n.Y + n.L)).Select(t => t.Value))
                                    {
                                        if (tl3.TimeLine.ExistNote(key))
                                        {
                                            existNote = true;
                                            break;
                                        }
                                    }
                                    if (existNote)
                                    {
                                        logs.Add(new DecodeLog(State.Warning,
                                                "LN内にノートを定義しています - x :  " + n.X + " y : " + n.Y));
                                        tl.AddBackGroundNote(new NormalNote(id, starttime, duration));
                                    }
                                    else
                                    {
                                        tl.SetNote(key, ln);
                                        // ln.setDuration(end.getTime() -
                                        // start.getTime());
                                        LongNote lnend = null;
                                        foreach (var up in lnup)
                                        {
                                            if (up.Key.Y == n.Y + n.L && up.Key.X == n.X)
                                            {
                                                lnend = up.Value;
                                                break;
                                            }
                                        }
                                        if (lnend == null)
                                        {
                                            lnend = new LongNote(-2);
                                        }

                                        end.SetNote(key, lnend);
                                        ln.Type = (n.T > 0 && n.T <= 3 ? (LNMode)n.T : model.LNMode);
                                        ln.Pair = (lnend);
                                        if (lnlist[key] == null)
                                        {
                                            lnlist[key] = new List<LongNote>();
                                        }
                                        lnlist[key].Add(ln);
                                    }
                                }
                            }
                            else
                            {
                                // 通常ノート
                                if (tl.ExistNote(key))
                                {
                                    if (tl.GetNote(key) is NormalNote)
                                    {
                                        tl.GetNote(key).AddLayeredNote(new NormalNote(id, starttime, duration));
                                    }
                                    else
                                    {
                                        logs.Add(new DecodeLog(State.Warning,
                                                "同一の位置にノートが複数定義されています - x :  " + n.X + " y : " + n.Y));
                                    }
                                }
                                else
                                {
                                    tl.SetNote(key, new NormalNote(id, starttime, duration));
                                }
                            }
                        }
                    }
                    starttime += duration;
                }
                id++;
            }

            foreach (var sc in bmson.KeyChannels)
            {
                wavmap[id] = sc.Name;
                sc.Notes = sc.Notes.OrderBy(n => n.Y).ToArray();
                var length = sc.Notes.Length;
                for (int i = 0; i < length; i++)
                {
                    var n = sc.Notes[i];
                    TimeLine tl4 = getTimeLine(n.Y, resolution, tlcache, model);

                    var key = n.X > 0 && n.X <= keyassign.Length ? keyassign[n.X - 1] : -1;
                    if (key >= 0)
                    {
                        // BGノート
                        tl4.SetHiddenNote(key, new NormalNote(id));
                    }
                }
                id++;
            }
            foreach (var sc in bmson.MineChannels)
            {
                wavmap[id] = sc.Name;
                sc.Notes = sc.Notes.OrderBy(n => n.Y).ToArray();
                var length = sc.Notes.Length;
                for (int i = 0; i < length; i++)
                {
                    var n = sc.Notes[i];
                    var tl3 = getTimeLine(n.Y, resolution, tlcache, model);

                    var key = n.X > 0 && n.X <= keyassign.Length ? keyassign[n.X - 1] : -1;
                    if (key >= 0)
                    {
                        var insideln = false;
                        if (lnlist[key] != null)
                        {
                            var section = (n.Y / resolution);
                            foreach (var ln in lnlist[key])
                            {
                                if (ln.Section < section && section <= ln.Pair.Section)
                                {
                                    insideln = true;
                                    break;
                                }
                            }
                        }

                        if (insideln)
                        {
                            logs.Add(new DecodeLog(State.Warning,
                                    "LN内に地雷ノートを定義しています - x :  " + n.X + " y : " + n.Y));
                        }
                        else if (tl3.ExistNote(key))
                        {
                            logs.Add(new DecodeLog(State.Warning,
                                    "地雷ノートを定義している位置に通常ノートが存在します - x :  " + n.X + " y : " + n.Y));
                        }
                        else
                        {
                            tl3.SetNote(key, new MineNote(id, n.Damage));
                        }
                    }
                }
                id++;
            }

            model.WavList = wavmap;
            // BGA処理
            if (bmson.Bga != null && bmson.Bga.BgaHeader != null)
            {
                var bgamap = new String[bmson.Bga.BgaHeader.Length];
                var idmap = new Dictionary<int, int>(bmson.Bga.BgaHeader.Length);
                var seqmap = new Dictionary<int, Sequence[]>();
                for (int i = 0; i < bmson.Bga.BgaHeader.Length; i++)
                {
                    var bh = bmson.Bga.BgaHeader[i];
                    bgamap[i] = bh.Name;
                    idmap.Put(bh.ID, i);
                }
                if (bmson.Bga.BgaSequence != null)
                {
                    foreach (var n in bmson.Bga.BgaSequence)
                    {
                        if (n != null)
                        {
                            var sequence = new Sequence[n.Sequence.Length];
                            for (int i = 0; i < sequence.Length; i++)
                            {
                                var seq = n.Sequence[i];
                                if (seq.ID.HasValue)
                                {
                                    sequence[i] = new Sequence(seq.Time, seq.ID.Value);
                                }
                                else
                                {
                                    sequence[i] = new Sequence(seq.Time);
                                }
                            }
                            seqmap.Put(n.ID, sequence);
                        }
                    }
                }
                if (bmson.Bga.BgaEvents != null)
                {
                    foreach (var n in bmson.Bga.BgaEvents)
                    {
                        getTimeLine(n.Y, resolution, tlcache, model).BgaID = (idmap[n.ID]);
                    }
                }
                if (bmson.Bga.LayerEvents != null)
                {
                    foreach (var n in bmson.Bga.LayerEvents)
                    {
                        int[] idset = n.IDSet != null ? n.IDSet : new int[] { n.ID };
                        var seqs = new Sequence[idset.Length][];
                        Event @event = null;
                        switch (n.Condition != null ? n.Condition : "")
                        {
                            case "play":
                                @event = new Event(EventType.Play, n.Interval);
                                break;
                            case "miss":
                                @event = new Event(EventType.Miss, n.Interval);
                                break;
                            default:
                                @event = new Event(EventType.Always, n.Interval);
                                break;
                        }
                        for (int seqindex = 0; seqindex < seqs.Length; seqindex++)
                        {
                            int nid = idset[seqindex];
                            if (seqmap.ContainsKey(nid))
                            {
                                seqs[seqindex] = seqmap[nid];
                            }
                            else
                            {
                                seqs[seqindex] = new Sequence[] { new Sequence(0, idmap[n.ID]), new Sequence(500) };
                            }
                        }
                        getTimeLine(n.Y, resolution, tlcache, model).EventLayer = new Layer[] { new Layer(@event, seqs) };
                    }
                }
                if (bmson.Bga.PoorEvents != null)
                {
                    foreach (var n in bmson.Bga.PoorEvents)
                    {
                        if (seqmap.ContainsKey(n.ID))
                        {
                            getTimeLine(n.Y, resolution, tlcache, model).EventLayer = (new Layer[] {new Layer(new Event(EventType.Miss, 1),
                                new Sequence[][] {seqmap[n.ID]})});
                        }
                        else
                        {
                            getTimeLine(n.Y, resolution, tlcache, model).EventLayer = (new Layer[] {new Layer(new Event(EventType.Miss, 1),
                                new Sequence[][] { new[] {new Sequence(0, idmap[n.ID]),new Sequence(500)}})});
                        }
                    }
                }
                model.BgaList = bgamap;
            }
            TimeLine[] tl2 = new TimeLine[tlcache.Count];
            int tlcount = 0;
            foreach (var tlc in tlcache.Values)
            {
                tl2[tlcount] = tlc.TimeLine;
                tlcount++;
            }
            model.TimeLines = tl2;

            model.ChartInformation = new ChartInformation(path, LNType, null);
            return model;
        }

        private TimeLine getTimeLine(int y, double resolution, SortedDictionary<int, TimeLineCache> tlcache, BmsModel model)
        {
            // Timeをus単位にする場合はこのメソッド内部だけ変更すればOK
            if (tlcache.TryGetValue(y, out var tlc))
            {
                return tlc.TimeLine;
            }

            var le = tlcache.OrderByDescending(t => t.Key).FirstOrDefault(t => t.Key < y);
            double bpm = le.Value.TimeLine.Bpm;
            double time = le.Value.Time + le.Value.TimeLine.StopMicrosecond
                    + (240000.0 * 1000 * ((y - le.Key) / resolution)) / bpm;

            TimeLine tl = new TimeLine(y / resolution, (long)time, model.Mode.Key);
            tl.Bpm = bpm;
            tlcache.Put(y, new TimeLineCache(time, tl));
            // System.out.println("y = " + y + " , bpm = " + bpm + " , time = " +
            // tl.getTime());
            return tl;
        }
    }
}
