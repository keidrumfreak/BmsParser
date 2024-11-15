using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using BmsParser;
using BmsParser.Bmson;

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

    public class BmsModel(Mode mode)
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

        string artist = string.Empty;
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

        public double MinBpm => new[] { Bpm }.Concat(Timelines.Select(t => t.Bpm)).Min();

        public double MaxBpm => new[] { Bpm }.Concat(Timelines.Select(t => t.Bpm)).Max();

        /// <summary>
        /// 時間とTimeLineのマッピング
        /// </summary>
        public Timeline[] Timelines { get; set; } = [];

        public IEnumerable<int> AllTimes => Timelines.Select(t => t.Time).ToArray();

        public long LastNoteMilliTime
        {
            get
            {
                var t = Timelines.Where(tl => Enumerable.Range(0, mode.Key).Any(lane => tl.ExistNote(lane)));
                return t.Any() ? t.Max(tl => tl.MilliTime) : 0;
            }
        }

        public int LastNoteTime => (int)LastNoteMilliTime;

        public long LastMilliTime
        {
            get
            {
                var t = Timelines.Where(tl => Enumerable.Range(0, mode.Key)
                .Any(lane => tl.ExistNote(lane)
                || tl.GetHiddenNote(lane) != null
                || tl.BackGroundNotes.Length > 0
                || tl.BgaID != -1
                || tl.LayerID != -1));
                return t.Any() ? t.Max(tl => tl.MilliTime) : 0;
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
        public string MD5 { get; set; } = string.Empty;

        /// <summary>
        /// Sha256値
        /// </summary>
        public string Sha256 { get; set; } = string.Empty;

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
                    tl.LaneCount = mode.Key;
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

        public ChartInformation? ChartInformation { get; set; }

        public int[] Random => ChartInformation?.SelectedRandoms ?? [];

        public string Path => ChartInformation?.Path ?? string.Empty;

        public LNType LNType => ChartInformation?.LNType ?? LNType.LongNote;

        string stageFile = string.Empty;
        /// <summary>
        /// ステージ画像
        /// </summary>
        public string StageFile { get => stageFile; set => stageFile = value ?? string.Empty; }

        string backBmp = string.Empty;
        public string BackBmp { get => backBmp; set => backBmp = value ?? string.Empty; }

        public bool ContainsUndefinedLongNote => Timelines
            .Any(t => Enumerable.Range(0, Mode.Key)
                .Any(i => t.GetNote(i) != null && t.GetNote(i) is LongNote ln && ln.Type == LNMode.Undefined));

        public bool ContainsLongNote => Timelines
            .Any(tl => Enumerable.Range(0, Mode.Key)
                .Any(i => tl.GetNote(i) is LongNote));

        public bool ContainsMineNote => Timelines
            .Any(tl => Enumerable.Range(0, Mode.Key)
                .Any(i => tl.GetNote(i) is MineNote));

        public string Preview { get; set; } = string.Empty;

        public LNMode LNMode { get; set; } = LNMode.Undefined;

        public JudgeRankType JudgeRankType { get; set; } = JudgeRankType.BmsRank;

        public TotalType TotalType { get; set; } = TotalType.Bmson;

        public int LNObj { get; set; } = -1;

        public Dictionary<string, string> Values { get; } = [];

        public EventLane EventLane => new(this);

        public Lane[] Lanes => Enumerable.Range(0, Mode.Key).Select(i => new Lane(this, i)).ToArray();

        /// <summary>
        /// 進数指定
        /// </summary>
        public int Base { get; set; } = 36;

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
                sb.Append($"LNMODE:{(int)LNMode}\n");
            var nowBpm = -double.MinValue;
            var tlsb = new StringBuilder();
            foreach (var tl in Timelines)
            {
                tlsb.Clear();
                tlsb.Append($"{tl.Time}:");
                var write = false;
                if (nowBpm != tl.Bpm)
                {
                    nowBpm = tl.Bpm;
                    tlsb.Append($"B({nowBpm})");
                    write = true;
                }
                if (tl.Stop != 0)
                {
                    tlsb.Append($"S({tl.Stop})");
                    write = true;
                }
                if (tl.IsSectionLine)
                {
                    tlsb.Append('L');
                    write = true;
                }

                tlsb.Append('[');
                for (var lane = 0; lane < mode.Key; lane++)
                {
                    var n = tl.GetNote(lane);
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
                            tlsb.Append(lnchars[(int)ln.Type] + ln.MilliDuration);
                            write = true;
                        }
                    }
                    else if (n is MineNote mine)
                    {
                        tlsb.Append($"m{mine.Damage}");
                        write = true;
                    }
                    else
                    {
                        tlsb.Append('0');
                    }
                    if (lane < mode.Key - 1)
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
            if (Mode.Player == 1 && side == PlaySide.P2)
                return 0;
            var slane = new int[Mode.ScratchKey.Length / (side == PlaySide.Both ? 1 : Mode.Player)];
            var sindex = 0;
            for (var i = side == PlaySide.P2 ? slane.Length : 0; sindex < slane.Length; i++)
            {
                slane[sindex] = Mode.ScratchKey[i];
                sindex++;
            }
            var nlane = new int[(Mode.Key - Mode.ScratchKey.Length) / (side == PlaySide.Both ? 1 : Mode.Player)];
            var nindex = 0;
            for (var i = 0; nindex < nlane.Length; i++)
            {
                if (!Mode.IsScratchKey(i))
                {
                    nlane[nindex] = i;
                    nindex++;
                }
            }

            return Timelines.Where(tl => start <= tl.Time && tl.Time < end)
                .Sum(tl => type switch
                {
                    NoteType.All => tl.GetTotalNotes(LNType),
                    NoteType.Key => nlane.Count(lane => tl.ExistNote(lane) && tl.GetNote(lane) is NormalNote),
                    NoteType.LongKey => nlane
                        .Count(lane => tl.ExistNote(lane) && tl.GetNote(lane) is LongNote ln
                            && (ln.Type == LNMode.LongNote
                                || ln.Type == LNMode.ChargeNote
                                || ln.Type == LNMode.HellChargeNote
                                || ln.Type == LNMode.Undefined && LNType != LNType.LongNote
                                || !ln.IsEnd)),
                    NoteType.Scratch => slane.Count(lane => tl.ExistNote(lane) && tl.GetNote(lane) is NormalNote),
                    NoteType.LongScratch => slane
                        .Count(lane => tl.ExistNote(lane) && tl.GetNote(lane) is LongNote ln
                            && (ln.Type == LNMode.LongNote
                                || ln.Type == LNMode.ChargeNote
                                || ln.Type == LNMode.HellChargeNote
                                || ln.Type == LNMode.Undefined && LNType != LNType.LongNote
                                || !ln.IsEnd)),
                    NoteType.Mine => nlane.Count(lane => tl.ExistNote(lane) && tl.GetNote(lane) is MineNote)
                        + slane.Count(lane => tl.ExistNote(lane) && tl.GetNote(lane) is MineNote),
                    _ => 0
                });
        }
    }
}
