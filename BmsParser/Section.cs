using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using BmsParser;
using static BmsParser.DecodeLog.State;
using static BmsParser.Layer;
using static BmsParser.ChartDecoder;

namespace BmsParser
{
    class Section
    {
        public const int ILLEGAL = -1;
        public const int LANE_AUTOPLAY = 1;
        public const int SECTION_RATE = 2;
        public const int BPM_CHANGE = 3;
        public const int BGA_PLAY = 4;
        public const int POOR_PLAY = 6;
        public const int LAYER_PLAY = 7;
        public const int BPM_CHANGE_EXTEND = 8;
        public const int STOP = 9;

        public const int P1_KEY_BASE = 1 * 36 + 1;
        public const int P2_KEY_BASE = 2 * 36 + 1;
        public const int P1_INVISIBLE_KEY_BASE = 3 * 36 + 1;
        public const int P2_INVISIBLE_KEY_BASE = 4 * 36 + 1;
        public const int P1_LONG_KEY_BASE = 5 * 36 + 1;
        public const int P2_LONG_KEY_BASE = 6 * 36 + 1;
        public const int P1_MINE_KEY_BASE = 13 * 36 + 1;
        public const int P2_MINE_KEY_BASE = 14 * 36 + 1;

        public const int SCROLL = 1020;

        public static readonly int[] NOTE_CHANNELS = {P1_KEY_BASE, P2_KEY_BASE ,P1_INVISIBLE_KEY_BASE, P2_INVISIBLE_KEY_BASE,
            P1_LONG_KEY_BASE, P2_LONG_KEY_BASE, P1_MINE_KEY_BASE, P2_MINE_KEY_BASE};

        /**
         * 小節の拡大倍率
         */
        private double rate = 1.0;
        /**
         * POORアニメーション
         */
        private int[] poor = new int[0];

        private BmsModel model;

        private double sectionnum;

        private List<DecodeLog> log;

        private List<String> channellines;

