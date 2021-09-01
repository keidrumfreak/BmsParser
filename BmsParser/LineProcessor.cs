using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BmsParser
{
    class LineProcessor
    {
        static CommandWord[] command =
        {
            new CommandWord("#PLAYER", ValueType.Number, nameof(BmsModel.Player)),
            new CommandWord("#GENRE", ValueType.Text, nameof(BmsModel.Genre)),
            new CommandWord("#TITLE", ValueType.Text, nameof(BmsModel.Title)),
            new CommandWord("#SUBTITLE", ValueType.Text, nameof(BmsModel.SubTitle)),
            new CommandWord("#ARTIST", ValueType.Text, nameof(BmsModel.Artist)),
            new CommandWord("#SUBARTIST", ValueType.Text, nameof(BmsModel.SubArtist)),
            new CommandWord("#PLAYLEVEL", ValueType.Text, nameof(BmsModel.PlayLevel)),
            new CommandWord("#RANK", ValueType.Number, nameof(BmsModel.JudgeRank))
                { NumberRange = rank => 0 <= rank && rank <= 4, AppendProcess = model => model.JudgeRankType = JudgeRankType.BmsRank },
            new CommandWord("#DEFEXRANK", ValueType.Number, nameof(BmsModel.JudgeRank))
                { NumberRange = rank => rank > 0, AppendProcess = model => model.JudgeRankType = JudgeRankType.BmsDefEXRank },
            new CommandWord("#TOTAL", ValueType.Number, nameof(BmsModel.Total))
                { NumberRange = total => total > 0, AppendProcess = model => model.TotalType = TotalType.Bms },
            new CommandWord("#VOLWAV", ValueType.Number, nameof(BmsModel.VolWav)),
            new CommandWord("#STAGEFILE", ValueType.Path, nameof(BmsModel.StageFile)),
            new CommandWord("#BACKBMP", ValueType.Path, nameof(BmsModel.BackBmp)),
            new CommandWord("#PREVIEW", ValueType.Path, nameof(BmsModel.Preview)),
            new CommandWord("#LNOBJ", ValueType.Number, nameof(BmsModel.LNObj)),
            new CommandWord("#LNMODE", ValueType.Number, nameof(BmsModel.LNMode))
                { NumberRange = lnMode => Enum.IsDefined(typeof(LNMode), lnMode) },
            new CommandWord("#DIFFICULTY", ValueType.Number, nameof(BmsModel.Difficulty)),
            new CommandWord("#BANNER", ValueType.Path, nameof(BmsModel.Banner)),
            new CommandWord("#BPM", ValueType.Other, nameof(BmsModel.Bpm))
        };

        static SequenceWord[] sequences =
        {
            new SequenceWord("#BPM", ValueType.Number) { NumberRange = bpm => bpm > 0 },
        };

        Dictionary<string, Dictionary<int, double>> numTables = new()
        {
            { "#BPM", new Dictionary<int, double>() }
        };

        public Dictionary<int, double> BpmTable => numTables["#BPM"];

        public void Process(BmsModel model, string line, List<DecodeLog> logs)
        {
            if (line.Length <= 1)
                return;

            var top = line.Split(' ')[0].Trim();
            var seq = sequences.FirstOrDefault(s => top.StartsWith(s.Name));
            if (seq != default && top != seq.Name)
            {
                seq.Process(top, line, model, logs, numTables);
                return;
            }

            var word = command.FirstOrDefault(w => w.Name == top);
            if (word != default)
            {
                word.Process(line, model, logs);
                return;
            }

            if (line[0] == '%' || line[0] == '@')
            {
                if (top.Length == line.Trim().Length)
                    return;
                model.Values.Add(top[1..], line[(top.Length + 1)..]);
                return;
            }
        }

        record SequenceWord(string Name, ValueType ValueType)
        {
            public Func<double, bool> NumberRange { get; init; }

            public void Process(string top, string line, BmsModel model, List<DecodeLog> logs, Dictionary<string, Dictionary<int, double>> numTables)
            {
                if (top.Length != Name.Length + 2 || line.Length < Name.Length + 4 || !ChartDecoder.TryParseInt36(top[^2..], 0, out var seq))
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
                        if (!NumberRange?.Invoke(value) ?? false)
                        {
                            logs.Add(new DecodeLog(State.Warning, $"#negative {Name[1..]}はサポートされていません : {line}"));
                            if (Name == "#BPM") return;
                        }
                        if (!numTables.TryGetValue(Name, out var numTable))
                        {
                            numTables.Add(Name, new Dictionary<int, double>());
                            numTable = numTables[Name];
                        }
                        if (numTable.ContainsKey(seq))
                        {
                            numTable[seq] = value;
                        }
                        else
                        {
                            numTable.Add(seq, value);
                        }
                        return;
                }
            }
        }

        record CommandWord(string Name, ValueType ValueType, string PropertyName)
        {
            public Func<int, bool> NumberRange { get; init; }

            public Action<BmsModel> AppendProcess { get; init; }

            public void Process(string line, BmsModel model, List<DecodeLog> logs)
            {
                var arg = line[Name.Length..].Trim();
                var prop = typeof(BmsModel).GetProperty(PropertyName);
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
                            logs.Add(new DecodeLog(State.Warning, $"{Name}に無効な数字が定義されています : {line}"));
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
                    case ValueType.Other:
                        if (Name == "#BPM")
                        {
                            if (!double.TryParse(arg, out var bpm))
                            {
                                logs.Add(new DecodeLog(State.Warning, $"{Name}に数字が定義されていません : {line}"));
                                return;
                            }
                            if (bpm <= 0)
                            {
                                logs.Add(new DecodeLog(State.Warning, $"#negative BPMはサポートされていません : {line}"));
                                return;
                            }
                            prop.SetValue(model, bpm);
                        }
                        break;
                    default:
                        break;
                }
                AppendProcess?.Invoke(model);
            }
        }

        enum ValueType { Text, Number, Path, Sequence, Other }
    }
}
