using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using BmsParser;

namespace BmsParser
{
    public enum LNType
    {
        LongNote = 0,
        ChargeNote = 1,
        HellChargeNote = 2
    }

    public enum Difficulty
    {
        Beginner = 0,
        Normal = 1,
        Hyper = 2,
        Another = 3,
        Insane = 4
    }

    public enum JudgeRankType { BmsRank, BmsDefEXRank, BmsonJudgeRank }

    public enum TotalType { Bms, Bmson }

    public class BmsModel
    {
        /// <summary>
        /// プレイヤー数
        /// </summary>
        public int Player { get; set; }

        string title = string.Empty;
        /// <summary>
        /// タイトル名
        /// </summary>
        public string Title { get => title; set => title = value ?? string.Empty; }

        string subtitle = string.Empty;
        /// <summary>
        /// サブタイトル名
        /// </summary>
        public string Subtitle { get => subtitle; set => subtitle = value ?? string.Empty; }

        string genre = string.Empty;
        /// <summary>
        /// ジャンル名
        /// </summary>
        public string Genre { get => genre; set => genre = value ?? string.Empty; }

        string artist;
        /// <summary>
        /// アーティスト
        /// </summary>
        public string Artist { get => artist; set => artist = value ?? string.Empty; }

        string subartist = string.Empty;
        /// <summary>
        /// サブアーティスト
        /// </summary>
        public string Subartist { get => subartist; set => subartist = value ?? string.Empty; }

        string banner = string.Empty;
        /// <summary>
        /// バナー
        /// </summary>
        public string Banner { get => banner; set => banner = value ?? string.Empty; }

        /// <summary>
        /// 標準BPM
        /// </summary>
        public double Bpm { get; set; }

        /// <summary>
        /// 表記レベル
        /// </summary>
        public string PlayLevel { get; set; } = string.Empty;

        /// <summary>
        /// 判定ランク
        /// </summary>
        public int JudgeRank { get; set; } = 2;

        /// <summary>
        /// TOTAL値
        /// </summary>
        public double Total { get; set; } = 100;

        /// <summary>
        /// 標準ボリューム
        /// </summary>
        public int VolWav { get; set; }

        public double MinBpm => new[] { Bpm }.Concat(Timelines.Select(t => t.getBPM())).Min();

        public double MaxBpm => new[] { Bpm }.Concat(Timelines.Select(t => t.getBPM())).Max();

        /// <summary>
        /// 時間とTimeLineのマッピング
        /// </summary>
        public TimeLine[] Timelines { get; set; } = [];

        public IEnumerable<int> AllTimes => Timelines.Select(t => t.getTime()).ToArray();

        public long LastNoteMilliTime
        {
            get
            {
                var t = Timelines.Where(tl => Enumerable.Range(0, mode.key).Any(lane => tl.existNote(lane)));
                return t.Any() ? t.Max(tl => tl.getMilliTime()) : 0;
            }
        }

        public int LastNoteTime => (int)LastNoteMilliTime;

        public long LastMilliTime
        {
            get
            {
                var t = Timelines.Where(tl => Enumerable.Range(0, mode.key)
                .Any(lane => tl.existNote(lane)
                || tl.getHiddenNote(lane) != null
                || tl.getBackGroundNotes().Length > 0
                || tl.getBGA() != -1
                || tl.getLayer() != -1));
                return t.Any() ? t.Max(tl => tl.getMilliTime()) : 0;
            }
        }

        public int LastTime => (int)LastMilliTime;

        /// <summary>
        /// 表記ランク
        /// </summary>
        public Difficulty Difficulty { get; set; }

        public string FullTitle => string.IsNullOrEmpty(Subtitle) ? Title : $"{Title} {Subtitle}";

        public string FullArtist => string.IsNullOrEmpty(Subartist) ? Artist : $"{Artist} {Subartist}";

        /// <summary>
        /// MD5値
        /// </summary>
        public string MD5 { get; set; }

        /// <summary>
        /// Sha256値
        /// </summary>
        public string Sha256 { get; set; }

        Mode mode;
        /// <summary>
        /// 使用するキー数
        /// </summary>
        public Mode Mode
        {
            get => mode;
            set
            {
                mode = value;
                foreach (var tl in Timelines)
                {
                    tl.setLaneCount(mode.key);
                }
            }
        }

        /// <summary>
        /// WAV定義のIDとファイル名のマップ
        /// </summary>
        public string[] WavList { get; set; } = [];

        /// <summary>
        /// BGA定義のIDとファイル名のマップ
        /// </summary>
        public string[] BgaList { get; set; } = [];

        public ChartInformation ChartInformation { get; set; }

        public int[] Random => ChartInformation?.SelectedRandoms;

        public string Path => ChartInformation?.Path;

        public LNType LNType => ChartInformation?.LNType ?? LNType.LongNote;

        string stageFile = string.Empty;
        /// <summary>
        /// ステージ画像
        /// </summary>
        public string StageFile { get => stageFile; set => stageFile = value ?? string.Empty; }

        string backBmp = string.Empty;
        public string BackBmp { get => backBmp; set => backBmp = value ?? string.Empty; }

        public bool ContainsUndefinedLongNote => Timelines
            .Any(t => Enumerable.Range(0, Mode.key)
                .Any(i => t.getNote(i) != null && t.getNote(i) is LongNote ln && ln.Type == LNMode.Undefined));

        public bool ContainsLongNote => Timelines
            .Any(tl => Enumerable.Range(0, Mode.key)
                .Any(i => tl.getNote(i) is LongNote));

        public bool ContainsMineNote => Timelines
            .Any(tl => Enumerable.Range(0, Mode.key)
                .Any(i => tl.getNote(i) is MineNote));