        public Section(BmsModel model, Section prev, List<String> lines, Dictionary<int, Double> bpmtable,
                Dictionary<int, Double> stoptable, Dictionary<int, Double> scrolltable, List<DecodeLog> log)
        {
            this.model = model;
            this.log = log;
            int @base = model.getBase();

            channellines = new List<String>(lines.Count);
            if (prev != null)
            {
                sectionnum = prev.sectionnum + prev.rate;
            }
            else
            {
                sectionnum = 0;
            }
            foreach (String line in lines)
            {
                int channel = ChartDecoder.parseInt36(line[4], line[5]);
                switch (channel)
                {
                    case ILLEGAL:
                        log.Add(new DecodeLog(WARNING, "チャンネル定義が無効です : " + line));
                        break;
                    // BGレーン
                    case LANE_AUTOPLAY:
                    // BGAレーン
                    case BGA_PLAY:
                    // レイヤー
                    case LAYER_PLAY:
                        channellines.Add(line);
                        break;
                    // 小節の拡大率
                    case SECTION_RATE:
                        int colon_index = line.IndexOf(":");
                        try
                        {
                            rate = Double.Parse(line.Substring(colon_index + 1));
                        }
                        catch (FormatException e)
                        {
                            log.Add(new DecodeLog(WARNING, "小節の拡大率が不正です : " + line));
                        }
                        break;
                    // BPM変化
                    case BPM_CHANGE:
                        this.processData(line, (pos, data) =>
                        {
                            if (@base == 62)
                            {
                                data = ChartDecoder.parseInt36(ChartDecoder.toBase62(data), 0); //間違った数値を再計算、62進数文字に戻して36進数数値化。
                            }
                            bpmchange.put(pos, (double)(data / 36) * 16 + (data % 36));
                        });
                        break;
                    // POORアニメーション
                    case POOR_PLAY:
                        poor = this.splitData(line);
                        // アニメーションが単一画像のみの定義の場合、0を除外する(ミスレイヤーチャンネルの定義が曖昧)
                        int singleid = 0;
                        foreach (int id in poor)
                        {
                            if (id != 0)
                            {
                                if (singleid != 0 && singleid != id)
                                {
                                    singleid = -1;
                                    break;
                                }
                                else
                                {
                                    singleid = id;
                                }
                            }
                        }
                        if (singleid != -1)
                        {
                            poor = new int[] { singleid };
                        }
                        break;
                    // BPM変化(拡張)
                    case BPM_CHANGE_EXTEND:
                        this.processData(line, (pos, data) =>
                        {
                            Double bpm = bpmtable[data];
                            if (bpm != null)
                            {
                                bpmchange.put(pos, bpm);
                            }
                            else
                            {
                                log.Add(new DecodeLog(WARNING, "未定義のBPM変化を参照しています : " + data));
                            }
                        });
                        break;
                    // ストップシーケンス
                    case STOP:
                        this.processData(line, (pos, data) =>
                        {
                            Double st = stoptable[data];
                            if (st != null)
                            {
                                stop.put(pos, st);
                            }
                            else
                            {
                                log.Add(new DecodeLog(WARNING, "未定義のSTOPを参照しています : " + data));
                            }
                        });
                        break;
                    // scroll
                    case SCROLL:
                        this.processData(line, (pos, data) =>
                        {
                            Double st = scrolltable[data];
                            if (st != null)
                            {
                                scroll.put(pos, st);
                            }
                            else
                            {
                                log.Add(new DecodeLog(WARNING, "未定義のSCROLLを参照しています : " + data));
                            }
                        });
                        break;
                }

                int basech = 0;
                int ch2 = -1;
                foreach (int ch in NOTE_CHANNELS)
                {
                    if (ch <= channel && channel <= ch + 8)
                    {
                        basech = ch;
                        ch2 = channel - ch;
                        channellines.Add(line);
                        break;
                    }
                }
                // 5/10KEY  => 7/14KEY
                if (ch2 == 7 || ch2 == 8)
                {
                    Mode mode = (model.Mode == Mode.BEAT_5K) ? Mode.BEAT_7K : (model.Mode == Mode.BEAT_10K ? Mode.BEAT_14K : null);
                    if (mode != null)
                    {
                        this.processData(line, (Action<double, int>)((pos, data) =>
                        {
                            model.Mode = mode;
                        }));
                    }
                }
                // 5/7KEY  => 10/14KEY			
                if (basech == P2_KEY_BASE || basech == P2_INVISIBLE_KEY_BASE || basech == P2_LONG_KEY_BASE || basech == P2_MINE_KEY_BASE)
                {
                    Mode mode = (model.Mode == Mode.BEAT_5K) ? Mode.BEAT_10K : (model.Mode == Mode.BEAT_7K ? Mode.BEAT_14K : null);
                    if (mode != null)
                    {
                        this.processData(line, (Action<double, int>)((pos, data) =>
                        {
                            model.Mode = mode;
                        }));
                    }
                }
            }
        }

        private int[] splitData(String line)
        {
            int @base = model.getBase();
            int findex = line.IndexOf(":") + 1;
            int lindex = line.Length;
            int split = (lindex - findex) / 2;
            int[] result = new int[split];
            for (int i = 0; i < split; i++)
            {
                if (@base == 62)
                {
                    result[i] = ChartDecoder.parseInt62(line[findex + i * 2], line[findex + i * 2 + 1]);
                }
                else
                {
                    result[i] = ChartDecoder.parseInt36(line[findex + i * 2], line[findex + i * 2 + 1]);
                }
                if (result[i] == -1)
                {
                    log.Add(new DecodeLog(WARNING, model.Title + ":チャンネル定義中の不正な値:" + line));
                    result[i] = 0;
                }
            }
            return result;
        }

