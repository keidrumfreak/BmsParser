using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BmsParser
{
    class LineProcessor
    {
        static readonly CommandWord[] commands =
            [
                new("#PLAYER", ValueType.Number, nameof(BmsModel.Player)) { NumberRange = player => player >= 1 && player < 3 },
                new("#GENRE", ValueType.Text, nameof(BmsModel.Genre)),
                new("#TITLE", ValueType.Text, nameof(BmsModel.Title)),
                new("#SUBTITLE", ValueType.Text, nameof(BmsModel.Subtitle)),
                new("#ARTIST", ValueType.Text, nameof(BmsModel.Artist)),
                new("#SUBARTIST", ValueType.Text, nameof(BmsModel.Subartist)),
                new("#PLAYLEVEL", ValueType.Text, nameof(BmsModel.PlayLevel)),
                new("#RANK", ValueType.Number, nameof(BmsModel.JudgeRank)) { NumberRange = rank => 0 <= rank && rank <= 4, AppendProcess = model => model.JudgeRankType = JudgeRankType.BmsRank },
                new("#DEFEXRANK", ValueType.Number, nameof(BmsModel.JudgeRank)) { NumberRange = rank => rank > 0, AppendProcess = model => model.JudgeRankType = JudgeRankType.BmsDefEXRank },
                new("#TOTAL", ValueType.Number, nameof(BmsModel.Total)) { NumberRange = total => total > 0, AppendProcess = model => model.TotalType = TotalType.Bms },
                new("#VOLWAV", ValueType.Number, nameof(BmsModel.VolWav)),
                new("#STAGEFILE", ValueType.Path, nameof(BmsModel.StageFile)),
                new("#BACKBMP", ValueType.Path, nameof(BmsModel.BackBmp)),
                new("#PREVIEW", ValueType.Path, nameof(BmsModel.Preview)),
                new("#LNOBJ", ValueType.BaseNum, nameof(BmsModel.LNObj)),
                new("#LNMODE", ValueType.Number, nameof(BmsModel.LNMode)) { NumberRange = lnMode => Enum.IsDefined(typeof(LNMode), lnMode) },
                new("#DIFFICULTY", ValueType.Number, nameof(BmsModel.Difficulty)),
                new("#BANNER", ValueType.Path, nameof(BmsModel.Banner)),
                new("#BASE", ValueType.Number, nameof(BmsModel.Base)) { NumberRange = @base => @base == 62 }
            ];

        static readonly SequenceWord[] sequences =
            [
                new("#BPM", ValueType.Number) { NumberRange = bpm => bpm > 0 },
                new("#WAV", ValueType.Path),
                new("#BMP", ValueType.Path),
                new("#STOP", ValueType.Number) { NumberRange = stop => stop >= 0 },
                new("#SCROLL", ValueType.Number)
            ];

        public ConcurrentDictionary<int, double> BpmTable { get; } = new();

        public ConcurrentDictionary<int, double> StopTable { get; } = new();

        public ConcurrentDictionary<int, double> ScrollTable { get; } = new();

        public ConcurrentDictionary<int, ConcurrentBag<string>> BarTable { get; } = new();

        public List<string> WavList { get; } = new(62 * 62);
        public int[] WavMap { get; } = new int[62 * 62];

        public List<string> BgaList { get; } = new(62 * 62);
        public int[] BgaMap { get; } = new int[62 * 62];

        public LineProcessor()
        {
            Array.Fill(WavMap, -2);
            Array.Fill(BgaMap, -2);
        }

        public void Process(BmsModel model, string line, List<DecodeLog> logs)
        {
            if (line.Length <= 1)
                return;

            var c = line[1];
            var @base = model.Base;
            if (line[0] == '#' && '0' <= c && c <= '9' && line.Length > 6)
            {
                // 楽譜
                if (!int.TryParse(line[1..4], out var barNum))
                {
                    logs.Add(new DecodeLog(State.Warning, "小節に数字が定義されていません : " + line));
                    return;
                }
                lock (BarTable)
                {
                    if (!BarTable.TryGetValue(barNum, out var bar))
                    {
                        bar = [];
                        BarTable.Put(barNum, bar);
                    }
                    bar.Add(line);
                }
                return;
            }

            var seq = sequences.FirstOrDefault(s => s.IsMatch(line));
            if (seq != default)
            {
                seq.Process(line, model, logs, this);
                return;
            }

            var command = commands.FirstOrDefault(c => c.IsMatch(line));
            if (command != default)
            {
                command.Process(line, model, logs);
                return;
            }

            if (line[0] == '%' || line[0] == '@')
            {
                var index = line.IndexOf(' ');
                if (index == -1 || line.Length <= index + 1)
                    return;
                model.Values.Put(line[1..index], line[(index + 1)..]);
            }

            return;
        }

        abstract record Keyword(string Name)
        {
            public bool IsMatch(string line) => line.StartsWith(Name, StringComparison.OrdinalIgnoreCase);
        }

        record SequenceWord(string Name, ValueType ValueType) : Keyword(Name)
        {
            public Func<double, bool>? NumberRange { get; init; }

            public void Process(string line, BmsModel model, List<DecodeLog> logs, LineProcessor processor)
            {
                if (Name == "#BPM" && line[4] == ' ')
                {
                    if (!double.TryParse(line[5..].Trim(), out var bpm))
                    {
                        logs.Add(new DecodeLog(State.Warning, $"#BPMに数字が定義されていません : {line}"));
                        return;
                    }
                    if (bpm > 0)
                    {
                        model.Bpm = bpm;
                    }
                    else
                    {
                        logs.Add(new DecodeLog(State.Warning, $"#negative BPMはサポートされていません : {line}"));
                    }
                    return;
                }

                if (line.Length < Name.Length + 4 || model.Base == 62 ? !Utility.TryParseInt62(line[Name.Length..(Name.Length + 2)], out var seq) : !Utility.TryParseInt36(line[Name.Length..(Name.Length + 2)], out seq))
                {
                    logs.Add(new DecodeLog(State.Warning, $"{Name}xxは不十分な定義です : {line}"));
                    return;
                }

                var arg = line[(Name.Length + 3)..].Trim();
                switch (ValueType)
                {
                    case ValueType.Number:
                        if (!double.TryParse(arg, out var value))
                        {
                            logs.Add(new DecodeLog(State.Warning, $"{Name}xxに数字が定義されていません : {line}"));
                            return;
                        }
                        if (Name == "#STOP") value /= 192;
                        if (!NumberRange?.Invoke(value) ?? false)
                        {
                            logs.Add(new DecodeLog(State.Warning, $"#negative {Name[1..]}はサポートされていません : {line}"));
                            if (Name == "#BPM") return;
                            if (Name == "#STOP") value = Math.Abs(value);
                        }
                        var numTable = Name switch
                        {
                            "#STOP" => processor.StopTable,
                            "#BPM" => processor.BpmTable,
                            "#SCROLL" => processor.ScrollTable,
                            _ => throw new NotSupportedException()
                        };
                        numTable.Put(seq, value);
                        return;
                    case ValueType.Path:
                        var (list, map) = Name switch
                        {
                            "#BMP" => (processor.BgaList, processor.BgaMap),
                            "#WAV" => (processor.WavList, processor.WavMap),
                            _ => throw new NotSupportedException()
                        };
                        map[seq] = list.Count;
                        list.Add(arg.Replace('\\', '/'));
                        return;
                }
            }
        }

        record CommandWord(string Name, ValueType ValueType, string PropertyName) : Keyword(Name)
        {
            public Func<int, bool>? NumberRange { get; init; }

            public Action<BmsModel>? AppendProcess { get; init; }

            public void Process(string line, BmsModel model, List<DecodeLog> logs)
            {
                var arg = line[Name.Length..].Trim();
                var prop = typeof(BmsModel).GetProperty(PropertyName);
                if (prop == null)
                    return;
                switch (ValueType)
                {
                    case ValueType.Number:
                        if (!int.TryParse(arg, out var value))
                        {
                            logs.Add(new DecodeLog(State.Warning, $"{Name}に数字が定義されていません : {line}"));
                            return;
                        }
                        if (!NumberRange?.Invoke(value) ?? false)
                        {
                            logs.Add(new DecodeLog(State.Warning, $"{Name}に規定外の数字が定義されています : {line}"));
                            return;
                        }
                        prop.SetValue(model, value);
                        break;
                    case ValueType.Text:
                        prop.SetValue(model, arg);
                        break;
                    case ValueType.Path:
                        prop.SetValue(model, arg.Replace('\\', '/'));
                        break;
                    case ValueType.BaseNum:
                        if (model.Base == 62 ? !Utility.TryParseInt62(arg[0..2], out var x) : !Utility.TryParseInt36(arg[0..2], out x))
                        {
                            logs.Add(new DecodeLog(State.Warning, $"{Name}に数字が定義されていません : {line}"));
                            return;
                        }
                        prop.SetValue(model, x);
                        break;
                    default:
                        break;
                }
                AppendProcess?.Invoke(model);
            }
        }

        enum ValueType { Text, Number, Path, Sequence, BaseNum }
    }
}
