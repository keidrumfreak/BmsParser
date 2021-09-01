using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BmsParser
{
    public class BmsDecoder : ChartDecoder
    {
        public BmsDecoder(LNType lnType = LNType.LongNote)
        {
            LNType = lnType;
        }

        public override BmsModel Decode(ChartInformation info)
        {
            throw new NotImplementedException();
        }

        private BmsModel decode(string path, byte[] data, bool isPms, int[] selectedRandom)
        {
            logs.Clear();
            var time = DateTime.Now;
            var model = new BmsModel();
            var scrollTable = new Dictionary<int, double>();
            var stopTable = new Dictionary<int, double>();
            var bpmTable = new Dictionary<int, double>();
            var wm = new int[36 * 36];
            Array.Fill(wm, -2);
            var wavList = new List<string>();

            var bm = new int[36 * 36];
            Array.Fill(bm, -2);
            var bgaList = new List<string>();

            model.Mode = isPms ? Mode.Popn9K : Mode.Beat5K;

            var randoms = new LinkedList<int>();
            var srandoms = new LinkedList<int>();
            var crandoms = new LinkedList<int>();
            var skip = new LinkedList<bool>();
            var maxsec = 0;
            var lines = new Dictionary<int, List<string>>();

            foreach (var line in File.ReadAllLines(path).Where(l => l.Length > 1))
            {
                if (line[0] == '#')
                {
                    if (line.StartsWith("#RANDOM"))
                    {
                        // RONDOM制御
                        if (!int.TryParse(line[8..].Trim(), out var r))
                        {
                            logs.Add(new DecodeLog(State.Warning, "#RANDOMに数字が定義されていません"));
                            continue;
                        }
                        if (selectedRandom == null)
                        {
                            crandoms.AddLast((int)(new Random().NextDouble() * r + 1));
                            srandoms.AddLast(crandoms.Last.Value);
                        }
                        else
                        {
                            crandoms.AddLast(selectedRandom[randoms.Count - 1]);
                        }
                    }
                    else if (line.StartsWith("#IF"))
                    {
                        // RANDOM分岐
                        if (!crandoms.Any())
                        {
                            logs.Add(new DecodeLog(State.Warning, "#IFに対応する#RANDOMが定義されていません"));
                            continue;
                        }

                        if (!int.TryParse(line[4..].Trim(), out var r))
                        {
                            logs.Add(new DecodeLog(State.Warning, "#IFに数字が定義されていません"));
                            continue;
                        }

                        skip.AddLast(crandoms.Last.Value != r);
                    }
                    else if (line.StartsWith("#ENDIF"))
                    {
                        if (!skip.Any())
                        {
                            logs.Add(new DecodeLog(State.Warning, $"ENDIFに対応するIFが存在しません : {line}"));
                            continue;
                        }

                        skip.RemoveLast();
                    }
                    else if (line.StartsWith("#ENDRANDOM"))
                    {
                        if (!crandoms.Any())
                        {
                            logs.Add(new DecodeLog(State.Warning, $"ENDRANDOMに対応するRANDOMが存在しません : {line}"));
                            continue;
                        }

                        crandoms.RemoveLast();
                    }
                    else if (skip.Any() && skip.Last.Value)
                    {
                        continue;
                    }

                    if ('0' <= line[0] && line[0] <= '9')
                    {
                        // 楽譜
                        if (!int.TryParse(line.Substring(1, 3), out var barIndex))
                        {
                            logs.Add(new DecodeLog(State.Warning, $"小節に数字が定義されていません : {line}"));
                            continue;
                        }

                        if (!lines.TryGetValue(barIndex, out var l))
                        {
                            l = new List<string>();
                        }

                        l.Add(line);
                        lines[barIndex] = l;

                        maxsec = maxsec > barIndex ? maxsec : barIndex;
                    }
                    else if (line.StartsWith("#BPM"))
                    {
                        // BPM
                        if (line[4] == ' ')
                        {
                            var arg = line[5..].Trim();
                            if (!double.TryParse(arg, out var bpm))
                            {
                                logs.Add(new DecodeLog(State.Warning, $"#BPMに数字が定義されていません : {line}"));
                                continue;
                            }
                            if (bpm <= 0)
                            {
                                logs.Add(new DecodeLog(State.Warning, $"#negative BPMはサポートされていません : {line}"));
                                continue;
                            }
                            model.Bpm = bpm;
                        }
                        else
                        {
                            var arg = line[7..].Trim();
                            if (!double.TryParse(arg, out var bpm) || !TryParseInt36(line.Substring(4, 2), out var seq))
                            {
                                logs.Add(new DecodeLog(State.Warning, $"#BPMxxに数字が定義されていません : {line}"));
                                continue;
                            }
                            if (bpm <= 0)
                            {
                                logs.Add(new DecodeLog(State.Warning, $"#negative BPMはサポートされていません : {line}"));
                                continue;
                            }
                            bpmTable.Add(seq, bpm);
                        }
                    }
                    else if (line.StartsWith("#WAV"))
                    {
                        // 音源
                        if (line.Length < 8 || !TryParseInt36(line.Substring(4, 2), out var seq))
                        {
                            logs.Add(new DecodeLog(State.Warning, $"#WAVxxは不十分な定義です : {line}"));
                            continue;
                        }

                        var fileName = line[7..].Trim().Replace('\\', '/');

                        wm[seq] = wavList.Count;
                        wavList.Add(fileName);
                    }
                    else if (line.StartsWith("#BMP"))
                    {
                        // BGAファイル
                        if (line.Length < 8 || !TryParseInt36(line.Substring(4, 2), out var seq))
                        {
                            logs.Add(new DecodeLog(State.Warning, $"#BMPxxは不十分な定義です : {line}"));
                            continue;
                        }

                        var fileName = line[7..].Trim().Replace('\\', '/');

                        bm[seq] = bgaList.Count;
                        bgaList.Add(fileName);
                    }
                    else if (line.StartsWith("#STOP"))
                    {
                        if (line.Length < 9)
                        {
                            logs.Add(new DecodeLog(State.Warning, $"#STOPxxは不十分な定義です : {line}"));
                            continue;
                        }
                        if (!double.TryParse(line[8..].Trim(), out var stop) || !TryParseInt36(line.Substring(5, 2), out var seq))
                        {
                            logs.Add(new DecodeLog(State.Warning, $"#STOPxxに数字が定義されていません : {line}"));
                            continue;
                        }
                        stop /= 192;
                        if (stop < 0)
                        {
                            logs.Add(new DecodeLog(State.Warning, $"#negative STOPはサポートされていません : {line}"));
                            stop = Math.Abs(stop);
                        }
                        stopTable.Add(seq, stop);
                    }
                    else if (line.StartsWith("#SCROLL"))
                    {
                        if (line.Length < 11)
                        {
                            logs.Add(new DecodeLog(State.Warning, $"#SCROLLxxは不十分な定義です : {line}"));
                            continue;
                        }
                        if (!double.TryParse(line[10..].Trim(), out var scroll) || !TryParseInt36(line.Substring(7, 2), out var seq))
                        {
                            logs.Add(new DecodeLog(State.Warning, $"#STOPxxに数字が定義されていません : {line}"));
                            continue;
                        }
                        scrollTable.Add(seq, scroll);
                    }
                    else
                    {
                        var word = CommandWord.Words.FirstOrDefault(w => line.StartsWith(w.Name) && line.Length > w.Name.Length + 2);
                        if (word == default)
                            continue;
                        var log = word.Func(model, line[(word.Name.Length + 2)..].Trim());
                        if (log != null)
                            logs.Add(log);
                    }
                }
                else if (line[0] == '%' || line[0] == '@')
                {
                    var index = line.IndexOf(' ');
                    if (index > 0 && line.Length > index + 1)
                    {
                        model.Values.Add(line.Substring(1, index), line[(index + 1)..]);
                    }
                }
            }

            model.WavList = wavList.ToArray();
            model.BgaList = bgaList.ToArray();

            var prev = default(Section);
            var sections = new List<Section>();
            for (var i = 0; i <= maxsec; i++)
            {
                var section = new Section(model, prev, lines[i] ?? new List<string>(), bpmTable, stopTable, scrollTable, logs);
                sections.Add(section);
                prev = section;
            }

            var baseTL = new TimeLine(0, 0, model.Mode.Key);

            return model;
        }

        public record CommandWord(string Name, Func<BmsModel, string, DecodeLog> Func)
        {
            public static readonly CommandWord Player = new("#PLAYER", (model, arg) =>
            {
                if (!int.TryParse(arg, out var player))
                    return new DecodeLog(State.Warning, $"#PLAYERに数字が定義されていません");
                model.Player = player;
                return null;
            });

            public static readonly CommandWord Genre = new("#GENRE", (model, arg) => { model.Genre = arg; return null; });

            public static readonly CommandWord Title = new("#TITLE", (model, arg) => { model.Title = arg; return null; });

            public static readonly CommandWord SubTitle = new("#SUBTITLE", (model, arg) => { model.SubTitle = arg; return null; });

            public static readonly CommandWord Artist = new("#ARTIST", (model, arg) => { model.Artist = arg; return null; });

            public static readonly CommandWord SubArtist = new("#SUBARTIST", (model, arg) => { model.SubArtist = arg; return null; });

            public static readonly CommandWord PlayLevel = new("#PLAYLEVEL", (model, arg) => { model.PlayLevel = arg; return null; });

            public static readonly CommandWord Rank = new("#RANK", (model, arg) =>
            {
                if (!int.TryParse(arg, out var rank))
                    return new DecodeLog(State.Warning, $"#RANKに数字が定義されていません");
                if (rank < 0 || 4 < rank)
                    return new DecodeLog(State.Warning, $"#RANKに規定外の数字が定義されています : {rank}");
                model.JudgeRank = rank;
                model.JudgeRankType = JudgeRankType.BmsRank;
                return null;
            });

            public static readonly CommandWord DefEXRank = new("#DEFEXRANK", (model, arg) =>
            {
                if (!int.TryParse(arg, out var rank))
                    return new DecodeLog(State.Warning, $"#DEFEXRANKに数字が定義されていません");
                if (rank < 1)
                    return new DecodeLog(State.Warning, $"#DEFEXRANK 1未満はサポートしていません{rank}");
                model.JudgeRank = rank;
                model.JudgeRankType = JudgeRankType.BmsDefEXRank;
                return null;
            });

            public static readonly CommandWord Total = new("#TOTAL", (model, arg) =>
            {
                if (!int.TryParse(arg, out var total))
                    return new DecodeLog(State.Warning, $"#TOTALに数字が定義されていません");
                if (total < 1)
                    return new DecodeLog(State.Warning, $"#TOTALが0以下です");
                model.JudgeRank = total;
                model.JudgeRankType = JudgeRankType.BmsDefEXRank;
                return null;
            });

            public static readonly CommandWord VolWav = new("#VOLWAV", (model, arg) =>
            {
                if (!int.TryParse(arg, out var total))
                    return new DecodeLog(State.Warning, $"#VOLWAVに数字が定義されていません");
                model.Total = total;
                model.TotalType = TotalType.Bms;
                return null;
            });

            public static readonly CommandWord StageFile = new("#STAGEFILE", (model, arg) => { model.StageFile = arg.Replace('\\', '/'); return null; });

            public static readonly CommandWord BackBmp = new("#BACKBMP", (model, arg) => { model.BackBmp = arg.Replace('\\', '/'); return null; });

            public static readonly CommandWord Preview = new("#PREVIEW", (model, arg) => { model.Preview = arg.Replace('\\', '/'); return null; });

            public static readonly CommandWord LNObj = new("#LNOBJ", (model, arg) =>
            {
                if (!int.TryParse(arg, out var lnObj))
                    return new DecodeLog(State.Warning, $"#LNOBJに数字が定義されていません");
                model.LNObj = lnObj;
                return null;
            });

            public static readonly CommandWord LNMode = new("#LNMODE", (model, arg) =>
            {
                if (!int.TryParse(arg, out var lnMode))
                    return new DecodeLog(State.Warning, $"#LNMODEに数字が定義されていません");
                if (!Enum.IsDefined(typeof(LNMode), lnMode))
                    return new DecodeLog(State.Warning, $"#LNMODEに無効な数字が定義されています");
                model.LNMode = (LNMode)lnMode;
                return null;
            });

            public static readonly CommandWord Difficulty = new("#DIFFICULTY", (model, arg) =>
            {
                if (!int.TryParse(arg, out var difficulty))
                    return new DecodeLog(State.Warning, $"#DIFFICULTYに数字が定義されていません");
                model.Difficulty = (Difficulty)difficulty;
                return null;
            });

            public static readonly CommandWord Banner = new("#BANNER", (model, arg) => { model.Banner = arg.Replace('\\', '/'); return null; });

            public static readonly CommandWord Comment = new("#COMMENT", (model, arg) => { return null; }); // TODO: 未実装

            public static readonly CommandWord[] Words = new[]
            {
                Player,
                Genre,
                Title,
                SubTitle,
                Artist,
                SubArtist,
                PlayLevel,
                Rank,
                DefEXRank,
                Total,
                VolWav,
                StageFile,
                BackBmp,
                Preview,
                LNObj,
                LNMode,
                Difficulty,
                Banner,
                Comment
            };
        }
    }
}
