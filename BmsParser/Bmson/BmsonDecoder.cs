using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using static BmsParser.Layer;

namespace BmsParser.Bmson
{
    class BmsonDecoder : ChartDecoder
    {
        private readonly SortedDictionary<int, TimeLineCache> tlcache = [];

        public BmsonDecoder(LNType lnType = LNType.LongNote)
        {
            LNType = lnType;
        }

        new public BmsModel? Decode(string path)
        {
            return Decode(path, File.ReadAllBytes(path));
        }

        public override BmsModel? Decode(ChartInformation info)
        {
            LNType = info.LNType;
            return Decode(info.Path);
        }

        //protected BmsModel Decode(string f)
        //{
        //    //Logger.getGlobal().fine("BMSファイル解析開始 :" + f);
        //    try
        //    {
        //        var model = this.Decode(f, File.ReadAllBytes(f));
        //        if (model == null)
        //        {
        //            return null;
        //        }
        //        //Logger.getGlobal().fine("BMSファイル解析完了 :" + f.ToString() + " - TimeLine数:" + model.getAllTimes().Length);
        //        return model;
        //    }
        //    catch (IOException e)
        //    {
        //        log.Add(new DecodeLog(State.Error, "BMSファイルが見つかりません"));
        //        //Logger.getGlobal().severe("BMSファイル解析中の例外 : " + e.getClass().getName() + " - " + e.getMessage());
        //    }
        //    return null;
        //}

