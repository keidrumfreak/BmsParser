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
                            if (!double.TryParse(arg, out var bpm) || !TryParseInt36(line, 4, out var seq))
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
                        if (line.Length < 8 || !TryParseInt36(line, 4, out var seq))
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
                        if (line.Length < 8 || !TryParseInt36(line, 4, out var seq))
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
                        if (!double.TryParse(line[8..].Trim(), out var stop) || !TryParseInt36(line, 5, out var seq))
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
                        if (!double.TryParse(line[10..].Trim(), out var scroll) || !TryParseInt36(line, 7, out var seq))
                        {
                            logs.Add(new DecodeLog(State.Warning, $"#STOPxxに数字が定義されていません : {line}"));
                            continue;
                        }
                        scrollTable.Add(seq, scroll);
                    }
                    else
                    {
                        var processor = new LineProcessor();
                        processor.Process(model, line, logs);
                    }
                }
                else if (line[0] == '%' || line[0] == '@')
                {
                    var processor = new LineProcessor();
                    processor.Process(model, line, logs);
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
    }
}
