using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BmsParser;
using static System.Collections.Specialized.BitVector32;
using static BmsParser.DecodeLog.State;
using static BmsParser.Layer;
using static BmsParser.Mode;

namespace BmsParser
{
    class BmsonDecoder : ChartDecoder
    {
        private BmsModel model;

        private SortedDictionary<int, TimeLineCache> tlcache = new SortedDictionary<int, TimeLineCache>();

        public BmsonDecoder(LNType lnType = LNType.LongNote)
        {
            this.lntype = lnType;
        }

        new public BmsModel Decode(string path)
        {
            return Decode(path, File.ReadAllBytes(path));
        }

        public override BmsModel Decode(ChartInformation info)
        {
            this.lntype = info.LNType;
            return decode(info.Path);
        }

        protected BmsModel decode(string f)
        {
            //Logger.getGlobal().fine("BMSファイル解析開始 :" + f);
            try
            {
                BmsModel model = this.Decode(f, File.ReadAllBytes(f));
                if (model == null)
                {
                    return null;
                }
                //Logger.getGlobal().fine("BMSファイル解析完了 :" + f.ToString() + " - TimeLine数:" + model.getAllTimes().Length);
                return model;
            }
            catch (IOException e)
            {
                log.Add(new DecodeLog(DecodeLog.State.ERROR, "BMSファイルが見つかりません"));
                //Logger.getGlobal().severe("BMSファイル解析中の例外 : " + e.getClass().getName() + " - " + e.getMessage());
            }
            return null;
        }

