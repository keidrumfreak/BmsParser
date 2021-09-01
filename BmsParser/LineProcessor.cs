using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BmsParser
{
    class LineProcessor
    {
        static CommandWord[] words =
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
            new CommandWord("#BANNER", ValueType.Path, nameof(BmsModel.Banner))
        };

        public void Process(BmsModel model, string line, List<DecodeLog> logs)
        {
            var top = line.Split(' ')[0].Trim();
            var word = words.FirstOrDefault(w => w.Name == top);
            if (word != default)
            {
                word.Process(line, model, logs);
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
                            logs.Add(new DecodeLog(State.Warning, $"{Name}に数字が定義されていません"));
                            return;
                        }
                        if (!NumberRange?.Invoke(value) ?? false)
                        {
                            logs.Add(new DecodeLog(State.Warning, $"{Name}に無効な数字が定義されています"));
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
                    default:
                        break;
                }
                AppendProcess?.Invoke(model);
            }
        }

        enum ValueType { Text, Number, Path, Other }
    }
}
