using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BmsParser
{
    class LineProcessor
    {
        //        static CommandWord[] command =
        //        {
        //            new CommandWord("#PLAYER", ValueType.Number, nameof(BmsModel.Player)),
        //            new CommandWord("#GENRE", ValueType.Text, nameof(BmsModel.Genre)),
        //            new CommandWord("#TITLE", ValueType.Text, nameof(BmsModel.Title)),
        //            new CommandWord("#SUBTITLE", ValueType.Text, nameof(BmsModel.Subtitle)),
        //            new CommandWord("#ARTIST", ValueType.Text, nameof(BmsModel.Artist)),
        //            new CommandWord("#SUBARTIST", ValueType.Text, nameof(BmsModel.SubArtist)),
        //            new CommandWord("#PLAYLEVEL", ValueType.Text, nameof(BmsModel.PlayLevel)),
        //            new CommandWord("#RANK", ValueType.Number, nameof(BmsModel.JudgeRank))
        //                { NumberRange = rank => 0 <= rank && rank <= 4, AppendProcess = model => model.JudgeRankType = JudgeRankType.BmsRank },
        //            new CommandWord("#DEFEXRANK", ValueType.Number, nameof(BmsModel.JudgeRank))
        //                { NumberRange = rank => rank > 0, AppendProcess = model => model.JudgeRankType = JudgeRankType.BmsDefEXRank },
        //            new CommandWord("#TOTAL", ValueType.Number, nameof(BmsModel.Total))
        //                { NumberRange = total => total > 0, AppendProcess = model => model.TotalType = TotalType.Bms },
        //            new CommandWord("#VOLWAV", ValueType.Number, nameof(BmsModel.VolWav)),
        //            new CommandWord("#STAGEFILE", ValueType.Path, nameof(BmsModel.StageFile)),
        //            new CommandWord("#BACKBMP", ValueType.Path, nameof(BmsModel.BackBmp)),
        //            new CommandWord("#PREVIEW", ValueType.Path, nameof(BmsModel.Preview)),
        //            new CommandWord("#LNOBJ", ValueType.Number, nameof(BmsModel.LNObj)),
        //            new CommandWord("#LNMODE", ValueType.Number, nameof(BmsModel.LNMode))
        //                { NumberRange = lnMode => Enum.IsDefined(typeof(LNMode), lnMode) },
        //            new CommandWord("#DIFFICULTY", ValueType.Number, nameof(BmsModel.Difficulty)),
        //            new CommandWord("#BANNER", ValueType.Path, nameof(BmsModel.Banner)),
        //            new CommandWord("#BPM", ValueType.Other, nameof(BmsModel.Bpm))
        //        }

        readonly SequenceWord bpm = new("#BPM", ValueType.Number) { NumberRange = bpm => bpm > 0 };
        readonly SequenceWord bmp = new("#WAV", ValueType.Path);
        //static SequenceWord[] sequences =
        //[
        //new("#BPM", ValueType.Number) { NumberRange = bpm => bpm > 0 },
        //            new SequenceWord("#WAV", ValueType.Path),
        //            new SequenceWord("#BMP", ValueType.Path),
        //            new SequenceWord("#STOP", ValueType.Number) { NumberRange = stop => stop >= 0 },
        //            new SequenceWord("#SCROLL", ValueType.Number)
        //];

        public ConcurrentDictionary<int, double> BpmTable { get; } = new();

        public ConcurrentDictionary<int, double> StopTable { get; } = new();

        public ConcurrentDictionary<int, double> ScrollTable { get; } = new();

        public ConcurrentDictionary<int, ConcurrentBag<string>> BarTable { get; } = new();

        public bool Process(BmsModel model, string line, List<DecodeLog> logs)
        {
            if (line.Length <= 1)
                return true;

            var c = line[1];
            var @base = model.Base;
            if ('0' <= c && c <= '9' && line.Length > 6)
            {
                // 楽譜
                if (!int.TryParse(line[1..4], out var barNum))
                {
                    logs.Add(new DecodeLog(State.Warning, "小節に数字が定義されていません : " + line));
                    return true;
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
                return true;
            }

            if (bpm.IsMatch(line))
            {
                if (line[4] == ' ')
                {
                    if (!double.TryParse(line[5..].Trim(), out var bpm))
                    {
                        logs.Add(new DecodeLog(State.Warning, "#BPMに数字が定義されていません : " + line));
                        return true;
                    }
                    if (bpm > 0)
                    {
                        model.Bpm = bpm;
                    }
                    else
                    {
                        logs.Add(new DecodeLog(State.Warning, "#negative BPMはサポートされていません : " + line));
                    }
                    return true;
                }
                else
                {
                    if (!double.TryParse(line[7..].Trim(), out var bpm))
                    {
                        logs.Add(new DecodeLog(State.Warning, "#BPMxxに数字が定義されていません : " + line));
                        return true;
                    }
                    if (bpm <= 0)
                    {
                        logs.Add(new DecodeLog(State.Warning, "#negative BPMはサポートされていません : " + line));
                        return true;
                    }
                    if (model.Base == 62 ? Utility.TryParseInt62(line[4..6], out var seq) : Utility.TryParseInt36(line[4..6], out seq))
                    {
                        BpmTable.Put(seq, bpm);
                    }
                    else
                    {
                        logs.Add(new DecodeLog(State.Warning, "#BPMxxに数字が定義されていません : " + line));
                    }
                    return true;
                }
            }

            //            var top = line.Split(' ')[0].Trim().ToUpper();
            //            var seq = sequences.FirstOrDefault(s => top.StartsWith(s.Name));
            //            if (seq != default && top != seq.Name)
            //            {
            //                seq.Process(top, line, model, logs, this);
            //                return;
            //            }

            //            var word = command.FirstOrDefault(w => w.Name == top);
            //            if (word != default)
            //            {
            //                word.Process(line, model, logs);
            //                return;
            //            }

            //            if (line[0] == '%' || line[0] == '@')
            //            {
            //                if (top.Length == line.Trim().Length)
            //                    return;
            //                if (model.Values.ContainsKey(top[1..]))
            //                    return;
            //                model.Values.Put(top[1..], line[(top.Length + 1)..]);
            //                return;
            //            }
            return false;
        }

        abstract record Keyword(string Name)
        {
            public bool IsMatch(string line)
            {
                var top = line[0..Name.Length];
                return top.Equals(Name, StringComparison.CurrentCultureIgnoreCase);
            }
        }

        record SequenceWord(string Name, ValueType ValueType) : Keyword(Name)
        {
            public Func<double, bool>? NumberRange { get; init; }

            public void Process(string line, BmsModel model, List<DecodeLog> logs, LineProcessor processor)
            {
                if (line.Length < Name.Length + 4 || model.Base == 62 ? !Utility.TryParseInt62(line[4..6], out var seq) : !Utility.TryParseInt36(line[4..6], out seq))
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
                        var textTable = Name switch
                        {
                            "#BMP" => model.BgaList,
                            "#WAV" => model.WavList,
                            _ => throw new NotSupportedException()
                        };
                        textTable[seq] = arg.Replace('\\', '/');
                        return;
                }
            }
        }

        //        record CommandWord(string Name, ValueType ValueType, string PropertyName)
        //        {
        //            public Func<int, bool> NumberRange { get; init; }

        //            public Action<BmsModel> AppendProcess { get; init; }

        //            public void Process(string line, BmsModel model, List<DecodeLog> logs)
        //            {
        //                var arg = line[Name.Length..].Trim();
        //                var prop = typeof(BmsModel).GetProperty(PropertyName);
        //                switch (ValueType)
        //                {
        //                    case ValueType.Number:
        //                        if (!int.TryParse(arg, out var value))
        //                        {
        //                            logs.Add(new DecodeLog(State.Warning, $"{Name}に数字が定義されていません : {line}"));
        //                            return;
        //                        }
        //                        if (!NumberRange?.Invoke(value) ?? false)
        //                        {
        //                            logs.Add(new DecodeLog(State.Warning, $"{Name}に無効な数字が定義されています : {line}"));
        //                            return;
        //                        }
        //                        prop.SetValue(model, value);
        //                        break;
        //                    case ValueType.Text:
        //                        prop.SetValue(model, arg);
        //                        break;
        //                    case ValueType.Path:
        //                        prop.SetValue(model, arg.Replace('\\', '/'));
        //                        break;
        //                    case ValueType.Other:
        //                        if (Name == "#BPM")
        //                        {
        //                            if (!double.TryParse(arg, out var bpm))
        //                            {
        //                                logs.Add(new DecodeLog(State.Warning, $"{Name}に数字が定義されていません : {line}"));
        //                                return;
        //                            }
        //                            if (bpm <= 0)
        //                            {
        //                                logs.Add(new DecodeLog(State.Warning, $"#negative BPMはサポートされていません : {line}"));
        //                                return;
        //                            }
        //                            prop.SetValue(model, bpm);
        //                        }
        //                        break;
        //                    default:
        //                        break;
        //                }
        //                AppendProcess?.Invoke(model);
        //            }
        //        }

        enum ValueType { Text, Number, Path, Sequence, Other }
    }
}
