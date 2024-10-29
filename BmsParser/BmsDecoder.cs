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

        new public BmsModel? Decode(string path)
        {
            var model = decode(path, File.ReadAllBytes(path), path.EndsWith(".pms"), null);
            return model;
        }

        public BmsModel? Decode(string path, byte[] bin)
        {
            var model = decode(path, bin, path.EndsWith(".pms"), null);
            return model;
        }

        public override BmsModel? Decode(ChartInformation info)
        {
            try
            {
                this.LNType = info.LNType;
                return decode(info.Path, File.ReadAllBytes(info.Path), info.Path.ToString().ToLower().EndsWith(".pms"), info.SelectedRandoms);
            }
            catch (IOException)
            {
                logs.Add(new DecodeLog(State.Error, "BMSファイルが見つかりません"));
                //Logger.getGlobal().severe("BMSファイル解析中の例外 : " + e.getClass().getName() + " - " + e.getMessage());
            }
            return null;
        }

        //protected BmsModel? Decode(string f)
        //{
        //    //Logger.getGlobal().fine("BMSファイル解析開始 :" + f);
        //    try
        //    {
        //        BmsModel model = this.decode(f, File.ReadAllBytes(f), f.ToLower().EndsWith(".pms"), null);
        //        if (model == null)
        //        {
        //            return null;
        //        }
        //        //Logger.getGlobal().fine("BMSファイル解析完了 :" + f.toString() + " - TimeLine数:" + model.getAllTimes().Length);
        //        return model;
        //    }
        //    catch (IOException e)
        //    {
        //        log.Add(new DecodeLog(State.Error, "BMSファイルが見つかりません"));
        //        //Logger.getGlobal().severe("BMSファイル解析中の例外 : " + e.getClass().getName() + " - " + e.getMessage());
        //    }
        //    return null;
        //}


        private static readonly string[] separator = ["\r\n", "\n", "\r"];

        ///**
        // * 指定したBMSファイルをモデルにデコードする
        // *
        // * @param data
        // * @return
        // */
        //public BmsModel? Decode(byte[] data, bool ispms, int[] random)
        //{
        //    return this.decode(null, data, ispms, random);
        //}

        private BmsModel? decode(string path, byte[] data, bool ispms, int[]? selectedRandom)
        {
            logs.Clear();
            var time = DateTime.Now.Ticks;
            var model = new BmsModel(ispms ? Mode.Popn9K : Mode.Beat5K);

            // BMS読み込み、ハッシュ値取得
            using var mem = new MemoryStream(data);
            using var reader = new StreamReader(mem, Encoding.GetEncoding("shift-jis"), true);
            var input = reader.ReadToEnd();
            var fileLines = input.Split(separator, StringSplitOptions.None);
            //model.Mode = ispms ? Mode.Popn9K : Mode.Beat5K;
            // Logger.getGlobal().info(
            // "BMSデータ読み込み時間(ms) :" + (System.currentTimeMillis() - time));

            var randoms = new LinkedList<int>();
            var srandoms = new LinkedList<int>();
            var crandoms = new LinkedList<int>();
            var skip = new LinkedList<bool>();
            var processor = new LineProcessor();

            foreach (var line in fileLines.Where(l => l.Length > 1))
            {
                if (line[0] == '#')
                {
                    // RANDOM制御系
                    if (line.StartsWith("#RANDOM", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!int.TryParse(line[8..].Trim(), out var r))
                        {
                            logs.Add(new DecodeLog(State.Warning, "#RANDOMに数字が定義されていません"));
                            continue;
                        }
                        randoms.AddLast(r);
                        if (selectedRandom == null)
                        {
                            crandoms.AddLast((int)(new Random().NextDouble() * r) + 1);
                            srandoms.AddLast(crandoms.Last!);
                        }
                        else
                        {
                            crandoms.AddLast(selectedRandom[randoms.Count - 1]);
                        }
                    }
                    else if (line.StartsWith("#IF", StringComparison.OrdinalIgnoreCase))
                    {
                        // RANDOM分岐開始
                        if (crandoms.Count == 0)
                        {
                            logs.Add(new DecodeLog(State.Warning, "#IFに対応する#RANDOMが定義されていません"));
                            continue;
                        }
                        if (int.TryParse(line[4..].Trim(), out var x))
                        {
                            skip.AddLast(crandoms.Last!.Value != x);
                        }
                        else
                        {
                            logs.Add(new DecodeLog(State.Warning, "#IFに数字が定義されていません"));
                        }
                    }
                    else if (line.StartsWith("#ENDIF", StringComparison.OrdinalIgnoreCase))
                    {
                        if (skip.Count != 0)
                        {
                            skip.RemoveLast();
                        }
                        else
                        {
                            logs.Add(new DecodeLog(State.Warning, "ENDIFに対応するIFが存在しません: " + line));
                        }
                    }
                    else if (line.StartsWith("#ENDRANDOM", StringComparison.OrdinalIgnoreCase))
                    {
                        if (crandoms.Count != 0)
                        {
                            crandoms.RemoveLast();
                        }
                        else
                        {
                            logs.Add(new DecodeLog(State.Warning, "ENDRANDOMに対応するRANDOMが存在しません: " + line));
                        }
                    }
                    else if (skip.Count != 0 && skip.Last!.Value)
                    {
                        continue;
                    }
                }
                processor.Process(model, line, logs);
            }

            model.WavList = [.. processor.WavList];
            model.BgaList = [.. processor.BgaList];

            Section? prev = null;
            var sections = new Section[processor.BarTable.Keys.Max() + 1];
            for (var i = 0; i <= processor.BarTable.Keys.Max(); i++)
            {
                sections[i] = new Section(model, prev, processor.BarTable.TryGetValue(i, out var bars) ? [.. bars] : ([]), processor.BpmTable.ToDictionary(),
                        processor.StopTable.ToDictionary(), processor.ScrollTable.ToDictionary(), logs);
                prev = sections[i];
            }

            SortedDictionary<double, TimeLineCache> timelines = [];
            var lnlist = new List<LongNote>[model.Mode.Key];
            var lnendstatus = new LongNote[model.Mode.Key];
            var basetl = new Timeline(0, 0, model.Mode.Key)
            {
                Bpm = model.Bpm
            };
            timelines.Put(0.0, new TimeLineCache(0.0, basetl));
            foreach (var section in sections)
            {
                section.MakeTimeLines(processor.WavMap, processor.BgaMap, timelines, lnlist, lnendstatus);
            }
            // Logger.getGlobal().info(
            // "Section生成時間(ms) :" + (System.currentTimeMillis() - time));
            var tl = new Timeline[timelines.Count];
            var tlcount = 0;
            foreach (var tlc in timelines.Values)
            {
                tl[tlcount] = tlc.Timeline;
                tlcount++;
            }
            model.Timelines = tl;

            if (tl[0].Bpm == 0)
            {
                logs.Add(new DecodeLog(State.Error, "開始BPMが定義されていないため、BMS解析に失敗しました"));
                //Logger.getGlobal().severe(path + ":BMSファイル解析失敗: 開始BPMが定義されていません");
                return null;
            }

            for (var i = 0; i < lnendstatus.Length; i++)
            {
                if (lnendstatus[i] != null)
                {
                    logs.Add(new DecodeLog(State.Warning, "曲の終端までにLN終端定義されていないLNがあります。lane:" + (i + 1)));
                    if (lnendstatus[i].Section != double.MinValue)
                    {
                        timelines[lnendstatus[i].Section].Timeline.SetNote(i, null);
                    }
                }
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
                if (tl[^1].Time >= model.LastTime + 30000)
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
            model.MD5 = GetMd5Hash(data);
            model.Sha256 = GetSha256Hash(data);
            logs.Add(new DecodeLog(State.Info, "#PLAYER定義が1にもかかわらず2P側のノーツ定義が存在します"));
            //Logger.getGlobal().fine("BMSデータ解析時間(ms) :" + (System.currentTimeMillis() - time));

            if (selectedRandom == null)
            {
                selectedRandom = new int[srandoms.Count];
                var ri = srandoms.GetEnumerator();
                for (var i = 0; i < selectedRandom.Length; i++)
                {
                    ri.MoveNext();
                    selectedRandom[i] = ri.Current;
                }
            }

            model.ChartInformation = new ChartInformation(path, LNType, selectedRandom);
            return model;
        }
    }
}