        public string Preview { get; set; }

        public LNMode LNMode { get; set; } = LNMode.Undefined;

        public JudgeRankType JudgeRankType { get; set; } = JudgeRankType.BmsRank;

        public TotalType TotalType { get; set; } = TotalType.Bmson;

        public int LNObj { get; set; } = -1;

        public Dictionary<string, string> Values { get; } = [];

        public EventLane EventLane => new(this);

        public Lane[] Lanes => Enumerable.Range(0, Mode.key).Select(i => new Lane(this, i)).ToArray();

        /// <summary>
        /// 進数指定
        /// </summary>
        public int Base { get; set; } = 36;

        public static BmsModel Decode(string path)
        {
            if (path.EndsWith("bmson"))
            {
                return new BmsonDecoder().Decode(path);
            }
            else
            {
                return new BmsDecoder().Decode(path);
            }
        }

        public static BmsModel Decode(string path, byte[] bin)
        {
            if (path.EndsWith("bmson"))
            {
                return new BmsonDecoder().Decode(path, bin);
            }
            else
            {
                return new BmsDecoder().Decode(path, bin);
            }
        }

        public int CompareTo(BmsModel model)
        {
            return Title.CompareTo(model.title);
        }

        public string ToChartString()
        {
            var sb = new StringBuilder();
            sb.Append($"JUDGERANK:{JudgeRank}\n");
            sb.Append($"TOTAL:{Total}\n");
            if (LNMode != LNMode.Undefined)
                sb.Append($"LNMODE:{LNMode}\n");
            var nowBpm = -double.MinValue;
            var tlsb = new StringBuilder();
            foreach (var tl in Timelines)
            {
                tlsb.Clear();
                tlsb.Append($"{tl.getTime()}:");
                var write = false;
                if (nowBpm != tl.getBPM())
                {
                    nowBpm = tl.getBPM();
                    tlsb.Append($"B({nowBpm})");
                    write = true;
                }
                if (tl.getStop() != 0)
                {
                    tlsb.Append($"S({tl.getStop()})");
                    write = true;
                }
                if (tl.getSectionLine())
                {
                    tlsb.Append('L');
                    write = true;
                }

                tlsb.Append('[');
                for (var lane = 0; lane < mode.key; lane++)
                {
                    var n = tl.getNote(lane);
                    if (n is NormalNote)
                    {
                        tlsb.Append('1');
                        write = true;
                    }
                    else if (n is LongNote ln)
                    {
                        if (!ln.IsEnd)
                        {
                            char[] lnchars = ['l', 'L', 'C', 'H'];
                            tlsb.Append(lnchars[(int)ln.Type] + ln.getMilliDuration());
                            write = true;
                        }
                    }
                    else if (n is MineNote mine)
                    {
                        tlsb.Append("m" + mine.getDamage());
                        write = true;
                    }
                    else
                    {
                        tlsb.Append('0');
                    }
                    if (lane < mode.key - 1)
                    {
                        tlsb.Append(',');
                    }
                }
                tlsb.Append("]\n");

                if (write)
                {
                    sb.Append(tlsb);
                }
            }
            return sb.ToString();
        }

        public enum NoteType { All, Key, LongKey, Scratch, LongScratch, Mine }
        public enum PlaySide { Both, P1, P2 }

        public int GetTotalNotes(NoteType type = NoteType.All, int start = 0, int end = int.MaxValue, PlaySide side = PlaySide.Both)
        {
            if (Mode.player == 1 && side == PlaySide.P2)
                return 0;
            var slane = new int[Mode.scratchKey.Length / (side == PlaySide.Both ? 1 : Mode.player)];
            var sindex = 0;
            for (var i = side == PlaySide.P2 ? slane.Length : 0; sindex < slane.Length; i++)
            {
                slane[sindex] = Mode.scratchKey[i];
                sindex++;
            }
            var nlane = new int[(Mode.key - Mode.scratchKey.Length) / (side == PlaySide.Both ? 1 : Mode.player)];
            var nindex = 0;
            for (var i = 0; nindex < nlane.Length; i++)
            {
                if (!Mode.isScratchKey(i))
                {
                    nlane[nindex] = i;
                    nindex++;
                }
            }

            return Timelines.Where(tl => start <= tl.getTime() && tl.getTime() < end)
                .Sum(tl => type switch
                {
                    NoteType.All => tl.getTotalNotes(LNType),
                    NoteType.Key => nlane.Count(lane => tl.existNote(lane) && tl.getNote(lane) is NormalNote),
                    NoteType.LongKey => nlane
                        .Count(lane => tl.existNote(lane) && tl.getNote(lane) is LongNote ln
                            && (ln.Type == LNMode.LongNote
                                || ln.Type == LNMode.ChargeNote
                                || ln.Type == LNMode.HellChargeNote
                                || ln.Type == LNMode.Undefined && LNType != LNType.LongNote
                                || !ln.IsEnd)),
                    NoteType.Scratch => slane.Count(lane => tl.existNote(lane) && tl.getNote(lane) is NormalNote),
                    NoteType.LongScratch => slane
                        .Count(lane => tl.existNote(lane) && tl.getNote(lane) is LongNote ln
                            && (ln.Type == LNMode.LongNote
                                || ln.Type == LNMode.ChargeNote
                                || ln.Type == LNMode.HellChargeNote
                                || ln.Type == LNMode.Undefined && LNType != LNType.LongNote
                                || !ln.IsEnd)),
                    NoteType.Mine => nlane.Count(lane => tl.existNote(lane) && tl.getNote(lane) is MineNote)
                        + slane.Count(lane => tl.existNote(lane) && tl.getNote(lane) is MineNote),
                    _ => 0
                });
        }
    }
}
