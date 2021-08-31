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
        int[] poor;

        SortedDictionary<double, double> bpmChange = new();
        SortedDictionary<double, double> stop = new();
        SortedDictionary<double, double> scroll = new();

        public Section(BmsModel model, Section prev, IEnumerable<string> lines, Dictionary<int, double> bpmTable,
            Dictionary<int, double> stopTable, Dictionary<int, double> scrollTable, List<DecodeLog> logs)
        {
            this.model = model;
            this.logs = logs;

            sectionNum = prev == null ? 0 : (prev.sectionNum + prev.rate);

            foreach (var line in lines)
            {
                if (!ChartDecoder.TryParseInt36(line.Substring(4, 2), out var channel))
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
                        if (!double.TryParse(line.Substring(line.IndexOf(":") + 1), out rate))
                            logs.Add(new DecodeLog(State.Warning, $"小節の拡大率が不正です : {line}"));
                        break;
                    case Channel.BpmChange:     // BPM変化
                        processData(line, (pos, data) => bpmChange.Add(pos, (double)(data / 36) * 16 + (data % 36)));
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
                            bpmChange.Add(pos, bpm);
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

                NoteChannels baseCH = 0;
                var ch2 = -1;
                foreach (NoteChannels ch in Enum.GetValues(typeof(NoteChannels)))
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
                if (baseCH == NoteChannels.P2KeyBase || baseCH == NoteChannels.P2InvisibleKeyBase || baseCH == NoteChannels.P2LongKeyBase || baseCH == NoteChannels.P2MineKeyBase)
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

        private IEnumerable<int> splitData(string line)
        {
            var findex = line.IndexOf(":") + 1;
            var lindex = line.Length;
            var split = (lindex - findex) / 2;
            for (var i = 0; i < split; i++)
            {
                if (!ChartDecoder.TryParseInt36(line.Substring(findex + i * 2, 2), out var result))
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
                if (!ChartDecoder.TryParseInt36(line.Substring(findex + i * 2, 2), out var result))
                {
                    logs.Add(new DecodeLog(State.Warning, $"{model.Title}:チャンネル定義中の不正な値:{line}"));
                    continue;
                }
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
        Scroll = 1020
    }

    public enum NoteChannels
    {
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
