using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BmsParser
{
    public class BmsDecoder : ChartDecoder
    {

        List<String> wavlist = new List<String>(62 * 62);
        private int[] wm = new int[62 * 62];

        List<String> bgalist = new List<String>(62 * 62);
        private int[] bm = new int[62 * 62];

        public BmsDecoder(LNType lnType = LNType.LongNote)
        {
            lntype = lnType;
        }

        new public BmsModel Decode(string path)
        {
            var model = decode(path, File.ReadAllBytes(path), path.EndsWith(".pms"), null);
            return model;
        }

        public BmsModel Decode(string path, byte[] bin)
        {
            var model = decode(path, bin, path.EndsWith(".pms"), null);
            return model;
        }

        public override BmsModel Decode(ChartInformation info)
        {
            try
            {
                this.lntype = info.LNType;
                return decode(info.Path, File.ReadAllBytes(info.Path), info.Path.ToString().ToLower().EndsWith(".pms"), info.SelectedRandoms);
            }
            catch (IOException e)
            {
                log.Add(new DecodeLog(State.Error, "BMSファイルが見つかりません"));
                //Logger.getGlobal().severe("BMSファイル解析中の例外 : " + e.getClass().getName() + " - " + e.getMessage());
            }
            return null;
        }

        protected BmsModel decode(string f)
        {
            //Logger.getGlobal().fine("BMSファイル解析開始 :" + f);
            try
            {
                BmsModel model = this.decode(f, File.ReadAllBytes(f), f.ToLower().EndsWith(".pms"), null);
                if (model == null)
                {
                    return null;
                }
                //Logger.getGlobal().fine("BMSファイル解析完了 :" + f.toString() + " - TimeLine数:" + model.getAllTimes().Length);
                return model;
            }
            catch (IOException e)
            {
                log.Add(new DecodeLog(State.Error, "BMSファイルが見つかりません"));
                //Logger.getGlobal().severe("BMSファイル解析中の例外 : " + e.getClass().getName() + " - " + e.getMessage());
            }
            return null;
        }


        private List<String>[] lines = new List<string>[1000];

        private Dictionary<int, Double> scrolltable = new Dictionary<int, double>();
        private Dictionary<int, Double> stoptable = new Dictionary<int, Double>();
        private Dictionary<int, Double> bpmtable = new Dictionary<int, Double>();
        private LinkedList<int> randoms = new LinkedList<int>();
        private LinkedList<int> srandoms = new LinkedList<int>();
        private LinkedList<int> crandom = new LinkedList<int>();
        private LinkedList<Boolean> skip = new LinkedList<Boolean>();

        private static CommandWord[] commandWords = CommandWord.values;

        /**
         * 指定したBMSファイルをモデルにデコードする
         *
         * @param data
         * @return
         */
        public BmsModel decode(byte[] data, bool ispms, int[] random)
        {
            return this.decode(null, data, ispms, random);
        }

        /**
         * 指定したBMSファイルをモデルにデコードする
         *
         * @param data
         * @return
         */
        private BmsModel decode(string path, byte[] data, bool ispms, int[] selectedRandom)
        {
            log.Clear();
            long time = DateTime.Now.Ticks;
            BmsModel model = new BmsModel(ispms ? Mode.Popn9K : Mode.Beat5K);
            scrolltable.Clear();
            stoptable.Clear();
            bpmtable.Clear();

            //MessageDigest md5digest, sha256digest;
            //try
            //{
            //    md5digest = MessageDigest.getInstance("MD5");
            //    sha256digest = MessageDigest.getInstance("SHA-256");
            //}
            //catch (NoSuchAlgorithmException e1)
            //{
            //    e1.printStackTrace();
            //    return null;
            //}

            int maxsec = 0;
            // BMS読み込み、ハッシュ値取得
            //try (BufferedReader br = new BufferedReader(new InputStreamReader(
            //        new DigestInputStream(new DigestInputStream(new ByteArrayInputStream(data), md5digest), sha256digest),
            //        "MS932"));) {
            try
            {
                using var mem = new MemoryStream(data);
                using var br = new StreamReader(mem);
                //model.Mode = ispms ? Mode.Popn9K : Mode.Beat5K;
                // Logger.getGlobal().info(
                // "BMSデータ読み込み時間(ms) :" + (System.currentTimeMillis() - time));

                String line = null;
                wavlist.Clear();
                Array.Fill(wm, -2);
                bgalist.Clear();
                Array.Fill(bm, -2);
                foreach (List<String> l in lines)
                {
                    if (l != null)
                    {
                        l.Clear();
                    }
                }

                randoms.Clear();
                srandoms.Clear();
                crandom.Clear();

                skip.Clear();
                while ((line = br.ReadLine()) != null)
                {
                    if (line.Length < 2)
                    {
                        continue;
                    }

                    if (line[0] == '#')
                    {
                        // line = line.Substring(1, line.Length);
                        // RANDOM制御系
                        if (matchesReserveWord(line, "RANDOM"))
                        {
                            try
                            {
                                int r = int.Parse(line.Substring(8).Trim());
                                randoms.AddLast(r);
                                if (selectedRandom != null)
                                {
                                    crandom.AddLast(selectedRandom[randoms.Count - 1]);
                                }
                                else
                                {
                                    crandom.AddLast((int)(new Random().NextDouble() * r) + 1);
                                    srandoms.AddLast(crandom.Last);
                                }
                            }
                            catch (FormatException e)
                            {
                                log.Add(new DecodeLog(State.Warning, "#RANDOMに数字が定義されていません"));
                            }
                        }
                        else if (matchesReserveWord(line, "IF"))
                        {
                            // RANDOM分岐開始
                            if (crandom.Count != 0)
                            {
                                try
                                {
                                    skip.AddLast((crandom.Last.Value != int.Parse(line.Substring(4).Trim())));
                                }
                                catch (FormatException e)
                                {
                                    log.Add(new DecodeLog(State.Warning, "#IFに数字が定義されていません"));
                                }
                            }
                            else
                            {
                                log.Add(new DecodeLog(State.Warning, "#IFに対応する#RANDOMが定義されていません"));
                            }
                        }
                        else if (matchesReserveWord(line, "ENDIF"))
                        {
                            if (skip.Count != 0)
                            {
                                skip.RemoveLast();
                            }
                            else
                            {
                                log.Add(new DecodeLog(State.Warning, "ENDIFに対応するIFが存在しません: " + line));
                            }
                        }
                        else if (matchesReserveWord(line, "ENDRANDOM"))
                        {
                            if (crandom.Count != 0)
                            {
                                crandom.RemoveLast();
                            }
                            else
                            {
                                log.Add(new DecodeLog(State.Warning, "ENDRANDOMに対応するRANDOMが存在しません: " + line));
                            }
                        }
                        else if (skip.Count == 0 || skip.Last == null)
                        {
                            char c = line[1];
                            int @base = model.Base;
                            if ('0' <= c && c <= '9' && line.Count() > 6)
                            {
                                // line = line.toUpperCase();
                                // 楽譜
                                char c2 = line[2];
                                char c3 = line[3];
                                if ('0' <= c2 && c2 <= '9' && '0' <= c3 && c3 <= '9')
                                {
                                    int bar_index = (c - '0') * 100 + (c2 - '0') * 10 + (c3 - '0');
                                    List<String> l = lines[bar_index];
                                    if (l == null)
                                    {
                                        l = lines[bar_index] = new List<String>();
                                    }
                                    l.Add(line);
                                    maxsec = (maxsec > bar_index) ? maxsec : bar_index;
                                }
                                else
                                {
                                    log.Add(new DecodeLog(State.Warning, "小節に数字が定義されていません : " + line));
                                }
                            }
                            else if (matchesReserveWord(line, "BPM"))
                            {
                                if (line[4] == ' ')
                                {
                                    // BPMは小数点のケースがある(FREEDOM DiVE)
                                    try
                                    {
                                        String arg = line.Substring(5).Trim();
                                        double bpm = Double.Parse(arg);
                                        if (bpm > 0)
                                        {
                                            model.Bpm = bpm;
                                        }
                                        else
                                        {
                                            log.Add(new DecodeLog(State.Warning, "#negative BPMはサポートされていません : " + line));
                                        }
                                    }
                                    catch (FormatException e)
                                    {
                                        log.Add(new DecodeLog(State.Warning, "#BPMに数字が定義されていません : " + line));
                                    }
                                }
                                else
                                {
                                    try
                                    {
                                        double bpm = Double.Parse(line.Substring(7).Trim());
                                        if (bpm > 0)
                                        {
                                            if (@base == 62)
                                            {
                                                bpmtable.put(ChartDecoder.parseInt62(line, 4), bpm);
                                            }
                                            else
                                            {
                                                bpmtable.put(ChartDecoder.parseInt36(line, 4), bpm);
                                            }
                                        }
                                        else
                                        {
                                            log.Add(new DecodeLog(State.Warning, "#negative BPMはサポートされていません : " + line));
                                        }
                                    }
                                    catch (FormatException e)
                                    {
                                        log.Add(new DecodeLog(State.Warning, "#BPMxxに数字が定義されていません : " + line));
                                    }
                                }
                            }
                            else if (matchesReserveWord(line, "WAV"))
                            {
                                // 音源ファイル
                                if (line.Length >= 8)
                                {
                                    try
                                    {
                                        String file_name = line.Substring(7).Trim().Replace('\\', '/');
                                        if (@base == 62)
                                        {
                                            wm[ChartDecoder.parseInt62(line, 4)] = wavlist.Count;
                                        }
                                        else
                                        {
                                            wm[ChartDecoder.parseInt36(line, 4)] = wavlist.Count;
                                        }
                                        wavlist.Add(file_name);
                                    }
                                    catch (FormatException e)
                                    {
                                        log.Add(new DecodeLog(State.Warning, "#WAVxxは不十分な定義です : " + line));
                                    }
                                }
                                else
                                {
                                    log.Add(new DecodeLog(State.Warning, "#WAVxxは不十分な定義です : " + line));
                                }
                            }
                            else if (matchesReserveWord(line, "BMP"))
                            {
                                // BGAファイル
                                if (line.Length >= 8)
                                {
                                    try
                                    {
                                        String file_name = line.Substring(7).Trim().Replace('\\', '/');
                                        if (@base == 62)
                                        {
                                            bm[ChartDecoder.parseInt62(line, 4)] = bgalist.Count;
                                        }
                                        else
                                        {
                                            bm[ChartDecoder.parseInt36(line, 4)] = bgalist.Count;
                                        }
                                        bgalist.Add(file_name);
                                    }
                                    catch (FormatException e)
                                    {
                                        log.Add(new DecodeLog(State.Warning, "#BMPxxは不十分な定義です : " + line));
                                    }
                                }
                                else
                                {
                                    log.Add(new DecodeLog(State.Warning, "#BMPxxは不十分な定義です : " + line));
                                }
                            }
                            else if (matchesReserveWord(line, "STOP"))
                            {
                                if (line.Length >= 9)
                                {
                                    try
                                    {
                                        double stop = Double.Parse(line.Substring(8).Trim()) / 192;
                                        if (stop < 0)
                                        {
                                            stop = Math.Abs(stop);
                                            log.Add(new DecodeLog(State.Warning, "#negative STOPはサポートされていません : " + line));
                                        }
                                        if (@base == 62)
                                        {
                                            stoptable.put(ChartDecoder.parseInt62(line, 5), stop);
                                        }
                                        else
                                        {
                                            stoptable.put(ChartDecoder.parseInt36(line, 5), stop);
                                        }
                                    }
                                    catch (FormatException e)
                                    {
                                        log.Add(new DecodeLog(State.Warning, "#STOPxxに数字が定義されていません : " + line));
                                    }
                                }
                                else
                                {
                                    log.Add(new DecodeLog(State.Warning, "#STOPxxは不十分な定義です : " + line));
                                }
                            }
                            else if (matchesReserveWord(line, "SCROLL"))
                            {
                                if (line.Length >= 11)
                                {
                                    try
                                    {
                                        double scroll = Double.Parse(line.Substring(10).Trim());
                                        if (@base == 62)
                                        {
                                            scrolltable.put(ChartDecoder.parseInt62(line, 7), scroll);
                                        }
                                        else
                                        {
                                            scrolltable.put(ChartDecoder.parseInt36(line, 7), scroll);
                                        }
                                    }
                                    catch (FormatException e)
                                    {
                                        log.Add(new DecodeLog(State.Warning, "#SCROLLxxに数字が定義されていません : " + line));
                                    }
                                }
                                else
                                {
                                    log.Add(new DecodeLog(State.Warning, "#SCROLLxxは不十分な定義です : " + line));
                                }
                            }
                            else
                            {
                                foreach (CommandWord cw in commandWords)
                                {
                                    if (line.Length > cw.name.Length + 2 && matchesReserveWord(line, cw.name))
                                    {
                                        DecodeLog log = cw.function(model, line.Substring(cw.name.Length + 2).Trim());
                                        if (log != null)
                                        {
                                            this.log.Add(log);
                                            //Logger.getGlobal().warning(model.getTitle() + " - " + log.getMessage() + " : " + line);
                                        }
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    else if (line[0] == '%')
                    {
                        int index = line.IndexOf(' ');
                        if (index > 0 && line.Length > index + 1)
                        {
                            model.Values.put(line.Substring(1, index), line.Substring(index + 1));
                        }
                    }
                    else if (line[0] == '@')
                    {
                        int index = line.IndexOf(' ');
                        if (index > 0 && line.Length > index + 1)
                        {
                            model.Values.put(line.Substring(1, index), line.Substring(index + 1));
                        }
                    }
                }

                model.WavList = wavlist.ToArray();
                model.BgaList = bgalist.ToArray();

                Section prev = null;
                Section[] sections = new Section[maxsec + 1];
                for (int i = 0; i <= maxsec; i++)
                {
                    sections[i] = new Section(model, prev, lines[i] != null ? lines[i] : new List<string>(), bpmtable,
                            stoptable, scrolltable, log);
                    prev = sections[i];
                }

                SortedDictionary<Double, TimeLineCache> timelines = new SortedDictionary<Double, TimeLineCache>();
                List<LongNote>[] lnlist = new List<LongNote>[model.Mode.Key];
                LongNote[] lnendstatus = new LongNote[model.Mode.Key];
                Timeline basetl = new Timeline(0, 0, model.Mode.Key);
                basetl.                Bpm = model.Bpm;
                timelines.put(0.0, new TimeLineCache(0.0, basetl));
                foreach (Section section in sections)
                {
                    section.makeTimeLines(wm, bm, timelines, lnlist, lnendstatus);
                }
                // Logger.getGlobal().info(
                // "Section生成時間(ms) :" + (System.currentTimeMillis() - time));
                Timeline[] tl = new Timeline[timelines.Count];
                int tlcount = 0;
                foreach (TimeLineCache tlc in timelines.Values)
                {
                    tl[tlcount] = tlc.timeline;
                    tlcount++;
                }
                model.Timelines = tl;

                if (tl[0].Bpm == 0)
                {
                    log.Add(new DecodeLog(State.Error, "開始BPMが定義されていないため、BMS解析に失敗しました"));
                    //Logger.getGlobal().severe(path + ":BMSファイル解析失敗: 開始BPMが定義されていません");
                    return null;
                }

                for (int i = 0; i < lnendstatus.Length; i++)
                {
                    if (lnendstatus[i] != null)
                    {
                        log.Add(new DecodeLog(State.Warning, "曲の終端までにLN終端定義されていないLNがあります。lane:" + (i + 1)));
                        if (lnendstatus[i].Section != Double.MinValue)
                        {
                            timelines[lnendstatus[i].Section].timeline.SetNote(i, null);
                        }
                    }
                }

                if (model.TotalType != TotalType.Bms)
                {
                    log.Add(new DecodeLog(State.Warning, "TOTALが未定義です"));
                }
                if (model.Total <= 60.0)
                {
                    log.Add(new DecodeLog(State.Warning, "TOTAL値が少なすぎます"));
                }
                if (tl.Length > 0)
                {
                    if (tl[tl.Length - 1].Time >= model.LastTime + 30000)
                    {
                        log.Add(new DecodeLog(State.Warning, "最後のノート定義から30秒以上の余白があります"));
                    }
                }
                if (model.Player > 1 && (model.Mode == Mode.Beat5K || model.Mode == Mode.Beat7K))
                {
                    log.Add(new DecodeLog(State.Warning, "#PLAYER定義が2以上にもかかわらず2P側のノーツ定義が一切ありません"));
                }
                if (model.Player == 1 && (model.Mode == Mode.Beat10K || model.Mode == Mode.Beat14K))
                {
                    log.Add(new DecodeLog(State.Warning, "#PLAYER定義が1にもかかわらず2P側のノーツ定義が存在します"));
                }
                model.MD5 = getMd5Hash(data);
                model.Sha256 = getSha256Hash(data);
                log.Add(new DecodeLog(State.Info, "#PLAYER定義が1にもかかわらず2P側のノーツ定義が存在します"));
                //Logger.getGlobal().fine("BMSデータ解析時間(ms) :" + (System.currentTimeMillis() - time));

                if (selectedRandom == null)
                {
                    selectedRandom = new int[srandoms.Count];
                    var ri = srandoms.GetEnumerator();
                    for (int i = 0; i < selectedRandom.Length; i++)
                    {
                        ri.MoveNext();
                        selectedRandom[i] = ri.Current;
                    }
                }

                model.ChartInformation = new ChartInformation(path, lntype, selectedRandom);
                printLog(path);
                return model;
            }
            catch (IOException e)
            {
                log.Add(new DecodeLog(State.Error, "BMSファイルへのアクセスに失敗しました"));
                //Logger.getGlobal()
                //        .severe(path + ":BMSファイル解析失敗: " + e.getClass().getName() + " - " + e.getMessage());
            }
            catch (Exception e)
            {
                log.Add(new DecodeLog(State.Error, "何らかの異常によりBMS解析に失敗しました"));
                throw;
                //Logger.getGlobal()
                //        .severe(path + ":BMSファイル解析失敗: " + e.getClass().getName() + " - " + e.getMessage());
                //e.printStackTrace();
            }
            return null;
        }

        private bool matchesReserveWord(String line, String s)
        {
            int len = s.Length;
            if (line.Length <= len)
            {
                return false;
            }
            for (int i = 0; i < len; i++)
            {
                char c = line[i + 1];
                char c2 = s[i];
                if (c != c2 && c != c2 + 32)
                {
                    return false;
                }
            }
            return true;
        }

        ///**
        // * バイトデータを16進数文字列表現に変換する
        // * 
        // * @param data
        // *            バイトデータ
        // * @returnバイトデータの16進数文字列表現
        // */
        //public static String convertHexString(byte[] data)
        //{
        //    StringBuilder sb = new StringBuilder(data.Length * 2);
        //    foreach (byte b in data)
        //    {
        //        sb.Append(Character.forDigit(b >> 4 & 0xf, 16));
        //        sb.Append(Character.forDigit(b & 0xf, 16));
        //    }
        //    return sb.ToString();
        //}

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

    /**
     * 予約語
     *
     * @author exch
     */
    class CommandWord
    {

        public static readonly CommandWord PLAYER = new CommandWord(nameof(PLAYER), (model, arg) =>
        {
            try
            {

                int player = int.Parse(arg);
                // TODO playerの許容幅は？
                if (player >= 1 && player < 3)
                {

                    model.Player = player;
                }
                else
                {
                    return new DecodeLog(State.Warning, "#PLAYERに規定外の数字が定義されています : " + player);
                }
            }
            catch (FormatException e)
            {
                return new DecodeLog(State.Warning, "#PLAYERに数字が定義されていません");
            }
            return null;
        });
        public static readonly CommandWord GENRE = new CommandWord(nameof(GENRE), (model, arg) =>
        {
            model.Genre = arg;
            return null;
        });
        public static readonly CommandWord TITLE = new CommandWord(nameof(TITLE), (model, arg) =>
        {
            model.Title = arg;
            return null;
        });
        public static readonly CommandWord SUBTITLE = new CommandWord(nameof(SUBTITLE), (model, arg) =>
        {
            model.Subtitle = arg;
            return null;
        });
        public static readonly CommandWord ARTIST = new CommandWord(nameof(ARTIST), (model, arg) =>
        {
            model.Artist = arg;
            return null;
        });
        public static readonly CommandWord SUBARTIST = new CommandWord(nameof(SUBARTIST), (model, arg) =>
        {
            model.Subartist = arg;
            return null;
        });
        public static readonly CommandWord PLAYLEVEL = new CommandWord(nameof(PLAYLEVEL), (model, arg) =>
        {
            model.PlayLevel = arg;
            return null;
        });
        public static readonly CommandWord RANK = new CommandWord(nameof(RANK), (model, arg) =>
        {
            try
            {
                int rank = int.Parse(arg);
                if (rank >= 0 && rank < 5)
                {
                    model.JudgeRank = rank;
                    model.JudgeRankType = JudgeRankType.BmsRank;
                }
                else
                {
                    return new DecodeLog(State.Warning, "#RANKに規定外の数字が定義されています : " + rank);
                }
            }
            catch (FormatException e)
            {
                return new DecodeLog(State.Warning, "#RANKに数字が定義されていません");
            }
            return null;
        });
        public static readonly CommandWord DEFEXRANK = new CommandWord(nameof(DEFEXRANK), (model, arg) =>
        {
            try
            {
                int rank = int.Parse(arg);
                if (rank >= 1)
                {
                    model.JudgeRank = rank;
                    model.JudgeRankType = JudgeRankType.BmsDefEXRank;
                }
                else
                {
                    return new DecodeLog(State.Warning, "#DEFEXRANK 1以下はサポートしていません" + rank);
                }
            }
            catch (FormatException e)
            {
                return new DecodeLog(State.Warning, "#DEFEXRANKに数字が定義されていません");
            }
            return null;
        });
        public static readonly CommandWord TOTAL = new CommandWord(nameof(TOTAL), (model, arg) =>
        {
            try
            {
                double total = Double.Parse(arg);
                if (total > 0)
                {
                    model.Total = total;
                    model.TotalType = TotalType.Bms;
                }
                else
                {
                    return new DecodeLog(State.Warning, "#TOTALが0以下です");
                }
            }
            catch (FormatException e)
            {
                return new DecodeLog(State.Warning, "#TOTALに数字が定義されていません");
            }
            return null;
        });
        public static readonly CommandWord VOLWAV = new CommandWord(nameof(VOLWAV), (model, arg) =>
        {
            try
            {
                model.VolWav = int.Parse(arg);
            }
            catch (FormatException e)
            {
                return new DecodeLog(State.Warning, "#VOLWAVに数字が定義されていません");
            }
            return null;
        });
        public static readonly CommandWord STAGEFILE = new CommandWord(nameof(STAGEFILE), (model, arg) =>
        {
            model.StageFile = arg.Replace('\\', '/');
            return null;
        });
        public static readonly CommandWord BACKBMP = new CommandWord(nameof(BACKBMP), (model, arg) =>
        {
            model.BackBmp = arg.Replace('\\', '/');
            return null;
        });
        public static readonly CommandWord PREVIEW = new CommandWord(nameof(PREVIEW), (model, arg) =>
        {
            model.Preview = arg.Replace('\\', '/');
            return null;
        });
        public static readonly CommandWord LNOBJ = new CommandWord(nameof(LNOBJ), (model, arg) =>
        {
            try
            {
                if (model.Base == 62)
                {
                    model.LNObj = ChartDecoder.parseInt62(arg, 0);
                }
                else
                {
                    model.LNObj = ChartDecoder.parseInt36(arg, 0);
                }
            }
            catch (FormatException e)
            {
                return new DecodeLog(State.Warning, "#LNOBJに数字が定義されていません");
            }
            return null;
        });
        public static readonly CommandWord LNMODE = new CommandWord(nameof(LNMODE), (model, arg) =>
        {
            try
            {
                int lnmode = int.Parse(arg);
                if (lnmode < 0 || lnmode > 3)
                {
                    return new DecodeLog(State.Warning, "#LNMODEに無効な数字が定義されています");
                }
                model.LNMode = (LNMode)lnmode;
            }
            catch (FormatException e)
            {
                return new DecodeLog(State.Warning, "#LNMODEに数字が定義されていません");
            }
            return null;
        });
        public static readonly CommandWord DIFFICULTY = new CommandWord(nameof(DIFFICULTY), (model, arg) =>
        {
            try
            {
                model.Difficulty = (Difficulty)int.Parse(arg);
            }
            catch (FormatException e)
            {
                return new DecodeLog(State.Warning, "#DIFFICULTYに数字が定義されていません");
            }
            return null;
        });
        public static readonly CommandWord BANNER = new CommandWord(nameof(BANNER), (model, arg) =>
        {
            model.Banner = arg.Replace('\\', '/');
            return null;
        });
        public static readonly CommandWord COMMENT = new CommandWord(nameof(COMMENT), (model, arg) =>
        {
            // TODO 未実装
            return null;
        });
        public static readonly CommandWord BASE = new CommandWord(nameof(BASE), (model, arg) =>
        {
            try
            {
                int @base = int.Parse(arg);
                if (@base != 62)
                {
                    return new DecodeLog(State.Warning, "#BASEに無効な数字が定義されています");
                }
                model.Base = @base;
            }
            catch (FormatException e)
            {
                return new DecodeLog(State.Warning, "#BASEに数字が定義されていません");
            }
            return null;
        });

        public static readonly CommandWord[] values = [PLAYER, GENRE, TITLE, SUBTITLE, ARTIST, SUBARTIST, PLAYLEVEL, RANK, DEFEXRANK, TOTAL, VOLWAV, STAGEFILE, BACKBMP, PREVIEW, LNOBJ, LNMODE, DIFFICULTY, BANNER, COMMENT, BASE];

        public Func<BmsModel, String, DecodeLog> function;
        public string name;

        private CommandWord(string name, Func<BmsModel, String, DecodeLog> function)
        {
            this.name = name;
            this.function = function;
        }
    }

    /**
     * 予約語
     *
     * @author exch
     */
    public class OptionWord
    {

        OptionWord URL = new OptionWord((model, arg) =>
        {
            // TODO 未実装
            return null;
        });

        public Func<BmsModel, String, DecodeLog> function;

        private OptionWord(Func<BmsModel, String, DecodeLog> function)
        {
            this.function = function;
        }
    }
}
