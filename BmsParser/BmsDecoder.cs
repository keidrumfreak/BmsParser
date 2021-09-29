using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BmsParser
{
    class BmsDecoder : ChartDecoder
    {
        public BmsDecoder(LNType lnType = LNType.LongNote)
        {
            LNType = lnType;
        }

        new public BmsModel Decode(string path)
        {
            var model = decode(path, path.EndsWith(".pms"), null, File.ReadAllBytes(path));
            return model;
        }

        public BmsModel Decode(string path, byte[] bin)
        {
            var model = decode(path, path.EndsWith(".pms"), null, bin);
            return model;
        }

        public override BmsModel Decode(ChartInformation info)
        {
            LNType = info.LNType;
            return decode(info.Path, info.Path.EndsWith(".pms"), info.SelectedRandoms, File.ReadAllBytes(info.Path));
        }

        private BmsModel decode(string path, bool isPms, int[] selectedRandom, byte[] bin)
        {
            string input;
            using (var mem = new MemoryStream(bin))
            using (var reader = new StreamReader(mem))
                input = new StreamReader(new MemoryStream(bin), Encoding.GetEncoding("shift-jis"), true).ReadToEnd();
            var fileLines = input.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            logs.Clear();
            var time = DateTime.Now;
            var model = new BmsModel();
            model.Mode = isPms ? Mode.Popn9K : Mode.Beat5K;

            var randoms = new LinkedList<int>();
            var srandoms = new LinkedList<int>();
            var crandoms = new LinkedList<int>();
            var skip = new LinkedList<bool>();
            var processor = new LineProcessor();
            var tasks = new List<Task>();

            foreach (var line in fileLines.Where(l => l.Length > 1))
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
                        randoms.AddLast(r);
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

                    tasks.Add(Task.Run(() => processor.Process(model, line, logs)));
                    processor.Process(model, line, logs);
                }
                else if (line[0] == '%' || line[0] == '@')
                {
                    tasks.Add(Task.Run(() => processor.Process(model, line, logs)));
                    processor.Process(model, line, logs);
                }
            }

            Task.WaitAll(tasks.ToArray());

            if (!processor.BarTable.Any())
                return null;

            var prev = default(Section);
            var sections = new List<Section>();
            for (var i = 0; i <= processor.BarTable.Keys.Max(); i++)
            {
                var section = new Section(model, prev, processor, i, logs);
                sections.Add(section);
                prev = section;
            }

            var timeLines = new SortedDictionary<double, TimeLineCache>();
            var lnList = new List<LongNote>[model.Mode.Key];
            var lnEndStatus = new LongNote[model.Mode.Key];
            var baseTL = new TimeLine(0, 0, model.Mode.Key);
            baseTL.Bpm = model.Bpm;
            timeLines.Add(0.0, new TimeLineCache(0.0, baseTL));
            foreach (var section in sections)
            {
                section.MakeTimeLine(model.WavList, model.BgaList, timeLines, lnList, lnEndStatus);
            }

            var tl = timeLines.Values.Select(t => t.TimeLine).ToArray();
            model.TimeLines = tl;
            if (tl[0].Bpm == 0)
            {
                logs.Add(new DecodeLog(State.Warning, "開始BPMが定義されていないため、BMS解析に失敗しました"));
                return null;
            }

            foreach (var i in Enumerable.Range(0, lnEndStatus.Length).Where(j => lnEndStatus[j] != null))
            {
                logs.Add(new DecodeLog(State.Warning, $"曲の終端までにLN終端定義されていないLNがあります。lane:{i + 1}"));
                if (lnEndStatus[i].Section != double.MinValue)
                    timeLines[lnEndStatus[i].Section].TimeLine.SetNote(i, null);
            }

            if (model.TotalType != TotalType.Bms)
            {
                logs.Add(new DecodeLog(State.Warning, "TOTALが未定義です"));
            }
            if (model.Total <= 60.0)
            {
                logs.Add(new DecodeLog(State.Warning, "TOTAL値が少なすぎます"));
            }
            if (tl.Length > 0)
            {
                if (tl[tl.Length - 1].Time >= model.LastTime + 30000)
                {
                    logs.Add(new DecodeLog(State.Warning, "最後のノート定義から30秒以上の余白があります"));
                }
            }
            if (model.Player > 1 && (model.Mode == Mode.Beat5K || model.Mode == Mode.Beat7K))
            {
                logs.Add(new DecodeLog(State.Warning, "#PLAYER定義が2以上にもかかわらず2P側のノーツ定義が一切ありません"));
            }
            if (model.Player == 1 && (model.Mode == Mode.Beat10K || model.Mode == Mode.Beat14K))
            {
                logs.Add(new DecodeLog(State.Warning, "#PLAYER定義が1にもかかわらず2P側のノーツ定義が存在します"));
            }

            model.MD5 = getMd5Hash(bin);
            model.Sha256 = getSha256Hash(bin);

            if (selectedRandom == null)
            {
                selectedRandom = srandoms.ToArray();
            }

            model.ChartInformation = new ChartInformation(path, LNType, selectedRandom);

            return model;
        }

        private string getMd5Hash(byte[] input)
        {
            var md5 = MD5.Create();
            var arr = md5.ComputeHash(input);
            return BitConverter.ToString(arr).ToLower().Replace("-", "");
        }

        private string getSha256Hash(byte[] input)
        {
            var sha256 = SHA256.Create();
            var arr = sha256.ComputeHash(input);
            return BitConverter.ToString(arr).ToLower().Replace("-", "");
        }
    }
}
