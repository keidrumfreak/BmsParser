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

        readonly List<string> wavlist = new(62 * 62);
        private readonly int[] wm = new int[62 * 62];

        readonly List<string> bgalist = new(62 * 62);
        private readonly int[] bm = new int[62 * 62];

        public BmsDecoder(LNType lnType = LNType.LongNote)
        {
            lntype = lnType;
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
                this.lntype = info.LNType;
                return decode(info.Path, File.ReadAllBytes(info.Path), info.Path.ToString().ToLower().EndsWith(".pms"), info.SelectedRandoms);
            }
            catch (IOException)
            {
                log.Add(new DecodeLog(State.Error, "BMSファイルが見つかりません"));
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


        private readonly List<string>[] lines = new List<string>[1000];

        private readonly Dictionary<int, double> scrolltable = [];
        private readonly Dictionary<int, double> stoptable = [];
        private readonly Dictionary<int, double> bpmtable = [];
        private readonly LinkedList<int> randoms = new();
        private readonly LinkedList<int> srandoms = new();
        private readonly LinkedList<int> crandom = new();
        private readonly LinkedList<bool> skip = new();

        private static readonly CommandWord[] commandWords = CommandWord.values;

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

        /**
         * 指定したBMSファイルをモデルにデコードする
         *
         * @param data
         * @return
         */
        private BmsModel? decode(string path, byte[] data, bool ispms, int[]? selectedRandom)
        {
            log.Clear();
            var time = DateTime.Now.Ticks;
            var model = new BmsModel(ispms ? Mode.Popn9K : Mode.Beat5K);
            scrolltable.Clear();
            stoptable.Clear();
            bpmtable.Clear();

            var maxsec = 0;
            // BMS読み込み、ハッシュ値取得
            try
            {
                using var mem = new MemoryStream(data);
                using var br = new StreamReader(mem);
                //model.Mode = ispms ? Mode.Popn9K : Mode.Beat5K;
                // Logger.getGlobal().info(
                // "BMSデータ読み込み時間(ms) :" + (System.currentTimeMillis() - time));

                string? line = null;
                wavlist.Clear();
                Array.Fill(wm, -2);
                bgalist.Clear();
                Array.Fill(bm, -2);
                foreach (var l in lines)
                {
                    l?.Clear();
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
                            if (int.TryParse(line[8..].Trim(), out var r))
                            {
                                randoms.AddLast(r);
                                if (selectedRandom != null)
                                {
                                    crandom.AddLast(selectedRandom[randoms.Count - 1]);
                                }
                                else
                                {
                                    crandom.AddLast((int)(new Random().NextDouble() * r) + 1);
                                    srandoms.AddLast(crandom.Last!);
                                }
                            }
                            else
                            {
                                log.Add(new DecodeLog(State.Warning, "#RANDOMに数字が定義されていません"));
                            }
                        }
                        else if (matchesReserveWord(line, "IF"))
                        {
                            // RANDOM分岐開始
                            if (crandom.Count != 0)
                            {
                                if (int.TryParse(line[4..].Trim(), out var x))
                                {
                                    skip.AddLast(crandom.Last!.Value != x);
                                }
                                else
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
                            var c = line[1];
                            var @base = model.Base;
                            if ('0' <= c && c <= '9' && line.Length > 6)
                            {
                                // line = line.toUpperCase();
                                // 楽譜
                                var c2 = line[2];
                                var c3 = line[3];
                                if ('0' <= c2 && c2 <= '9' && '0' <= c3 && c3 <= '9')
                                {
                                    var bar_index = (c - '0') * 100 + (c2 - '0') * 10 + (c3 - '0');
                                    var l = lines[bar_index];
                                    l ??= lines[bar_index] = [];
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
                                    if (double.TryParse(line[5..].Trim(), out var bpm))
                                    {
                                        if (bpm > 0)
                                        {
                                            model.Bpm = bpm;
                                        }
                                        else
                                        {
                                            log.Add(new DecodeLog(State.Warning, "#negative BPMはサポートされていません : " + line));
                                        }
                                    }
                                    else
                                    {
                                        log.Add(new DecodeLog(State.Warning, "#BPMに数字が定義されていません : " + line));
                                    }
                                }
                                else
                                {
                                    if (double.TryParse(line[7..].Trim(), out var bpm))
                                    {
                                        if (bpm > 0)
                                        {
                                            if (@base == 62)
                                            {
                                                bpmtable.Put(ParseInt62(line, 4), bpm);
                                            }
                                            else
                                            {
                                                bpmtable.Put(ParseInt36(line, 4), bpm);
                                            }
                                        }
                                        else
                                        {
                                            log.Add(new DecodeLog(State.Warning, "#negative BPMはサポートされていません : " + line));
                                        }
                                    }
                                    else
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
                                        var file_name = line[7..].Trim().Replace('\\', '/');
                                        if (@base == 62)
                                        {
                                            wm[ParseInt62(line, 4)] = wavlist.Count;
                                        }
                                        else
                                        {
                                            wm[ParseInt36(line, 4)] = wavlist.Count;
                                        }
                                        wavlist.Add(file_name);
                                    }
                                    catch (FormatException)
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
                                        var file_name = line[7..].Trim().Replace('\\', '/');
                                        if (@base == 62)
                                        {
                                            bm[ParseInt62(line, 4)] = bgalist.Count;
                                        }
                                        else
                                        {
                                            bm[ParseInt36(line, 4)] = bgalist.Count;
                                        }
                                        bgalist.Add(file_name);
                                    }
                                    catch (FormatException)
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
                                    if (double.TryParse(line[8..].Trim(), out var stop))
                                    {
                                        stop /= 192;
                                        if (stop < 0)
                                        {
                                            stop = Math.Abs(stop);
                                            log.Add(new DecodeLog(State.Warning, "#negative STOPはサポートされていません : " + line));
                                        }
                                        if (@base == 62)
                                        {
                                            stoptable.Put(ParseInt62(line, 5), stop);
                                        }
                                        else
                                        {
                                            stoptable.Put(ParseInt36(line, 5), stop);
                                        }
                                    }
                                    else
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
                                    if (double.TryParse(line[10..].Trim(), out var scroll))
                                    {
                                        if (@base == 62)
                                        {
                                            scrolltable.Put(ParseInt62(line, 7), scroll);
                                        }
                                        else
                                        {
                                            scrolltable.Put(ParseInt36(line, 7), scroll);
                                        }
                                    }
                                    else
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
                                foreach (var cw in commandWords)
                                {
                                    if (line.Length > cw.name.Length + 2 && matchesReserveWord(line, cw.name))
                                    {
                                        var log = cw.function(model, line[(cw.name.Length + 2)..].Trim());
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
                        var index = line.IndexOf(' ');
                        if (index > 0 && line.Length > index + 1)
                        {
                            model.Values.Put(line.Substring(1, index), line[(index + 1)..]);
                        }
                    }
                    else if (line[0] == '@')
                    {
                        var index = line.IndexOf(' ');
                        if (index > 0 && line.Length > index + 1)
                        {
                            model.Values.Put(line.Substring(1, index), line[(index + 1)..]);
                        }
                    }
                }

                model.WavList = [.. wavlist];
                model.BgaList = [.. bgalist];

                Section? prev = null;
                var sections = new Section[maxsec + 1];
                for (var i = 0; i <= maxsec; i++)
                {
                    sections[i] = new Section(model, prev, lines[i] ?? ([]), bpmtable,
                            stoptable, scrolltable, log);
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
                    section.MakeTimeLines(wm, bm, timelines, lnlist, lnendstatus);
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
                    log.Add(new DecodeLog(State.Error, "開始BPMが定義されていないため、BMS解析に失敗しました"));
                    //Logger.getGlobal().severe(path + ":BMSファイル解析失敗: 開始BPMが定義されていません");
                    return null;
                }

                for (var i = 0; i < lnendstatus.Length; i++)
                {
                    if (lnendstatus[i] != null)
                    {
                        log.Add(new DecodeLog(State.Warning, "曲の終端までにLN終端定義されていないLNがあります。lane:" + (i + 1)));
                        if (lnendstatus[i].Section != double.MinValue)
                        {
                            timelines[lnendstatus[i].Section].Timeline.SetNote(i, null);
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
                    if (tl[^1].Time >= model.LastTime + 30000)
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
                model.MD5 = GetMd5Hash(data);
                model.Sha256 = GetSha256Hash(data);
                log.Add(new DecodeLog(State.Info, "#PLAYER定義が1にもかかわらず2P側のノーツ定義が存在します"));
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

                model.ChartInformation = new ChartInformation(path, lntype, selectedRandom);
                PrintLog(path);
                return model;
            }
            catch (IOException)
            {
                log.Add(new DecodeLog(State.Error, "BMSファイルへのアクセスに失敗しました"));
                //Logger.getGlobal()
                //        .severe(path + ":BMSファイル解析失敗: " + e.getClass().getName() + " - " + e.getMessage());
            }
            catch (Exception)
            {
                log.Add(new DecodeLog(State.Error, "何らかの異常によりBMS解析に失敗しました"));
                throw;
                //Logger.getGlobal()
                //        .severe(path + ":BMSファイル解析失敗: " + e.getClass().getName() + " - " + e.getMessage());
                //e.printStackTrace();
            }
            return null;
        }

        private static bool matchesReserveWord(string line, string s)
        {
            var len = s.Length;
            if (line.Length <= len)
            {
                return false;
            }
            for (var i = 0; i < len; i++)
            {
                var c = line[i + 1];
                var c2 = s[i];
                if (c != c2 && c != c2 + 32)
                {
                    return false;
                }
            }
            return true;
        }
    }

    /**
     * 予約語
     *
     * @author exch
     */
    class CommandWord
    {

        public static readonly CommandWord PLAYER = new(nameof(PLAYER), (model, arg) =>
        {
            if (int.TryParse(arg, out var player))
            {
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
            else
            {
                return new DecodeLog(State.Warning, "#PLAYERに数字が定義されていません");
            }
            return null;
        });
        public static readonly CommandWord GENRE = new(nameof(GENRE), (model, arg) =>
        {
            model.Genre = arg;
            return null;
        });
        public static readonly CommandWord TITLE = new(nameof(TITLE), (model, arg) =>
        {
            model.Title = arg;
            return null;
        });
        public static readonly CommandWord SUBTITLE = new(nameof(SUBTITLE), (model, arg) =>
        {
            model.Subtitle = arg;
            return null;
        });
        public static readonly CommandWord ARTIST = new(nameof(ARTIST), (model, arg) =>
        {
            model.Artist = arg;
            return null;
        });
        public static readonly CommandWord SUBARTIST = new(nameof(SUBARTIST), (model, arg) =>
        {
            model.Subartist = arg;
            return null;
        });
        public static readonly CommandWord PLAYLEVEL = new(nameof(PLAYLEVEL), (model, arg) =>
        {
            model.PlayLevel = arg;
            return null;
        });
        public static readonly CommandWord RANK = new(nameof(RANK), (model, arg) =>
        {
            if (int.TryParse(arg, out var rank))
            {
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
            else
            {
                return new DecodeLog(State.Warning, "#RANKに数字が定義されていません");
            }
            return null;
        });
        public static readonly CommandWord DEFEXRANK = new(nameof(DEFEXRANK), (model, arg) =>
        {
            if (int.TryParse(arg, out var rank))
            {
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
            else
            {
                return new DecodeLog(State.Warning, "#DEFEXRANKに数字が定義されていません");
            }
            return null;
        });
        public static readonly CommandWord TOTAL = new(nameof(TOTAL), (model, arg) =>
        {
            if (double.TryParse(arg, out var total))
            {
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
            else
            {
                return new DecodeLog(State.Warning, "#TOTALに数字が定義されていません");
            }
            return null;
        });
        public static readonly CommandWord VOLWAV = new(nameof(VOLWAV), (model, arg) =>
        {
            if (int.TryParse(arg, out var x))
            {
                model.VolWav = x;
            }
            else
            {
                return new DecodeLog(State.Warning, "#VOLWAVに数字が定義されていません");
            }
            return null;
        });
        public static readonly CommandWord STAGEFILE = new(nameof(STAGEFILE), (model, arg) =>
        {
            model.StageFile = arg.Replace('\\', '/');
            return null;
        });
        public static readonly CommandWord BACKBMP = new(nameof(BACKBMP), (model, arg) =>
        {
            model.BackBmp = arg.Replace('\\', '/');
            return null;
        });
        public static readonly CommandWord PREVIEW = new(nameof(PREVIEW), (model, arg) =>
        {
            model.Preview = arg.Replace('\\', '/');
            return null;
        });
        public static readonly CommandWord LNOBJ = new(nameof(LNOBJ), (model, arg) =>
        {
            try
            {
                if (model.Base == 62)
                {
                    model.LNObj = ChartDecoder.ParseInt62(arg, 0);
                }
                else
                {
                    model.LNObj = ChartDecoder.ParseInt36(arg, 0);
                }
            }
            catch (FormatException)
            {
                return new DecodeLog(State.Warning, "#LNOBJに数字が定義されていません");
            }
            return null;
        });
        public static readonly CommandWord LNMODE = new(nameof(LNMODE), (model, arg) =>
        {
            if (int.TryParse(arg, out var lnmode))
            {
                if (lnmode < 0 || lnmode > 3)
                {
                    return new DecodeLog(State.Warning, "#LNMODEに無効な数字が定義されています");
                }
                model.LNMode = (LNMode)lnmode;
            }
            else
            {
                return new DecodeLog(State.Warning, "#LNMODEに数字が定義されていません");
            }
            return null;
        });
        public static readonly CommandWord DIFFICULTY = new(nameof(DIFFICULTY), (model, arg) =>
        {
            if (int.TryParse(arg, out var x))
            {
                model.Difficulty = (Difficulty)x;
            }
            else
            {
                return new DecodeLog(State.Warning, "#DIFFICULTYに数字が定義されていません");
            }
            return null;
        });
        public static readonly CommandWord BANNER = new(nameof(BANNER), (model, arg) =>
        {
            model.Banner = arg.Replace('\\', '/');
            return null;
        });
        public static readonly CommandWord COMMENT = new(nameof(COMMENT), (model, arg) =>
        {
            // TODO 未実装
            return null;
        });
        public static readonly CommandWord BASE = new(nameof(BASE), (model, arg) =>
        {
            if (int.TryParse(arg, out var @base))
            {
                if (@base != 62)
                {
                    return new DecodeLog(State.Warning, "#BASEに無効な数字が定義されています");
                }
                model.Base = @base;
            }
            else
            {
                return new DecodeLog(State.Warning, "#BASEに数字が定義されていません");
            }
            return null;
        });

        public static readonly CommandWord[] values = [PLAYER, GENRE, TITLE, SUBTITLE, ARTIST, SUBARTIST, PLAYLEVEL, RANK, DEFEXRANK, TOTAL, VOLWAV, STAGEFILE, BACKBMP, PREVIEW, LNOBJ, LNMODE, DIFFICULTY, BANNER, COMMENT, BASE];

        public Func<BmsModel, string, DecodeLog?> function;
        public string name;

        private CommandWord(string name, Func<BmsModel, string, DecodeLog?> function)
        {
            this.name = name;
            this.function = function;
        }
    }
}
