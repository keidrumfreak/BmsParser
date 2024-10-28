﻿using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using BmsParser;
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

        public static readonly int[] NOTE_CHANNELS = [P1_KEY_BASE, P2_KEY_BASE ,P1_INVISIBLE_KEY_BASE, P2_INVISIBLE_KEY_BASE,
            P1_LONG_KEY_BASE, P2_LONG_KEY_BASE, P1_MINE_KEY_BASE, P2_MINE_KEY_BASE];

        /**
         * 小節の拡大倍率
         */
        private readonly double rate = 1.0;
        /**
         * POORアニメーション
         */
        private readonly int[] poor = [];

        private readonly BmsModel model;

        private readonly double sectionnum;

        private readonly List<DecodeLog> log;

        private readonly List<string> channellines;

        public Section(BmsModel model, Section? prev, List<string> lines, Dictionary<int, double> bpmtable,
                Dictionary<int, double> stoptable, Dictionary<int, double> scrolltable, List<DecodeLog> log)
        {
            this.model = model;
            this.log = log;
            var @base = model.Base;

            channellines = new List<string>(lines.Count);
            if (prev != null)
            {
                sectionnum = prev.sectionnum + prev.rate;
            }
            else
            {
                sectionnum = 0;
            }
            foreach (var line in lines)
            {
                var channel = ParseInt36(line[4], line[5]);
                switch (channel)
                {
                    case ILLEGAL:
                        log.Add(new DecodeLog(State.Warning, "チャンネル定義が無効です : " + line));
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
                        var colon_index = line.IndexOf(':');
                        if (!double.TryParse(line[(colon_index + 1)..], out rate))
                        {
                            log.Add(new DecodeLog(State.Warning, "小節の拡大率が不正です : " + line));
                        }
                        break;
                    // BPM変化
                    case BPM_CHANGE:
                        this.processData(line, (pos, data) =>
                        {
                            if (@base == 62)
                            {
                                data = ParseInt36(ToBase62(data), 0); //間違った数値を再計算、62進数文字に戻して36進数数値化。
                            }
                            bpmchange.Put(pos, (double)(data / 36) * 16 + (data % 36));
                        });
                        break;
                    // POORアニメーション
                    case POOR_PLAY:
                        poor = this.splitData(line);
                        // アニメーションが単一画像のみの定義の場合、0を除外する(ミスレイヤーチャンネルの定義が曖昧)
                        var singleid = 0;
                        foreach (var id in poor)
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
                            poor = [singleid];
                        }
                        break;
                    // BPM変化(拡張)
                    case BPM_CHANGE_EXTEND:
                        this.processData(line, (pos, data) =>
                        {
                            if (bpmtable.TryGetValue(data, out var bpm))
                            {
                                bpmchange.Put(pos, bpm);
                            }
                            else
                            {
                                log.Add(new DecodeLog(State.Warning, "未定義のBPM変化を参照しています : " + data));
                            }
                        });
                        break;
                    // ストップシーケンス
                    case STOP:
                        this.processData(line, (pos, data) =>
                        {
                            if (stoptable.TryGetValue(data, out var st))
                            {
                                stop.Put(pos, st);
                            }
                            else
                            {
                                log.Add(new DecodeLog(State.Warning, "未定義のSTOPを参照しています : " + data));
                            }
                        });
                        break;
                    // scroll
                    case SCROLL:
                        this.processData(line, (pos, data) =>
                        {
                            if (scrolltable.TryGetValue(data, out var st))
                            {
                                scroll.Put(pos, st);
                            }
                            else
                            {
                                log.Add(new DecodeLog(State.Warning, "未定義のSCROLLを参照しています : " + data));
                            }
                        });
                        break;
                }

                var basech = 0;
                var ch2 = -1;
                foreach (var ch in NOTE_CHANNELS)
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
                    var mode = (model.Mode == Mode.Beat5K) ? Mode.Beat7K : (model.Mode == Mode.Beat10K ? Mode.Beat14K : null);
                    if (mode != null)
                    {
                        this.processData(line, (pos, data) =>
                        {
                            model.Mode = mode;
                        });
                    }
                }
                // 5/7KEY  => 10/14KEY			
                if (basech == P2_KEY_BASE || basech == P2_INVISIBLE_KEY_BASE || basech == P2_LONG_KEY_BASE || basech == P2_MINE_KEY_BASE)
                {
                    var mode = (model.Mode == Mode.Beat5K) ? Mode.Beat10K : (model.Mode == Mode.Beat7K ? Mode.Beat14K : null);
                    if (mode != null)
                    {
                        this.processData(line, (pos, data) =>
                        {
                            model.Mode = mode;
                        });
                    }
                }
            }
        }

        private int[] splitData(string line)
        {
            var @base = model.Base;
            var findex = line.IndexOf(':') + 1;
            var lindex = line.Length;
            var split = (lindex - findex) / 2;
            var result = new int[split];
            for (var i = 0; i < split; i++)
            {
                if (@base == 62)
                {
                    result[i] = ParseInt62(line[findex + i * 2], line[findex + i * 2 + 1]);
                }
                else
                {
                    result[i] = ParseInt36(line[findex + i * 2], line[findex + i * 2 + 1]);
                }
                if (result[i] == -1)
                {
                    log.Add(new DecodeLog(State.Warning, model.Title + ":チャンネル定義中の不正な値:" + line));
                    result[i] = 0;
                }
            }
            return result;
        }

        private void processData(string line, Action<double, int> processor)
        {
            var @base = model.Base;
            var findex = line.IndexOf(':') + 1;
            var lindex = line.Length;
            var split = (lindex - findex) / 2;
            int result;
            for (var i = 0; i < split; i++)
            {
                if (@base == 62)
                {
                    result = ParseInt62(line[findex + i * 2], line[findex + i * 2 + 1]);
                }
                else
                {
                    result = ParseInt36(line[findex + i * 2], line[findex + i * 2 + 1]);
                }
                if (result > 0)
                {
                    processor((double)i / split, result);
                }
                else if (result == -1)
                {
                    log.Add(new DecodeLog(State.Warning, model.Title + ":チャンネル定義中の不正な値:" + line));
                }
            }
        }

        private readonly SortedDictionary<double, double> bpmchange = [];
        private readonly SortedDictionary<double, double> stop = [];
        private readonly SortedDictionary<double, double> scroll = [];

        private static readonly int[] CHANNELASSIGN_BEAT5 = [0, 1, 2, 3, 4, 5, -1, -1, -1, 6, 7, 8, 9, 10, 11, -1, -1, -1];
        private static readonly int[] CHANNELASSIGN_BEAT7 = [0, 1, 2, 3, 4, 7, -1, 5, 6, 8, 9, 10, 11, 12, 15, -1, 13, 14];
        private static readonly int[] CHANNELASSIGN_POPN = [0, 1, 2, 3, 4, -1, -1, -1, -1, -1, 5, 6, 7, 8, -1, -1, -1, -1];

        /// <summary>
        /// SectionモデルからTimeLineモデルを作成し、BMSModelに登録する
        /// </summary>
        /// <param name="wavmap"></param>
        /// <param name="bgamap"></param>
        /// <param name="tlcache"></param>
        /// <param name="lnlist"></param>
        /// <param name="startln"></param>
        public void MakeTimeLines(int[] wavmap, int[] bgamap, SortedDictionary<double, TimeLineCache> tlcache, List<LongNote>[] lnlist, LongNote?[] startln)
        {
            var lnobj = model.LNObj;
            var lnmode = model.LNMode;
            var cassign = model.Mode == Mode.Popn9K ? CHANNELASSIGN_POPN :
               (model.Mode == Mode.Beat7K || model.Mode == Mode.Beat14K ? CHANNELASSIGN_BEAT7 : CHANNELASSIGN_BEAT5);
            var @base = model.Base;
            // 小節線追加
            var basetl = getTimeLine(sectionnum);
            basetl.IsSectionLine = true;

            if (poor.Length > 0)
            {
                var poors = new Sequence[poor.Length + 1];
                var poortime = 500;

                for (var i = 0; i < poor.Length; i++)
                {
                    if (bgamap[poor[i]] != -2)
                    {
                        poors[i] = new Sequence(i * poortime / poor.Length, bgamap[poor[i]]);
                    }
                    else
                    {
                        poors[i] = new Sequence(i * poortime / poor.Length, -1);
                    }
                }
                poors[^1] = new Sequence(poortime);
                basetl.EventLayer = ([new Layer(new Event(EventType.Miss, 1), [poors])]);
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
                var bc = !bce.Equals(default(KeyValuePair<double, double>)) ? bce.Key : 2;
                var st = !ste.Equals(default(KeyValuePair<double, double>)) ? ste.Key : 2;
                var sc = !sce.Equals(default(KeyValuePair<double, double>)) ? sce.Key : 2;
                if (sc <= st && sc <= bc)
                {
                    getTimeLine(sectionnum + sc * rate).Scroll = sce.Value;
                    sce = scrolls.MoveNext() ? scrolls.Current : default;
                }
                else if (bc <= st)
                {
                    getTimeLine(sectionnum + bc * rate).Bpm = bce.Value;
                    bce = bpms.MoveNext() ? bpms.Current : default;
                }
                else if (st <= 1)
                {
                    var tl = getTimeLine(sectionnum + ste.Key * rate);
                    tl.MicroStop = (long)(1000.0 * 1000 * 60 * 4 * ste.Value / tl.Bpm);
                    ste = stops.MoveNext() ? stops.Current : default;
                }
            }

            foreach (var line in channellines)
            {
                var channel = ParseInt36(line[4], line[5]);
                var tmpkey = 0;
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
                var key = tmpkey;
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
                            var tl = getTimeLine(sectionnum + rate * pos);
                            if (tl.ExistNote(key))
                            {
                                log.Add(new DecodeLog(State.Warning, "通常ノート追加時に衝突が発生しました : " + (key + 1) + ":"
                                        + tl.Time));
                            }
                            if (data == lnobj)
                            {
                                // LN終端処理
                                // TODO 高速化のために直前のノートを記録しておく
                                foreach (var e in tlcache)
                                {
                                    if (e.Key >= tl.Section)
                                    {
                                        continue;
                                    }
                                    var tl2 = e.Value.Timeline;
                                    if (tl2.ExistNote(key))
                                    {
                                        var note = tl2.GetNote(key);
                                        if (note is NormalNote)
                                        {
                                            // LNOBJの直前のノートをLNに差し替える
                                            var ln = new LongNote(note.Wav)
                                            {
                                                Type = lnmode
                                            };
                                            tl2.SetNote(key, ln);
                                            var lnend = new LongNote(-2);
                                            tl.SetNote(key, lnend);
                                            ln.Pair = lnend;

                                            if (lnlist[key] == null)
                                            {
                                                lnlist[key] = [];
                                            }
                                            lnlist[key].Add(ln);
                                            break;
                                        }
                                        else if (note is LongNote ln && ln.Pair == null)
                                        {
                                            log.Add(new DecodeLog(State.Warning,
                                                    "LNレーンで開始定義し、LNオブジェクトで終端定義しています。レーン: " + (key + 1) + " - Section : "
                                                            + tl2.Section + " - " + tl.Section));
                                            var lnend = new LongNote(-2);
                                            tl.SetNote(key, lnend);
                                            ln.Pair = lnend;

                                            if (lnlist[key] == null)
                                            {
                                                lnlist[key] = [];
                                            }
                                            lnlist[key].Add(ln);
                                            startln[key] = null;
                                            break;
                                        }
                                        else
                                        {
                                            log.Add(new DecodeLog(State.Warning, "LNオブジェクトの対応が取れません。レーン: " + key
                                                    + " - Time(ms):" + tl2.Time));
                                            break;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                tl.SetNote(key, new NormalNote(wavmap[data]));
                            }
                        });
                        break;

                    case P1_INVISIBLE_KEY_BASE:
                        this.processData(line, (pos, data) =>
                        {
                            getTimeLine(sectionnum + rate * pos).SetHiddenNote(key, new NormalNote(wavmap[data]));
                        });
                        break;
                    case P1_LONG_KEY_BASE:
                        this.processData(line, (pos, data) =>
                        {
                            // long note
                            var tl = getTimeLine(sectionnum + rate * pos);
                            var insideln = false;
                            if (!insideln && lnlist[key] != null)
                            {
                                var section = tl.Section;
                                foreach (var ln in lnlist[key])
                                {
                                    if (ln.Section <= section && section <= ln.Pair?.Section)
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
                                    if (tl.ExistNote(key))
                                    {
                                        var note = tl.GetNote(key);
                                        log.Add(new DecodeLog(State.Warning, "LN開始位置に通常ノートが存在します。レーン: "
                                                + (key + 1) + " - Time(ms):" + tl.Time));
                                        if (note is NormalNote && note.Wav != wavmap[data])
                                        {
                                            tl.AddBackGroundNote(note);
                                        }
                                    }
                                    var ln = new LongNote(wavmap[data]);
                                    tl.SetNote(key, ln);
                                    startln[key] = ln;
                                }
                                else if (startln[key]!.Section == double.MinValue)
                                {
                                    startln[key] = null;
                                }
                                else
                                {
                                    // LN終端処理
                                    foreach (var e in tlcache)
                                    {
                                        if (e.Key >= tl.Section)
                                        {
                                            continue;
                                        }

                                        var tl2 = e.Value.Timeline;
                                        if (tl2.Section == startln[key]!.Section)
                                        {
                                            Note note = startln[key]!;
                                            ((LongNote)note).Type = lnmode;
                                            var noteend = new LongNote(startln[key]!.Wav != wavmap[data] ? wavmap[data] : -2);
                                            tl.SetNote(key, noteend);
                                            ((LongNote)note).Pair = noteend;
                                            if (lnlist[key] == null)
                                            {
                                                lnlist[key] = [];
                                            }
                                            lnlist[key].Add((LongNote)note);

                                            startln[key] = null;
                                            break;
                                        }
                                        else if (tl2.ExistNote(key))
                                        {
                                            var note = tl2.GetNote(key);
                                            log.Add(new DecodeLog(State.Warning, "LN内に通常ノートが存在します。レーン: "
                                                    + (key + 1) + " - Time(ms):" + tl2.Time));
                                            tl2.SetNote(key, null);
                                            if (note is NormalNote)
                                            {
                                                tl2.AddBackGroundNote(note);
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                if (startln[key] == null)
                                {
                                    var ln = new LongNote(wavmap[data])
                                    {
                                        Section = double.MinValue
                                    };
                                    startln[key] = ln;
                                    log.Add(new DecodeLog(State.Warning, "LN内にLN開始ノートを定義しようとしています : "
                                            + (key + 1) + " - Section : " + tl.Section + " - Time(ms):" + tl.Time));
                                }
                                else
                                {
                                    if (startln[key]!.Section != double.MinValue)
                                    {
                                        tlcache[startln[key]!.Section].Timeline.SetNote(key, null);
                                    }
                                    startln[key] = null;
                                    log.Add(new DecodeLog(State.Warning, "LN内にLN終端ノートを定義しようとしています : "
                                            + (key + 1) + " - Section : " + tl.Section + " - Time(ms):" + tl.Time));
                                }
                            }
                        });
                        break;

                    case P1_MINE_KEY_BASE:
                        // mine note
                        this.processData(line, (pos, data) =>
                        {
                            var tl = getTimeLine(sectionnum + rate * pos);
                            var insideln = tl.ExistNote(key);
                            if (!insideln && lnlist[key] != null)
                            {
                                var section = tl.Section;
                                foreach (var ln in lnlist[key])
                                {
                                    if (ln.Section <= section && section <= ln.Pair?.Section)
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
                                    data = ParseInt36(ToBase62(data), 0); //間違った数値を再計算、62進数文字に戻して36進数数値化。
                                }
                                tl.SetNote(key, new MineNote(wavmap[0], data));
                            }
                            else
                            {
                                log.Add(new DecodeLog(State.Warning, "地雷ノート追加時に衝突が発生しました : " + (key + 1) + ":"
                                        + tl.Time));
                            }
                        });
                        break;
                    case LANE_AUTOPLAY:
                        // BGレーン
                        this.processData(line, (pos, data) =>
                        {
                            getTimeLine(sectionnum + rate * pos).AddBackGroundNote(new NormalNote(wavmap[data]));
                        });
                        break;
                    case BGA_PLAY:
                        this.processData(line, (pos, data) =>
                        {
                            getTimeLine(sectionnum + rate * pos).BgaID = bgamap[data];
                        });
                        break;
                    case LAYER_PLAY:
                        this.processData(line, (pos, data) =>
                        {
                            getTimeLine(sectionnum + rate * pos).LayerID = bgamap[data];
                        });
                        break;

                }
            }

            Timeline getTimeLine(double section)
            {
                if (tlcache.TryGetValue(section, out var tlc))
                    return tlc.Timeline;

                var le = tlcache.LastOrDefault(c => c.Key < section);
                var scroll = le.Value.Timeline.Scroll;
                var bpm = le.Value.Timeline.Bpm;
                var time = le.Value.Time + le.Value.Timeline.MicroStop + 240000.0 * 1000 * (section - le.Key) / bpm;

                var tl = new Timeline(section, (long)time, model.Mode.Key)
                {
                    Bpm = bpm,
                    Scroll = scroll
                };
                tlcache.Put(section, new TimeLineCache(time, tl));
                return tl;
            }
        }
    }
}