        private void processData(String line, Action<double, int> processor)
        {
            int @base = model.getBase();
            int findex = line.IndexOf(":") + 1;
            int lindex = line.Length;
            int split = (lindex - findex) / 2;
            int result;
            for (int i = 0; i < split; i++)
            {
                if (@base == 62)
                {
                    result = ChartDecoder.parseInt62(line[findex + i * 2], line[findex + i * 2 + 1]);
                }
                else
                {
                    result = ChartDecoder.parseInt36(line[findex + i * 2], line[findex + i * 2 + 1]);
                }
                if (result > 0)
                {
                    processor((double)i / split, result);
                }
                else if (result == -1)
                {
                    log.Add(new DecodeLog(WARNING, model.Title + ":チャンネル定義中の不正な値:" + line));
                }
            }
        }

        private SortedDictionary<Double, Double> bpmchange = new SortedDictionary<Double, Double>();
        private SortedDictionary<Double, Double> stop = new SortedDictionary<Double, Double>();
        private SortedDictionary<Double, Double> scroll = new SortedDictionary<Double, Double>();

        private static int[] CHANNELASSIGN_BEAT5 = { 0, 1, 2, 3, 4, 5, -1, -1, -1, 6, 7, 8, 9, 10, 11, -1, -1, -1 };
        private static int[] CHANNELASSIGN_BEAT7 = { 0, 1, 2, 3, 4, 7, -1, 5, 6, 8, 9, 10, 11, 12, 15, -1, 13, 14 };
        private static int[] CHANNELASSIGN_POPN = { 0, 1, 2, 3, 4, -1, -1, -1, -1, -1, 5, 6, 7, 8, -1, -1, -1, -1 };

        private SortedDictionary<Double, TimeLineCache> tlcache;