        public BmsModel? Decode(string path, byte[] bin)
        {
            //Logger.getGlobal().fine("BMSONファイル解析開始 :" + f.ToString());
            log.Clear();
            tlcache.Clear();
            var currnttime = DateTime.Now.Ticks;
            // BMS読み込み、ハッシュ値取得
            var model = new BmsModel(Mode.Beat7K);
            Bmson? bmson = null;
            try
            {
                using var mem = new MemoryStream(bin);
                using var reader = new StreamReader(mem);
                var input = reader.ReadToEnd();
                //MessageDigest digest = MessageDigest.getInstance("SHA-256");
                //bmson = mapper.readValue(new DigestInputStream(new BufferedInputStream(Files.newInputStream(f)), digest),
                //        Bmson.class);
                bmson = JsonSerializer.Deserialize<Bmson>(input);
                model.Sha256 = GetSha256Hash(bin);
                //model.setSHA256(BMSDecoder.convertHexString(digest.digest()));
            }
            catch (IOException)
            {
                //e.printStackTrace();
                return null;
            }
            if (bmson == null || bmson.Info == null)
                return null;
            model.Title = bmson.Info.Title;
            model.Subtitle = (bmson.Info.Subtitle ?? "")
            + (bmson.Info.Subtitle != null && bmson.Info.Subtitle.Length > 0 && bmson.Info.ChartName != null
            && bmson.Info.ChartName.Length > 0 ? " " : "")
                    + (bmson.Info.ChartName != null && bmson.Info.ChartName.Length > 0
                            ? "[" + bmson.Info.ChartName + "]" : "");
            model.Artist = bmson.Info.Artist;
            var subartist = new StringBuilder();
            foreach (var s in bmson.Info.SubArtists)
                subartist.Append((subartist.Length > 0 ? "," : "") + s);
            model.Subartist = subartist.ToString();
            model.Genre = bmson.Info.Genre;

            if (bmson.Info.JudgeRank < 0)
                log.Add(new DecodeLog(State.Warning, "judge_rankが0以下です。judge_rank = " + bmson.Info.JudgeRank));
            else if (bmson.Info.JudgeRank < 5)
            {
                model.JudgeRank = (int)bmson.Info.JudgeRank;
                log.Add(new DecodeLog(State.Warning, "judge_rankの定義が仕様通りでない可能性があります。judge_rank = " + bmson.Info.JudgeRank));
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
                log.Add(new DecodeLog(State.Warning, "totalが0以下です。total = " + bmson.Info.Total));

            model.Bpm = bmson.Info.InitBpm;
            model.PlayLevel = bmson.Info.Level.ToString();
            var mode = Mode.GetMode(bmson.Info.ModeHint);
            if (mode != null)
                model.Mode = mode;
            else
            {
                log.Add(new DecodeLog(State.Warning, "非対応のmode_hintです。mode_hint = " + bmson.Info.ModeHint));
                model.Mode = Mode.Beat7K;
            }
            if (bmson.Info.LNType > 0 && bmson.Info.LNType <= 3)
                model.LNMode = (LNMode)bmson.Info.LNType;
            int[] keyassign;
            if (model.Mode == Mode.Beat5K)
                keyassign = [0, 1, 2, 3, 4, -1, -1, 5];
            else if (model.Mode == Mode.Beat10K)
                keyassign = [0, 1, 2, 3, 4, -1, -1, 5, 6, 7, 8, 9, 10, -1, -1, 11];
            else
            {
                keyassign = new int[model.Mode.Key];
                for (var i = 0; i < keyassign.Length; i++)
                    keyassign[i] = i;
            }
            var lnlist = new List<LongNote>[model.Mode.Key];
            Dictionary<BmsonNote, LongNote> lnup = [];

            model.Banner = bmson.Info.BannerImage;
            model.BackBmp = bmson.Info.BackImage;
            model.StageFile = bmson.Info.EyecatchImage;
            model.Preview = bmson.Info.PreviewMusic;
            var basetl = new Timeline(0, 0, model.Mode.Key)
            {
                Bpm = model.Bpm
            };
            tlcache.Put(0, new TimeLineCache(0.0, basetl));

            bmson.BpmEvents ??= [];
            bmson.StopEvents ??= [];
            bmson.ScrollEvents ??= [];

            double resolution = bmson.Info.Resolution > 0 ? bmson.Info.Resolution * 4 : 960;
            //Comparison<Bmson> comparator = (n1, n2) => (n1.Y - n2.Y);

            var bpmpos = 0;
            var stoppos = 0;
            var scrollpos = 0;
            // bpmNotes, stopNotes処理
            Array.Sort(bmson.BpmEvents, (n1, n2) => n1.Y - n2.Y);
            Array.Sort(bmson.StopEvents, (n1, n2) => n1.Y - n2.Y);
            Array.Sort(bmson.ScrollEvents, (n1, n2) => n1.Y - n2.Y);

            while (bpmpos < bmson.BpmEvents.Length || stoppos < bmson.StopEvents.Length || scrollpos < bmson.ScrollEvents.Length)
            {
                var bpmy = bpmpos < bmson.BpmEvents.Length ? bmson.BpmEvents[bpmpos].Y : int.MaxValue;
                var stopy = stoppos < bmson.StopEvents.Length ? bmson.StopEvents[stoppos].Y : int.MaxValue;
                var scrolly = scrollpos < bmson.ScrollEvents.Length ? bmson.ScrollEvents[scrollpos].Y : int.MaxValue;
                if (scrolly <= stopy && scrolly <= bpmy)
                {
                    getTimeLine(scrolly, resolution).Scroll = bmson.ScrollEvents[scrollpos].Rate;
                    scrollpos++;
                }
                else if (bpmy <= stopy)
                {
                    if (bmson.BpmEvents[bpmpos].Bpm > 0)
                        getTimeLine(bpmy, resolution).Bpm = bmson.BpmEvents[bpmpos].Bpm;
                    else
                        log.Add(new DecodeLog(State.Warning,
                                "negative BPMはサポートされていません - y : " + bmson.BpmEvents[bpmpos].Y + " bpm : " + bmson.BpmEvents[bpmpos].Bpm));
                    bpmpos++;
                }
                else if (stopy != int.MaxValue)
                {
                    if (bmson.StopEvents[stoppos].Duration >= 0)
                    {
                        var tl = getTimeLine(stopy, resolution);
                        tl.MicroStop = (long)(1000.0 * 1000 * 60 * 4 * bmson.StopEvents[stoppos].Duration
                        / (tl.Bpm * resolution));
                    }
                    else
                        log.Add(new DecodeLog(State.Warning,
                                "negative STOPはサポートされていません - y : " + bmson.StopEvents[stoppos].Y + " bpm : " + bmson.StopEvents[stoppos].Duration));
                    stoppos++;
                }
            }
            // lines処理(小節線)
            if (bmson.Lines != null)
                foreach (var bl in bmson.Lines)
                    getTimeLine(bl.Y, resolution).IsSectionLine = true;

            var wavmap = new string[bmson.SoundChannels.Length + bmson.KeyChannels.Length + bmson.MineChannels.Length];
            var id = 0;
            long starttime = 0;
            foreach (var sc in bmson.SoundChannels)
            {
                wavmap[id] = sc.Name;
                Array.Sort(sc.Notes, (n1, n2) => n1.Y - n2.Y);
                var Length = sc.Notes.Length;
                for (var i = 0; i < Length; i++)
                {
                    var n = sc.Notes[i];
                    BmsonNote? next = null;
                    for (var j = i + 1; j < Length; j++)
                        if (sc.Notes[j].Y > n.Y)
                        {
                            next = sc.Notes[j];
                            break;
                        }
                    long duration = 0;
                    if (!n.C)
                        starttime = 0;
                    var tl = getTimeLine(n.Y, resolution);
                    if (next != null && next.C)
                        duration = getTimeLine(next.Y, resolution).MicroTime - tl.MicroTime;

                    var key = n.X > 0 && n.X <= keyassign.Length ? keyassign[n.X - 1] : -1;
                    if (key < 0)
                        // BGノート
                        tl.AddBackGroundNote(new NormalNote(id, starttime, duration));
                    else if (n.Up)
                    {
                        // LN終端音定義
                        var assigned = false;
                        if (lnlist[key] != null)
                        {
                            var section = n.Y / resolution;
                            foreach (var ln in lnlist[key])
                                if (section == ln.Pair?.Section)
                                {
                                    ln.Pair.Wav = id;
                                    ln.Pair.MicroStarttime = starttime;
                                    ln.Pair.MicroDuration = duration;
                                    assigned = true;
                                    break;
                                }
                            if (!assigned)
                                lnup.Put(n, new LongNote(id, starttime, duration));
                        }
                    }
                    else
                    {
                        var insideln = false;
                        if (lnlist[key] != null)
                        {
                            var section = n.Y / resolution;
                            foreach (var ln in lnlist[key])
                                if (ln.Section < section && section <= ln.Pair?.Section)
                                {
                                    insideln = true;
                                    break;
                                }
                        }

                        if (insideln)
                        {
                            log.Add(new DecodeLog(State.Warning,
                                    "LN内にノートを定義しています - x :  " + n.X + " y : " + n.Y));
                            tl.AddBackGroundNote(new NormalNote(id, starttime, duration));
                        }
                        else
                            if (n.L > 0)
                        {
                            // ロングノート
                            var end = getTimeLine(n.Y + n.L, resolution);
                            var ln = new LongNote(id, starttime, duration);
                            if (tl.GetNote(key) != null)
                            {
                                // レイヤーノート判定
                                var en = tl.GetNote(key);
                                if (en is LongNote note && end.GetNote(key) == note.Pair)
                                    en.AddLayeredNote(ln);
                                else
                                    log.Add(new DecodeLog(State.Warning,
                                            "同一の位置にノートが複数定義されています - x :  " + n.X + " y : " + n.Y));
                            }
                            else
                            {
                                var existNote = false;
                                foreach (var tl2 in tlcache.Where(k => n.Y < k.Key && k.Key <= n.Y + n.L).Select(t => t.Value))
                                    if (tl2.Timeline.ExistNote(key))
                                    {
                                        existNote = true;
                                        break;
                                    }
                                if (existNote)
                                {
                                    log.Add(new DecodeLog(State.Warning,
                                            "LN内にノートを定義しています - x :  " + n.X + " y : " + n.Y));
                                    tl.AddBackGroundNote(new NormalNote(id, starttime, duration));
                                }
                                else
                                {
                                    tl.SetNote(key, ln);
                                    // ln.setDuration(end.getTime() -
                                    // start.getTime());
                                    LongNote? lnend = null;
                                    foreach (var up in lnup)
                                        if (up.Key.Y == n.Y + n.L && up.Key.X == n.X)
                                        {
                                            lnend = up.Value;
                                            break;
                                        }
                                    lnend ??= new LongNote(-2);

                                    end.SetNote(key, lnend);
                                    ln.Type = n.T > 0 && n.T <= 3 ? (LNMode)n.T : model.LNMode;
                                    ln.Pair = lnend;
                                    if (lnlist[key] == null)
                                        lnlist[key] = [];
                                    lnlist[key].Add(ln);
                                }
                            }
                        }
                        // 通常ノート
                        else if (tl.ExistNote(key))
                            if (tl.GetNote(key) is NormalNote note)
                                note.AddLayeredNote(new NormalNote(id, starttime, duration));
                            else
                                log.Add(new DecodeLog(State.Warning,
                                        "同一の位置にノートが複数定義されています - x :  " + n.X + " y : " + n.Y));
                        else
                            tl.SetNote(key, new NormalNote(id, starttime, duration));
                    }
                    starttime += duration;
                }
                id++;
            }

            foreach (var sc in bmson.KeyChannels)
            {
                wavmap[id] = sc.Name;
                Array.Sort(sc.Notes, (n1, n2) => n1.Y - n2.Y);
                var Length = sc.Notes.Length;
                for (var i = 0; i < Length; i++)
                {
                    var n = sc.Notes[i];
                    var tl = getTimeLine(n.Y, resolution);

                    var key = n.X > 0 && n.X <= keyassign.Length ? keyassign[n.X - 1] : -1;
                    if (key >= 0)
                        // BGノート
                        tl.SetHiddenNote(key, new NormalNote(id));
                }
                id++;
            }
            foreach (var sc in bmson.MineChannels)
            {
                wavmap[id] = sc.Name;
                Array.Sort(sc.Notes, (n1, n2) => n1.Y - n2.Y);
                var Length = sc.Notes.Length;
                for (var i = 0; i < Length; i++)
                {
                    var n = sc.Notes[i];
                    var tl = getTimeLine(n.Y, resolution);

                    var key = n.X > 0 && n.X <= keyassign.Length ? keyassign[n.X - 1] : -1;
                    if (key >= 0)
                    {
                        var insideln = false;
                        if (lnlist[key] != null)
                        {
                            var section = n.Y / resolution;
                            foreach (var ln in lnlist[key])
                                if (ln.Section < section && section <= ln.Pair?.Section)
                                {
                                    insideln = true;
                                    break;
                                }
                        }

                        if (insideln)
                            log.Add(new DecodeLog(State.Warning,
                                    "LN内に地雷ノートを定義しています - x :  " + n.X + " y : " + n.Y));
                        else if (tl.ExistNote(key))
                            log.Add(new DecodeLog(State.Warning,
                                    "地雷ノートを定義している位置に通常ノートが存在します - x :  " + n.X + " y : " + n.Y));
                        else
                            tl.SetNote(key, new MineNote(id, n.Damage));
                    }
                }
                id++;
            }

            model.WavList = wavmap;
            // BGA処理
            if (bmson.Bga != null && bmson.Bga.BgaHeader != null)
            {
                var bgamap = new string[bmson.Bga.BgaHeader.Length];
                var idmap = new Dictionary<int, int>(bmson.Bga.BgaHeader.Length);
                Dictionary<int, Sequence[]> seqmap = [];
                for (var i = 0; i < bmson.Bga.BgaHeader.Length; i++)
                {
                    var bh = bmson.Bga.BgaHeader[i];
                    bgamap[i] = bh.Name;
                    idmap.Put(bh.ID, i);
                }
                if (bmson.Bga.BgaSequence != null)
                    foreach (var n in bmson.Bga.BgaSequence)
                        if (n != null)
                        {
                            var sequence = new Sequence[n.Sequence.Length];
                            for (var i = 0; i < sequence.Length; i++)
                            {
                                var seq = n.Sequence[i];
                                if (seq.ID.HasValue)
                                    sequence[i] = new Sequence(seq.Time, seq.ID.Value);
                                else
                                    sequence[i] = new Sequence(seq.Time);
                            }
                            seqmap.Put(n.ID, sequence);
                        }
                if (bmson.Bga.BgaEvents != null)
                    foreach (var n in bmson.Bga.BgaEvents)
                        getTimeLine(n.Y, resolution).BgaID = idmap[n.ID];
                if (bmson.Bga.LayerEvents != null)
                    foreach (var n in bmson.Bga.LayerEvents)
                    {
                        var idset = n.IDSet ?? ([n.ID]);
                        var seqs = new Sequence[idset.Length][];
                        var @event = (n.Condition ?? "") switch
                        {
                            "play" => new Event(EventType.Play, n.Interval),
                            "miss" => new Event(EventType.Miss, n.Interval),
                            _ => new Event(EventType.Always, n.Interval),
                        };
                        for (var seqindex = 0; seqindex < seqs.Length; seqindex++)
                        {
                            var nid = idset[seqindex];
                            if (seqmap.TryGetValue(nid, out var value))
                                seqs[seqindex] = value;
                            else
                                seqs[seqindex] = [new(0, idmap[n.ID]), new(500)];
                        }
                        getTimeLine(n.Y, resolution).EventLayer = ([new(@event, seqs)]);
                    }
                if (bmson.Bga.PoorEvents != null)
                    foreach (var n in bmson.Bga.PoorEvents)
                        if (seqmap.TryGetValue(n.ID, out var value))
                            getTimeLine(n.Y, resolution).EventLayer = ([new(new Event(EventType.Miss, 1),
                                [value])]);
                        else
                            getTimeLine(n.Y, resolution).EventLayer = ([new(new Event(EventType.Miss, 1),
                                [[new Sequence(0, idmap[n.ID]),new Sequence(500)]])]);
                model.BgaList = bgamap;
            }
            model.Timelines = tlcache.Values.Select(tlc => tlc.Timeline).ToArray();

            //Logger.getGlobal().fine("BMSONファイル解析完了 :" + f.ToString() + " - TimeLine数:" + tlcache.size() + " 時間(ms):"
            //        + (System.currentTimeMillis() - currnttime));

            model.ChartInformation = new ChartInformation(path, LNType, null);
            return model;

            Timeline getTimeLine(int y, double resolution)
            {
                // Timeをus単位にする場合はこのメソッド内部だけ変更すればOK
                if (tlcache.TryGetValue(y, out var tlc))
                    return tlc.Timeline;

                var le = tlcache.LastOrDefault(c => c.Key < y);
                var bpm = le.Value.Timeline.Bpm;
                var time = le.Value.Time + le.Value.Timeline.MicroStop
                        + 240000.0 * 1000 * ((y - le.Key) / resolution) / bpm;

                var tl = new Timeline(y / resolution, (long)time, model.Mode.Key)
                {
                    Bpm = bpm
                };
                tlcache.Put(y, new TimeLineCache(time, tl));
                // System.out.println("y = " + y + " , bpm = " + bpm + " , time = " +
                // tl.getTime());
                return tl;
            }
        }
    }
}
