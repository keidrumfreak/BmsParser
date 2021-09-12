using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BmsParser
{
    public class Section
    {
        BmsModel model;
        List<DecodeLog> logs;
        List<string> channelLines = new();
        double sectionNum;
        double rate = 1.0;
        int[] poor = Array.Empty<int>();

        SortedList<double, double> bpms = new();
        SortedList<double, double> stop = new();
        SortedList<double, double> scroll = new();

        Channel[] noteChannels = { Channel.P1KeyBase, Channel.P2KeyBase, Channel.P1InvisibleKeyBase, Channel.P2InvisibleKeyBase, Channel.P1LongKeyBase, Channel.P2LongKeyBase, Channel.P1MineKeyBase, Channel.P2MineKeyBase };

        public Section(BmsModel model, Section prev, IEnumerable<string> lines, Dictionary<int, double> bpmTable,
            Dictionary<int, double> stopTable, Dictionary<int, double> scrollTable, List<DecodeLog> logs)
        {
            this.model = model;
            this.logs = logs;

            sectionNum = prev == null ? 0 : (prev.sectionNum + prev.rate);

            foreach (var line in lines)
            {
                if (!ChartDecoder.TryParseInt36(line, 4, out var channel))
                    channel = -1;

                switch ((Channel)channel)
                {
                    case Channel.Illegal:
                        logs.Add(new DecodeLog(State.Warning, $"チャンネル定義が無効です : {line}"));
                        break;
                    case Channel.LaneAutoPlay:  // BGレーン
                    case Channel.BgaPlay:       // BGAレーン
                    case Channel.LayerPlay:     // レイヤー
                        channelLines.Add(line);
                        break;
                    case Channel.SectionRate:   // 小節の拡大率
                        if (!double.TryParse(line[(line.IndexOf(":") + 1)..], out rate))
                            logs.Add(new DecodeLog(State.Warning, $"小節の拡大率が不正です : {line}"));
                        break;
                    case Channel.BpmChange:     // BPM変化
                        processData(line, (pos, data) => bpms.Add(pos, (double)(data / 36) * 16 + (data % 36)));
                        break;
                    case Channel.PoorPlay:      // POORアニメーション
                        poor = splitData(line).ToArray();
                        // アニメーションが単一画像のみの定義の場合、0を除外する(ミスレイヤーチャンネルの定義が曖昧)
                        var singleid = 0;
                        foreach (var id in poor.Where(i => i != 0))
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
                        if (singleid != -1)
                            poor = new int[] { singleid };
                        break;
                    case Channel.BpmChangeExtend:   // BPM変化(拡張)
                        processData(line, (pos, data) =>
                        {
                            if (!bpmTable.TryGetValue(data, out var bpm))
                            {
                                logs.Add(new DecodeLog(State.Warning, $"未定義のBPM変化を参照しています : {data}"));
                                return;
                            }
                            bpms.Add(pos, bpm);
                        });
                        break;
                    case Channel.Stop:          // ストップシーケンス
                        processData(line, (pos, data) =>
                        {
                            if (!stopTable.TryGetValue(data, out var st))
                            {
                                logs.Add(new DecodeLog(State.Warning, $"未定義のSTOPを参照しています : {data}"));
                                return;
                            }
                            stop.Add(pos, st);
                        });
                        break;
                    case Channel.Scroll:
                        processData(line, (pos, data) =>
                        {
                            if (!scrollTable.TryGetValue(data, out var st))
                            {
                                logs.Add(new DecodeLog(State.Warning, $"未定義のSCROLLを参照しています : {data}"));
                                return;
                            }
                            scroll.Add(pos, st);
                        });
                        break;
                }

                Channel baseCH = 0;
                var ch2 = -1;
                foreach (Channel ch in noteChannels)
                {
                    if ((int)ch <= channel && channel <= (int)ch + 8)
                    {
                        baseCH = ch;
                        ch2 = channel - (int)ch;
                        channelLines.Add(line);
                        break;
                    }
                }
                // 5/10KEY -> 7/14KEY
                if (ch2 == 7 || ch2 == 8)
                {
                    var mode = (model.Mode == Mode.Beat5K) ? Mode.Beat7K : (model.Mode == Mode.Beat10K ? Mode.Beat14K : null);
                    if (mode != null)
                    {
                        processData(line, (pos, data) => {
                            model.Mode = mode;
                        });
                    }
                }
                // 5/7KEY -> 10/14KEY
                if (baseCH == Channel.P2KeyBase || baseCH == Channel.P2InvisibleKeyBase || baseCH == Channel.P2LongKeyBase || baseCH == Channel.P2MineKeyBase)
                {
                    var mode = (model.Mode == Mode.Beat5K) ? Mode.Beat7K : (model.Mode == Mode.Beat10K ? Mode.Beat14K : null);
                    if (mode != null)
                    {
                        processData(line, (pos, data) => {
                            model.Mode = mode;
                        });
                    }
                }
            }
        }

        int[] beat5ChannelAssign = { 0, 1, 2, 3, 4, 5, -1, -1, -1, 6, 7, 8, 9, 10, 11, -1, -1, -1 };
        int[] beat7ChannelAssign = { 0, 1, 2, 3, 4, 7, -1, 5, 6, 8, 9, 10, 11, 12, 15, -1, 13, 14 };
        int[] popnChannelAssign = { 0, 1, 2, 3, 4, -1, -1, -1, -1, -1, 5, 6, 7, 8, -1, -1, -1, -1 };

        public void MakeTimeLine(int[] wavMap, int[] bgaMap, SortedDictionary<double, TimeLineCache> tlCache, List<LongNote>[] lnList, LongNote[] startLN)
        {
            var baseTL = getTimeLine(sectionNum, tlCache);
            baseTL.IsSectionLine = true;

            if (poor.Length > 0)
            {
                var poors = new Sequece[poor.Length + 1];
                var poorTime = 500;
                for (var i = 0; i < poor.Length; i++)
                {
                    if (bgaMap[poor[i]] != -2)
                    {
                        poors[i] = new Sequece(i * poorTime / poor.Length, bgaMap[poor[i]]);
                    }
                    else
                    {
                        poors[i] = new Sequece(i * poorTime / poor.Length, -1);
                    }
                }
                poors[poors.Length - 1] = new Sequece(poorTime);
                baseTL.EventLayer = new[] { new Layer(new Event(EventType.Miss, 1), new Sequece[][] { poors }) };
            }

            var hasStop = stop.Keys.Any();
            var ste = stop.FirstOrDefault();
            var hasBpm = bpms.Keys.Any();
            var bce = bpms.FirstOrDefault();
            var hasScroll = scroll.Keys.Any();
            var sce = scroll.FirstOrDefault();
            if (hasStop || hasBpm || hasScroll)
            {
                var bc = hasBpm ? bce.Key : 2;
                var st = hasStop ? ste.Key : 2;
                var sc = hasScroll ? sce.Key : 2;
                if (sc <= st && sc <= bc)
                {
                    getTimeLine(sectionNum + sc * rate, tlCache).Scroll = sce.Value;
                    var scq = scroll.SkipWhile(s => s.Key != sce.Key);
                    hasScroll = scq.Count() > 1;
                    if (hasScroll)
                    {
                        sce = scq.Skip(1).First();
                    }
                }
                else if (bc >= st)
                {
                    getTimeLine(sectionNum + bc * rate, tlCache).Scroll = bce.Value;
                    var bcq = bpms.SkipWhile(b => b.Key != bce.Key);
                    hasBpm = bcq.Count() > 1;
                    if (hasBpm)
                    {
                        bce = bcq.Skip(1).First();
                    }
                }
                else if (st <= 1)
                {
                    var tl = getTimeLine(sectionNum + st * rate, tlCache);
                    tl.StopMicrosecond = (long)(1000.0 * 1000 * 60 * 4 * ste.Value / tl.Bpm);
                    var stq = stop.SkipWhile(s => s.Key != ste.Key);
                    hasStop = stq.Count() > 1;
                    if (hasStop)
                    {
                        ste = stq.Skip(1).First();
                    }
                }
            }

            var cassign = model.Mode == Mode.Popn9K ? popnChannelAssign :
                model.Mode == Mode.Beat7K || model.Mode == Mode.Beat14K ? beat7ChannelAssign : beat5ChannelAssign;
            foreach (var line in channelLines)
            {
                ChartDecoder.TryParseInt36(line, 4, out var channel);
                var key = 0;
                var q = Enum.GetValues(typeof(Channel)).Cast<Channel>()
                    .Where(nc => (int)nc <= channel && channel < (int)nc + 9);
                if (q.Any())
                {
                    var note = q.First();
                    key = cassign[channel - (int)note];
                    channel = note switch
                    {
                        Channel.P1KeyBase or Channel.P2KeyBase => (int)Channel.P1KeyBase,
                        Channel.P1InvisibleKeyBase or Channel.P2InvisibleKeyBase => (int)Channel.P1InvisibleKeyBase,
                        Channel.P1LongKeyBase or Channel.P2LongKeyBase => (int)Channel.P1LongKeyBase,
                        Channel.P1MineKeyBase or Channel.P2MineKeyBase => (int)Channel.P1MineKeyBase,
                        _ => 0
                    };
                }
                if (key == -1)
                    continue;
                switch ((Channel)channel)
                {
                    case Channel.P1KeyBase:
                        // normal note, lnobj
                        processData(line, (pos, data) =>
                        {
                            var tl = getTimeLine(sectionNum + rate * pos, tlCache);
                            if (tl.ExistNote(key))
                                logs.Add(new DecodeLog(State.Warning, $"通常ノート追加時に衝突が発生しました : {key + 1}:{tl.Time}"));
                            if (data != model.LNObj)
                            {
                                tl.SetNote(key, new NormalNote(wavMap[data]));
                                return;
                            }

                            // LN終端処理
                            foreach (var e in tlCache.OrderByDescending(t => t.Key))
                            {
                                if (e.Key >= tl.Section)
                                    continue;
                                var tl2 = e.Value.TimeLine;
                                if (!tl2.ExistNote(key))
                                    continue;
                                var note = tl2.GetNote(key);
                                if (note is NormalNote)
                                {
                                    // LOOBJの直前のノートをLNに差し替える
                                    var ln = new LongNote(note.Wav);
                                    ln.Type = model.LNMode;
                                    tl2.SetNote(key, ln);
                                    var lnEnd = new LongNote(-2);
                                    tl.SetNote(key, lnEnd);
                                    ln.Pair = lnEnd;
                                    if (lnList[key] == null)
                                        lnList[key] = new List<LongNote>();
                                    lnList[key].Add(ln);
                                    break;
                                }
                                else if (note is LongNote ln && ln.Pair == null)
                                {
                                    logs.Add(new DecodeLog(State.Warning, $"LNレーンで開始定義し、LNオブジェクトで終端定義しています。レーン: {key + 1} - Section : {tl2.Section} - {tl.Section}"));
                                    var lnEnd = new LongNote(-2);
                                    tl.SetNote(key, lnEnd);
                                    ln.Pair = lnEnd;
                                    if (lnList[key] == null)
                                        lnList[key] = new List<LongNote>();
                                    lnList[key].Add(ln);
                                    break;
                                }
                                else
                                {
                                    logs.Add(new DecodeLog(State.Warning, $"LNオブジェクトの対応が取れません。レーン: {key} - Time(ms):{tl2.Time}"));
                                    break;
                                }
                            }
                        });
                        break;
                    case Channel.P1InvisibleKeyBase:
                        processData(line, (pos, data) => getTimeLine(sectionNum + rate * pos, tlCache).SetHiddenNote(key, new NormalNote(wavMap[data])));
                        break;
                    case Channel.P1LongKeyBase:
                        processData(line, (pos, data) =>
                        {
                            // long note
                            var tl = getTimeLine(sectionNum + rate * pos, tlCache);
                            var insideLN = lnList[key] != null
                                && lnList[key].Any(ln => ln.Section <= tl.Section && tl.Section <= ln.Pair.Section);

                            if (insideLN)
                            {
                                if (startLN[key] == null)
                                {
                                    var ln = new LongNote(wavMap[data]);
                                    ln.Section = double.MinValue;
                                    startLN[key] = ln;
                                    logs.Add(new DecodeLog(State.Warning, $"LN内にLN開始ノートを定義しようとしています : {key + 1} - Section : {tl.Section} - Time(ms):{tl.Time}"));
                                }
                                else
                                {
                                    if (startLN[key].Section != double.MinValue)
                                    {
                                        tlCache[startLN[key].Section].TimeLine.SetNote(key, null);
                                    }
                                    startLN[key] = null;
                                    logs.Add(new DecodeLog(State.Warning, $"LN内にLN終端ノートを定義しようとしています : {key + 1} - Section : {tl.Section} - Time(ms):{tl.Time}"));
                                }
                                return;
                            }

                            // LN処理
                            if (startLN[key] == null)
                            {
                                if (tl.ExistNote(key))
                                {
                                    var note = tl.GetNote(key);
                                    logs.Add(new DecodeLog(State.Warning, $"LN開始位置に通常ノートが存在します。レーン: {key + 1} - Time(ms):{tl.Time}"));
                                    if (note is NormalNote && note.Wav != wavMap[data])
                                        tl.AddBackGroundNote(note);
                                }
                                var ln = new LongNote(wavMap[data]);
                                tl.SetNote(key, ln);
                            }
                            else if (startLN[key].Section == double.MinValue)
                            {
                                startLN[key] = null;
                            }
                            else
                            {
                                // LN終端処理
                                foreach (var e in tlCache.OrderByDescending(t => t.Key))
                                {
                                    if (e.Key >= tl.Section)
                                        continue;
                                    var tl2 = e.Value.TimeLine;
                                    if (tl2.Section == startLN[key].Section)
                                    {
                                        var note = startLN[key];
                                        note.Type = model.LNMode;
                                        var noteEnd = new LongNote(startLN[key].Wav != wavMap[data] ? wavMap[data] : -2);
                                        tl.SetNote(key, noteEnd);
                                        if (lnList[key] == null)
                                            lnList[key] = new List<LongNote>();
                                        lnList[key].Add(note);
                                        break;
                                    }
                                    else if (tl2.ExistNote(key))
                                    {
                                        var note = tl2.GetNote(key);
                                        logs.Add(new DecodeLog(State.Warning, $"LN内に通常ノートが存在します。レーン: {key + 1} - Time(ms):{tl2.Time}"));
                                        tl2.SetNote(key, null);
                                        if (note is NormalNote)
                                            tl2.AddBackGroundNote(note);
                                    }
                                }
                            }
                        });
                        break;
                    case Channel.P1MineKeyBase:
                        // mine note
                        processData(line, (pos, data) =>
                        {
                            var tl = getTimeLine(sectionNum + rate * pos, tlCache);
                            var inSideLN = tl.ExistNote(key);
                            if (!inSideLN && lnList[key] != null)
                            {
                                inSideLN = lnList[key].Any(ln => ln.Section <= tl.Section && tl.Section <= ln.Pair.Section);
                            }
                            if (!inSideLN)
                            {
                                tl.SetNote(key, new MineNote(wavMap[0], data));
                            }
                            else
                            {
                                logs.Add(new DecodeLog(State.Warning, $"地雷ノート追加時に衝突が発生しました : {key + 1}:{tl.Time}"));
                            }
                        });
                        break;
                    case Channel.LaneAutoPlay:
                        processData(line, (pos, data) => getTimeLine(sectionNum + rate * pos, tlCache).AddBackGroundNote(new NormalNote(wavMap[data])));
                        break;
                    case Channel.BgaPlay:
                        processData(line, (pos, data) => getTimeLine(sectionNum + rate * pos, tlCache).BgaID = bgaMap[data]);
                        break;
                    case Channel.LayerPlay:
                        processData(line, (pos, data) => getTimeLine(sectionNum + rate * pos, tlCache).LayerID = bgaMap[data]);
                        break;
                }
            }
        }

        private TimeLine getTimeLine(double section, SortedDictionary<double, TimeLineCache> tlCache)
        {
            if (tlCache.TryGetValue(section, out var tlc))
                return tlc.TimeLine;
            var le = tlCache.OrderByDescending(t => t.Key).FirstOrDefault(t => t.Key < section);
            var scroll = le.Value.TimeLine.Scroll;
            var bpm = le.Value.TimeLine.Bpm;
            var time = le.Value.Time + le.Value.TimeLine.StopMicrosecond + (240000.0 * 1000 * (section - le.Key)) / bpm;

            var tl = new TimeLine(section, (long)time, model.Mode.Key);
            tl.Bpm = bpm;
            tl.Scroll = scroll;
            tlCache.Add(section, new TimeLineCache(time, tl));
            return tl;
        }

        private IEnumerable<int> splitData(string line)
        {
            var findex = line.IndexOf(":") + 1;
            var lindex = line.Length;
            var split = (lindex - findex) / 2;
            for (var i = 0; i < split; i++)
            {
                if (!ChartDecoder.TryParseInt36(line, findex + i * 2, out var result))
                {
                    logs.Add(new DecodeLog(State.Warning, $"{model.Title}:チャンネル定義中の不正な値:{line}"));
                    yield return 0;
                    continue;
                }
                yield return result;
            }
        }

        private void processData(string line, Action<double, int> processser)
        {
            var findex = line.IndexOf(":") + 1;
            var lindex = line.Length;
            var split = (lindex - findex) / 2;
            for (var i = 0; i < split; i++)
            {
                if (!ChartDecoder.TryParseInt36(line, findex + i * 2, out var result))
                {
                    logs.Add(new DecodeLog(State.Warning, $"{model.Title}:チャンネル定義中の不正な値:{line}"));
                    continue;
                }
                if (result > 0)
                    processser((double)i / split, result);
            }
        }
    }

    public enum Channel
    {
        Illegal = -1,
        LaneAutoPlay = 1,
        SectionRate = 2,
        BpmChange = 3,
        BgaPlay = 4,
        PoorPlay = 6,
        LayerPlay = 7,
        BpmChangeExtend = 8,
        Stop = 9,
        Scroll = 1020,
        P1KeyBase = 1 * 36 + 1,
        P2KeyBase = 2 * 36 + 1,
        P1InvisibleKeyBase = 3 * 36 + 1,
        P2InvisibleKeyBase = 4 * 36 + 1,
        P1LongKeyBase = 5 * 36 + 1,
        P2LongKeyBase = 6 * 36 + 1,
        P1MineKeyBase = 13 * 36 + 1,
        P2MineKeyBase = 14 * 36 + 1
    }
}