        /**
         * SectionモデルからTimeLineモデルを作成し、BMSModelに登録する
         */
        public void makeTimeLines(int[] wavmap, int[] bgamap, SortedDictionary<Double, TimeLineCache> tlcache, List<LongNote>[] lnlist, LongNote[] startln)
        {
            int lnobj = model.LNObj;
            var lnmode = model.LNMode;
            this.tlcache = tlcache;
            int[] cassign = model.Mode == Mode.POPN_9K ? CHANNELASSIGN_POPN :
               (model.Mode == Mode.BEAT_7K || model.Mode == Mode.BEAT_14K ? CHANNELASSIGN_BEAT7 : CHANNELASSIGN_BEAT5);
            int @base = model.getBase();
            // 小節線追加
            TimeLine basetl = getTimeLine(sectionnum);
            basetl.setSectionLine(true);

            if (poor.Length > 0)
            {
                Layer.Sequence[] poors = new Layer.Sequence[poor.Length + 1];
                int poortime = 500;

                for (int i = 0; i < poor.Length; i++)
                {
                    if (bgamap[poor[i]] != -2)
                    {
                        poors[i] = new Layer.Sequence((long)(i * poortime / poor.Length), bgamap[poor[i]]);
                    }
                    else
                    {
                        poors[i] = new Layer.Sequence((long)(i * poortime / poor.Length), -1);
                    }
                }
                poors[poors.Length - 1] = new Layer.Sequence(poortime);
                basetl.setEventlayer(new Layer[] { new Layer(new Layer.Event(EventType.MISS, 1), new Layer.Sequence[][] { poors }) });
            }
            // BPM変化。ストップシーケンステーブル準備
            var stops = stop.GetEnumerator();
            var ste = stops.MoveNext() ? stops.Current : default;
            var bpms = bpmchange.GetEnumerator();
            var bce = bpms.MoveNext() ? bpms.Current : default;
            var scrolls = scroll.GetEnumerator();
            var sce = scrolls.MoveNext() ? scrolls.Current : default;

            while (!ste.Equals(default(KeyValuePair<double, double>)) || !bce.Equals(default(KeyValuePair<double, double>)) || !sce.Equals(default(KeyValuePair<double, double>)))
            {
                double bc = !bce.Equals(default(KeyValuePair<double, double>)) ? bce.Key : 2;
                double st = !ste.Equals(default(KeyValuePair<double, double>)) ? ste.Key : 2;
                double sc = !sce.Equals(default(KeyValuePair<double, double>)) ? sce.Key : 2;
                if (sc <= st && sc <= bc)
                {
                    getTimeLine(sectionnum + sc * rate).setScroll(sce.Value);
                    sce = scrolls.MoveNext() ? scrolls.Current : default;
                }
                else if (bc <= st)
                {
                    getTimeLine(sectionnum + bc * rate).setBPM(bce.Value);
                    bce = bpms.MoveNext() ? bpms.Current : default;
                }
                else if (st <= 1)
                {
                    TimeLine tl = getTimeLine(sectionnum + ste.Key * rate);
                    tl.setStop((long)(1000.0 * 1000 * 60 * 4 * ste.Value / (tl.getBPM())));
                    ste = stops.MoveNext() ? stops.Current : default;
                }
            }

            foreach (String line in channellines)
            {
                int channel = ChartDecoder.parseInt36(line[4], line[5]);
                int tmpkey = 0;
                if (channel >= P1_KEY_BASE && channel < P1_KEY_BASE + 9)
                {
                    tmpkey = cassign[channel - P1_KEY_BASE];
                    channel = P1_KEY_BASE;
                }
                else if (channel >= P2_KEY_BASE && channel < P2_KEY_BASE + 9)
                {
                    tmpkey = cassign[channel - P2_KEY_BASE + 9];
                    channel = P1_KEY_BASE;
                }
                else if (channel >= P1_INVISIBLE_KEY_BASE && channel < P1_INVISIBLE_KEY_BASE + 9)
                {
                    tmpkey = cassign[channel - P1_INVISIBLE_KEY_BASE];
                    channel = P1_INVISIBLE_KEY_BASE;
                }
                else if (channel >= P2_INVISIBLE_KEY_BASE && channel < P2_INVISIBLE_KEY_BASE + 9)
                {
                    tmpkey = cassign[channel - P2_INVISIBLE_KEY_BASE + 9];
                    channel = P1_INVISIBLE_KEY_BASE;
                }
                else if (channel >= P1_LONG_KEY_BASE && channel < P1_LONG_KEY_BASE + 9)
                {
                    tmpkey = cassign[channel - P1_LONG_KEY_BASE];
                    channel = P1_LONG_KEY_BASE;
                }
                else if (channel >= P2_LONG_KEY_BASE && channel < P2_LONG_KEY_BASE + 9)
                {
                    tmpkey = cassign[channel - P2_LONG_KEY_BASE + 9];
                    channel = P1_LONG_KEY_BASE;
                }
                else if (channel >= P1_MINE_KEY_BASE && channel < P1_MINE_KEY_BASE + 9)
                {
                    tmpkey = cassign[channel - P1_MINE_KEY_BASE];
                    channel = P1_MINE_KEY_BASE;
                }
                else if (channel >= P2_MINE_KEY_BASE && channel < P2_MINE_KEY_BASE + 9)
                {
                    tmpkey = cassign[channel - P2_MINE_KEY_BASE + 9];
                    channel = P1_MINE_KEY_BASE;
                }
                int key = tmpkey;
                if (key == -1)
                {
                    continue;
                }
                switch (channel)
                {
                    case P1_KEY_BASE:
                        this.processData(line, (pos, data) =>
                        {
                            // normal note, lnobj
                            TimeLine tl = getTimeLine(sectionnum + rate * pos);
                            if (tl.existNote(key))
                            {
                                log.Add(new DecodeLog(WARNING, "通常ノート追加時に衝突が発生しました : " + (key + 1) + ":"
                                        + tl.getTime()));
                            }
                            if (data == lnobj)
                            {
                                // LN終端処理
                                // TODO 高速化のために直前のノートを記録しておく
                                foreach (var e in tlcache)
                                {
                                    if (e.Key >= tl.getSection())
                                    {
                                        continue;
                                    }
                                    TimeLine tl2 = e.Value.timeline;
                                    if (tl2.existNote(key))
                                    {
                                        Note note = tl2.getNote(key);
                                        if (note is NormalNote)
                                        {
                                            // LNOBJの直前のノートをLNに差し替える
                                            LongNote ln = new LongNote(note.getWav());
                                            ln.Type = lnmode;
                                            tl2.setNote(key, ln);
                                            LongNote lnend = new LongNote(-2);
                                            tl.setNote(key, lnend);
                                            ln.Pair = lnend;

                                            if (lnlist[key] == null)
                                            {
                                                lnlist[key] = new List<LongNote>();
                                            }
                                            lnlist[key].Add(ln);
                                            break;
                                        }
                                        else if (note is LongNote && ((LongNote)note).Pair == null)
                                        {
                                            log.Add(new DecodeLog(WARNING,
                                                    "LNレーンで開始定義し、LNオブジェクトで終端定義しています。レーン: " + (key + 1) + " - Section : "
                                                            + tl2.getSection() + " - " + tl.getSection()));
                                            LongNote lnend = new LongNote(-2);
                                            tl.setNote(key, lnend);
                                            ((LongNote)note).Pair = lnend;

                                            if (lnlist[key] == null)
                                            {
                                                lnlist[key] = new List<LongNote>();
                                            }
                                            lnlist[key].Add((LongNote)note);
                                            startln[key] = null;
                                            break;
                                        }
                                        else
                                        {
                                            log.Add(new DecodeLog(WARNING, "LNオブジェクトの対応が取れません。レーン: " + key
                                                    + " - Time(ms):" + tl2.getTime()));
                                            break;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                tl.setNote(key, new NormalNote(wavmap[data]));
                            }
                        });
                        break;

                    case P1_INVISIBLE_KEY_BASE:
                        this.processData(line, (pos, data) =>
                        {
                            getTimeLine(sectionnum + rate * pos).setHiddenNote(key, new NormalNote(wavmap[data]));
                        });
                        break;
                    case P1_LONG_KEY_BASE:
                        this.processData(line, (pos, data) =>
                        {
                            // long note
                            TimeLine tl = getTimeLine(sectionnum + rate * pos);
                            bool insideln = false;
                            if (!insideln && lnlist[key] != null)
                            {
                                double section = tl.getSection();
                                foreach (LongNote ln in lnlist[key])
                                {
                                    if (ln.getSection() <= section && section <= ln.Pair.getSection())
                                    {
                                        insideln = true;
                                        break;
                                    }
                                }
                            }

                            if (!insideln)
                            {
                                // LN処理
                                if (startln[key] == null)
                                {
                                    if (tl.existNote(key))
                                    {
                                        Note note = tl.getNote(key);
                                        log.Add(new DecodeLog(WARNING, "LN開始位置に通常ノートが存在します。レーン: "
                                                + (key + 1) + " - Time(ms):" + tl.getTime()));
                                        if (note is NormalNote && note.getWav() != wavmap[data])
                                        {
                                            tl.addBackGroundNote(note);
                                        }
                                    }
                                    LongNote ln = new LongNote(wavmap[data]);
                                    tl.setNote(key, ln);
                                    startln[key] = ln;
                                }
                                else if (startln[key].getSection() == Double.MinValue)
                                {
                                    startln[key] = null;
                                }
                                else
                                {
                                    // LN終端処理
                                    foreach (var e in tlcache)
                                    {
                                        if (e.Key >= tl.getSection())
                                        {
                                            continue;
                                        }

                                        TimeLine tl2 = e.Value.timeline;
                                        if (tl2.getSection() == startln[key].getSection())
                                        {
                                            Note note = startln[key];
                                            ((LongNote)note).Type = lnmode;
                                            LongNote noteend = new LongNote(startln[key].getWav() != wavmap[data] ? wavmap[data] : -2);
                                            tl.setNote(key, noteend);
                                            ((LongNote)note).Pair = noteend;
                                            if (lnlist[key] == null)
                                            {
                                                lnlist[key] = new List<LongNote>();
                                            }
                                            lnlist[key].Add((LongNote)note);

                                            startln[key] = null;
                                            break;
                                        }
                                        else if (tl2.existNote(key))
                                        {
                                            Note note = tl2.getNote(key);
                                            log.Add(new DecodeLog(WARNING, "LN内に通常ノートが存在します。レーン: "
                                                    + (key + 1) + " - Time(ms):" + tl2.getTime()));
                                            tl2.setNote(key, null);
                                            if (note is NormalNote)
                                            {
                                                tl2.addBackGroundNote(note);
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                if (startln[key] == null)
                                {
                                    LongNote ln = new LongNote(wavmap[data]);
                                    ln.setSection(Double.MinValue);
                                    startln[key] = ln;
                                    log.Add(new DecodeLog(WARNING, "LN内にLN開始ノートを定義しようとしています : "
                                            + (key + 1) + " - Section : " + tl.getSection() + " - Time(ms):" + tl.getTime()));
                                }
                                else
                                {
                                    if (startln[key].getSection() != Double.MinValue)
                                    {
                                        tlcache[startln[key].getSection()].timeline.setNote(key, null);
                                    }
                                    startln[key] = null;
                                    log.Add(new DecodeLog(WARNING, "LN内にLN終端ノートを定義しようとしています : "
                                            + (key + 1) + " - Section : " + tl.getSection() + " - Time(ms):" + tl.getTime()));
                                }
                            }
                        });
                        break;

                    case P1_MINE_KEY_BASE:
                        // mine note
                        this.processData(line, (pos, data) =>
                        {
                            TimeLine tl = getTimeLine(sectionnum + rate * pos);
                            bool insideln = tl.existNote(key);
                            if (!insideln && lnlist[key] != null)
                            {
                                double section = tl.getSection();
                                foreach (LongNote ln in lnlist[key])
                                {
                                    if (ln.getSection() <= section && section <= ln.Pair.getSection())
                                    {
                                        insideln = true;
                                        break;
                                    }
                                }
                            }

                            if (!insideln)
                            {
                                if (@base == 62)
                                {
                                    data = ChartDecoder.parseInt36(ChartDecoder.toBase62(data), 0); //間違った数値を再計算、62進数文字に戻して36進数数値化。
                                }
                                tl.setNote(key, new MineNote(wavmap[0], data));
                            }
                            else
                            {
                                log.Add(new DecodeLog(WARNING, "地雷ノート追加時に衝突が発生しました : " + (key + 1) + ":"
                                        + tl.getTime()));
                            }
                        });
                        break;
                    case LANE_AUTOPLAY:
                        // BGレーン
                        this.processData(line, (pos, data) =>
                        {
                            getTimeLine(sectionnum + rate * pos).addBackGroundNote(new NormalNote(wavmap[data]));
                        });
                        break;
                    case BGA_PLAY:
                        this.processData(line, (pos, data) =>
                        {
                            getTimeLine(sectionnum + rate * pos).setBGA(bgamap[data]);
                        });
                        break;
                    case LAYER_PLAY:
                        this.processData(line, (pos, data) =>
                        {
                            getTimeLine(sectionnum + rate * pos).setLayer(bgamap[data]);
                        });
                        break;

                }
            }
        }

        private TimeLine getTimeLine(double section)
        {
            if (tlcache.TryGetValue(section, out var tlc))
                return tlc.timeline;

            var le = tlcache.LastOrDefault(c => c.Key < section);
            double scroll = le.Value.timeline.getScroll();
            double bpm = le.Value.timeline.getBPM();
            double time = le.Value.time + le.Value.timeline.getMicroStop() + (240000.0 * 1000 * (section - le.Key)) / bpm;

            TimeLine tl = new TimeLine(section, (long)time, model.Mode.key);
            tl.setBPM(bpm);
            tl.setScroll(scroll);
            tlcache.put(section, new TimeLineCache(time, tl));
            return tl;
        }
    }
}