        public BmsModel Decode(string path, byte[] bin)
        {
            //Logger.getGlobal().fine("BMSONファイル解析開始 :" + f.ToString());
            log.Clear();
            tlcache.Clear();
            long currnttime = DateTime.Now.Ticks;
            // BMS読み込み、ハッシュ値取得
            model = new BmsModel();
            Bmson bmson = null;
            try
            {
                using var mem = new MemoryStream(bin);
                using var reader = new StreamReader(mem);
                var input = reader.ReadToEnd();
                //MessageDigest digest = MessageDigest.getInstance("SHA-256");
                //bmson = mapper.readValue(new DigestInputStream(new BufferedInputStream(Files.newInputStream(f)), digest),
                //        Bmson.class);
                bmson = JsonSerializer.Deserialize<Bmson>(input);
                var sha256 = SHA256.Create();
                var arr = sha256.ComputeHash(bin);
                model.Sha256 = BitConverter.ToString(arr).ToLower().Replace("-", "");
                //model.setSHA256(BMSDecoder.convertHexString(digest.digest()));
            }
            catch (IOException e)
            {
                //e.printStackTrace();
                return null;
            }
            model.Title = bmson.Info.Title;
            model.Subtitle = (bmson.Info.Subtitle != null ? bmson.Info.Subtitle : "")
            + (bmson.Info.Subtitle != null && bmson.Info.Subtitle.Length > 0 && bmson.Info.ChartName != null
            && bmson.Info.ChartName.Length > 0 ? " " : "")
                    + (bmson.Info.ChartName != null && bmson.Info.ChartName.Length > 0
                            ? "[" + bmson.Info.ChartName + "]" : "");
            model.Artist = bmson.Info.Artist;
            StringBuilder subartist = new StringBuilder();
            foreach (String s in bmson.Info.SubArtists)
            {
                subartist.Append((subartist.Length > 0 ? "," : "") + s);
            }
            model.Subartist = subartist.ToString();
            model.Genre = bmson.Info.Genre;

            if (bmson.Info.JudgeRank < 0)
            {
                log.Add(new DecodeLog(WARNING, "judge_rankが0以下です。judge_rank = " + bmson.Info.JudgeRank));
            }
            else if (bmson.Info.JudgeRank < 5)
            {
                model.JudgeRank = (int)bmson.Info.JudgeRank;
                log.Add(new DecodeLog(WARNING, "judge_rankの定義が仕様通りでない可能性があります。judge_rank = " + bmson.Info.JudgeRank));
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
                log.Add(new DecodeLog(WARNING, "totalが0以下です。total = " + bmson.Info.Total));
            }

            model.Bpm = bmson.Info.InitBpm;
            model.PlayLevel = bmson.Info.Level.ToString();
            Mode mode = Mode.GetMode(bmson.Info.ModeHint);
            if (mode != null)
            {
                model.Mode = mode;
            }
            else
            {
                log.Add(new DecodeLog(WARNING, "非対応のmode_hintです。mode_hint = " + bmson.Info.ModeHint));
                model.Mode = Mode.Beat7K;
            }
            if (bmson.Info.LNType > 0 && bmson.Info.LNType <= 3)
            {
                model.LNMode = (LNMode)bmson.Info.LNType;
            }
            int[] keyassign;
            if (model.Mode == Beat5K)
            {
                keyassign = new int[] { 0, 1, 2, 3, 4, -1, -1, 5 };
            }
            else if (model.Mode == Beat10K)
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
            Dictionary<BmsonNote, LongNote> lnup = new Dictionary<BmsonNote, LongNote>();

            model.Banner = bmson.Info.BannerImage;
            model.BackBmp = bmson.Info.BackImage;
            model.StageFile = bmson.Info.EyecatchImage;
            model.Preview = bmson.Info.PreviewMusic;
            TimeLine basetl = new TimeLine(0, 0, model.Mode.Key);
            basetl.setBPM(model.Bpm);
            tlcache.put(0, new TimeLineCache(0.0, basetl));

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

            double resolution = bmson.Info.Resolution > 0 ? bmson.Info.Resolution * 4 : 960;
            //Comparison<Bmson> comparator = (n1, n2) => (n1.Y - n2.Y);

            int bpmpos = 0;
            int stoppos = 0;
            int scrollpos = 0;
            // bpmNotes, stopNotes処理
            Array.Sort(bmson.BpmEvents, (n1, n2) => (n1.Y - n2.Y));
            Array.Sort(bmson.StopEvents, (n1, n2) => (n1.Y - n2.Y));
            Array.Sort(bmson.ScrollEvents, (n1, n2) => (n1.Y - n2.Y));

            while (bpmpos < bmson.BpmEvents.Length || stoppos < bmson.StopEvents.Length || scrollpos < bmson.ScrollEvents.Length)
            {
                int bpmy = bpmpos < bmson.BpmEvents.Length ? bmson.BpmEvents[bpmpos].Y : int.MaxValue;
                int stopy = stoppos < bmson.StopEvents.Length ? bmson.StopEvents[stoppos].Y : int.MaxValue;
                int scrolly = scrollpos < bmson.ScrollEvents.Length ? bmson.ScrollEvents[scrollpos].Y : int.MaxValue;
                if (scrolly <= stopy && scrolly <= bpmy)
                {
                    getTimeLine(scrolly, resolution).setScroll(bmson.ScrollEvents[scrollpos].rate);
                    scrollpos++;
                }
                else if (bpmy <= stopy)
                {
                    if (bmson.BpmEvents[bpmpos].Bpm > 0)
                    {
                        getTimeLine(bpmy, resolution).setBPM(bmson.BpmEvents[bpmpos].Bpm);
                    }
                    else
                    {
                        log.Add(new DecodeLog(WARNING,
                                "negative BPMはサポートされていません - y : " + bmson.BpmEvents[bpmpos].Y + " bpm : " + bmson.BpmEvents[bpmpos].Bpm));
                    }
                    bpmpos++;
                }
                else if (stopy != int.MaxValue)
                {
                    if (bmson.StopEvents[stoppos].Duration >= 0)
                    {
                        TimeLine tl = getTimeLine(stopy, resolution);
                        tl.setStop((long)((1000.0 * 1000 * 60 * 4 * bmson.StopEvents[stoppos].Duration)
                        / (tl.getBPM() * resolution)));
                    }
                    else
                    {
                        log.Add(new DecodeLog(WARNING,
                                "negative STOPはサポートされていません - y : " + bmson.StopEvents[stoppos].Y + " bpm : " + bmson.StopEvents[stoppos].Duration));
                    }
                    stoppos++;
                }
            }
            // lines処理(小節線)
            if (bmson.Lines != null)
            {
                foreach (BarLine bl in bmson.Lines)
                {
                    getTimeLine(bl.Y, resolution).setSectionLine(true);
                }
            }

            String[] wavmap = new String[bmson.SoundChannels.Length + bmson.KeyChannels.Length + bmson.MineChannels.Length];
            int id = 0;
            long starttime = 0;
            foreach (SoundChannel sc in bmson.SoundChannels)
            {
                wavmap[id] = sc.Name;
                Array.Sort(sc.Notes, (n1, n2) => (n1.Y - n2.Y));
                int Length = sc.Notes.Length;
                for (int i = 0; i < Length; i++)
                {
                    var n = sc.Notes[i];
                    BmsonNote next = null;
                    for (int j = i + 1; j < Length; j++)
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
                    TimeLine tl = getTimeLine(n.Y, resolution);
                    if (next != null && next.C)
                    {
                        duration = getTimeLine(next.Y, resolution).getMicroTime() - tl.getMicroTime();
                    }

                    int key = n.X > 0 && n.X <= keyassign.Length ? keyassign[n.X - 1] : -1;
                    if (key < 0)
                    {
                        // BGノート
                        tl.addBackGroundNote(new NormalNote(id, starttime, duration));
                    }
                    else if (n.Up)
                    {
                        // LN終端音定義
                        bool assigned = false;
                        if (lnlist[key] != null)
                        {
                            double section = (n.Y / resolution);
                            foreach (LongNote ln in lnlist[key])
                            {
                                if (section == ln.Pair.getSection())
                                {
                                    ln.Pair.setWav(id);
                                    ln.Pair.setMicroStarttime(starttime);
                                    ln.Pair.setMicroDuration(duration);
                                    assigned = true;
                                    break;
                                }
                            }
                            if (!assigned)
                            {
                                lnup.put(n, new LongNote(id, starttime, duration));
                            }
                        }
                    }
                    else
                    {
                        bool insideln = false;
                        if (lnlist[key] != null)
                        {
                            double section = (n.Y / resolution);
                            foreach (LongNote ln in lnlist[key])
                            {
                                if (ln.getSection() < section && section <= ln.Pair.getSection())
                                {
                                    insideln = true;
                                    break;
                                }
                            }
                        }

                        if (insideln)
                        {
                            log.Add(new DecodeLog(WARNING,
                                    "LN内にノートを定義しています - x :  " + n.X + " y : " + n.Y));
                            tl.addBackGroundNote(new NormalNote(id, starttime, duration));
                        }
                        else
                        {
                            if (n.L > 0)
                            {
                                // ロングノート
                                TimeLine end = getTimeLine(n.Y + n.L, resolution);
                                LongNote ln = new LongNote(id, starttime, duration);
                                if (tl.getNote(key) != null)
                                {
                                    // レイヤーノート判定
                                    var en = tl.getNote(key);
                                    if (en is LongNote && end.getNote(key) == ((LongNote)en).Pair)
                                    {
                                        en.addLayeredNote(ln);
                                    }
                                    else
                                    {
                                        log.Add(new DecodeLog(WARNING,
                                                "同一の位置にノートが複数定義されています - x :  " + n.X + " y : " + n.Y));
                                    }
                                }
                                else
                                {
                                    bool existNote = false;
                                    foreach (TimeLineCache tl2 in tlcache.Where(k => n.Y < k.Key && k.Key <= (n.Y + n.L)).Select(t => t.Value))
                                    {
                                        if (tl2.timeline.existNote(key))
                                        {
                                            existNote = true;
                                            break;
                                        }
                                    }
                                    if (existNote)
                                    {
                                        log.Add(new DecodeLog(WARNING,
                                                "LN内にノートを定義しています - x :  " + n.X + " y : " + n.Y));
                                        tl.addBackGroundNote(new NormalNote(id, starttime, duration));
                                    }
                                    else
                                    {
                                        tl.setNote(key, ln);
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

                                        end.setNote(key, lnend);
                                        ln.Type = n.T > 0 && n.T <= 3 ? (LNMode)n.T : model.LNMode;
                                        ln.Pair = lnend;
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
                                if (tl.existNote(key))
                                {
                                    if (tl.getNote(key) is NormalNote)
                                    {
                                        tl.getNote(key).addLayeredNote(new NormalNote(id, starttime, duration));
                                    }
                                    else
                                    {
                                        log.Add(new DecodeLog(WARNING,
                                                "同一の位置にノートが複数定義されています - x :  " + n.X + " y : " + n.Y));
                                    }
                                }
                                else
                                {
                                    tl.setNote(key, new NormalNote(id, starttime, duration));
                                }
                            }
                        }
                    }
                    starttime += duration;
                }
                id++;
            }

            foreach (MineChannel sc in bmson.KeyChannels)
            {
                wavmap[id] = sc.Name;
                Array.Sort(sc.Notes, (n1, n2) => (n1.Y - n2.Y));
                int Length = sc.Notes.Length;
                for (int i = 0; i < Length; i++)
                {
                    var n = sc.Notes[i];
                    TimeLine tl = getTimeLine(n.Y, resolution);

                    int key = n.X > 0 && n.X <= keyassign.Length ? keyassign[n.X - 1] : -1;
                    if (key >= 0)
                    {
                        // BGノート
                        tl.setHiddenNote(key, new NormalNote(id));
                    }
                }
                id++;
            }
            foreach (MineChannel sc in bmson.MineChannels)
            {
                wavmap[id] = sc.Name;
                Array.Sort(sc.Notes, (n1, n2) => (n1.Y - n2.Y));
                int Length = sc.Notes.Length;
                for (int i = 0; i < Length; i++)
                {
                    var n = sc.Notes[i];
                    TimeLine tl = getTimeLine(n.Y, resolution);

                    int key = n.X > 0 && n.X <= keyassign.Length ? keyassign[n.X - 1] : -1;
                    if (key >= 0)
                    {
                        bool insideln = false;
                        if (lnlist[key] != null)
                        {
                            double section = (n.Y / resolution);
                            foreach (LongNote ln in lnlist[key])
                            {
                                if (ln.getSection() < section && section <= ln.Pair.getSection())
                                {
                                    insideln = true;
                                    break;
                                }
                            }
                        }

                        if (insideln)
                        {
                            log.Add(new DecodeLog(WARNING,
                                    "LN内に地雷ノートを定義しています - x :  " + n.X + " y : " + n.Y));
                        }
                        else if (tl.existNote(key))
                        {
                            log.Add(new DecodeLog(WARNING,
                                    "地雷ノートを定義している位置に通常ノートが存在します - x :  " + n.X + " y : " + n.Y));
                        }
                        else
                        {
                            tl.setNote(key, new MineNote(id, n.Damage));
                        }
                    }
                }
                id++;
            }

            model.WavList = wavmap;
            // BGA処理
            if (bmson.Bga != null && bmson.Bga.BgaHeader != null)
            {
                String[] bgamap = new String[bmson.Bga.BgaHeader.Length];
                Dictionary<int, int> idmap = new Dictionary<int, int>(bmson.Bga.BgaHeader.Length);
                Dictionary<int, Layer.Sequence[]> seqmap = new Dictionary<int, Layer.Sequence[]>();
                for (int i = 0; i < bmson.Bga.BgaHeader.Length; i++)
                {
                    var bh = bmson.Bga.BgaHeader[i];
                    bgamap[i] = bh.Name;
                    idmap.put(bh.ID, i);
                }
                if (bmson.Bga.BgaSequence != null)
                {
                    foreach (var n in bmson.Bga.BgaSequence)
                    {
                        if (n != null)
                        {
                            Layer.Sequence[] sequence = new Layer.Sequence[n.Sequence.Length];
                            for (int i = 0; i < sequence.Length; i++)
                            {
                                var seq = n.Sequence[i];
                                if (seq.ID != int.MinValue)
                                {
                                    sequence[i] = new Layer.Sequence(seq.Time, seq.ID.Value);
                                }
                                else
                                {
                                    sequence[i] = new Layer.Sequence(seq.Time);
                                }
                            }
                            seqmap.put(n.ID, sequence);
                        }
                    }
                }
                if (bmson.Bga.BgaEvents != null)
                {
                    foreach (var n in bmson.Bga.BgaEvents)
                    {
                        getTimeLine(n.Y, resolution).setBGA(idmap[n.ID]);
                    }
                }
                if (bmson.Bga.LayerEvents != null)
                {
                    foreach (var n in bmson.Bga.LayerEvents)
                    {
                        int[] idset = n.IDSet != null ? n.IDSet : new int[] { n.ID };
                        Layer.Sequence[][] seqs = new Layer.Sequence[idset.Length][];
                        Layer.Event @event = null;
                        switch (n.Condition != null ? n.Condition : "")
                        {
                            case "play":
                                @event = new Layer.Event(EventType.PLAY, n.Interval);
                                break;
                            case "miss":
                                @event = new Layer.Event(EventType.MISS, n.Interval);
                                break;
                            default:
                                @event = new Layer.Event(EventType.ALWAYS, n.Interval);
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
                                seqs[seqindex] = new Layer.Sequence[] { new Layer.Sequence(0, idmap[n.ID]), new Layer.Sequence(500) };
                            }
                        }
                        getTimeLine(n.Y, resolution).setEventlayer(new Layer[] { new Layer(@event, seqs) });
                    }
                }
                if (bmson.Bga.PoorEvents != null)
                {
                    foreach (var n in bmson.Bga.PoorEvents)
                    {
                        if (seqmap.ContainsKey(n.ID))
                        {
                            getTimeLine(n.Y, resolution).setEventlayer(new Layer[] {new Layer(new Layer.Event(EventType.MISS, 1),
                                new Layer.Sequence[][] {seqmap[n.ID]})});
                        }
                        else
                        {
                            getTimeLine(n.Y, resolution).setEventlayer(new Layer[] {new Layer(new Layer.Event(EventType.MISS, 1),
                                new Layer.Sequence[][] {[new Layer.Sequence(0, idmap[n.ID]),new Layer.Sequence(500)]})});
                        }
                    }
                }
                model.BgaList = bgamap;
            }
            model.Timelines = tlcache.Values.Select(tlc => tlc.timeline).ToArray();

            //Logger.getGlobal().fine("BMSONファイル解析完了 :" + f.ToString() + " - TimeLine数:" + tlcache.size() + " 時間(ms):"
            //        + (System.currentTimeMillis() - currnttime));

            model.ChartInformation = new ChartInformation(path, lntype, null);
            printLog(path);
            return model;
        }

        private TimeLine getTimeLine(int y, double resolution)
        {
            // Timeをus単位にする場合はこのメソッド内部だけ変更すればOK
            if (tlcache.TryGetValue(y, out var tlc))
                return tlc.timeline;

            var le = tlcache.LastOrDefault(c => c.Key < y);
            double bpm = le.Value.timeline.getBPM();
            double time = le.Value.time + le.Value.timeline.getMicroStop()
                    + (240000.0 * 1000 * ((y - le.Key) / resolution)) / bpm;

            TimeLine tl = new TimeLine(y / resolution, (long)time, model.Mode.Key);
            tl.setBPM(bpm);
            tlcache.put(y, new TimeLineCache(time, tl));
            // System.out.println("y = " + y + " , bpm = " + bpm + " , time = " +
            // tl.getTime());
            return tl;
        }
    }
}
