using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        /// <summary>
        /// タイトル名
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// サブタイトル名
        /// </summary>
        public string SubTitle { get; set; } = string.Empty;

        /// <summary>
        /// ジャンル名
        /// </summary>
        public string Genre { get; set; } = string.Empty;

        /// <summary>
        /// アーティスト
        /// </summary>
        public string Artist { get; set; } = string.Empty;

        /// <summary>
        /// サブアーティスト
        /// </summary>
        public string SubArtist { get; set; } = string.Empty;

        /// <summary>
        /// バナー
        /// </summary>
        public string Banner { get; set; } = string.Empty;

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

        public double MinBpm => new[] { Bpm }.Concat(TimeLines.Select(t => t.Bpm)).Min();

        public double MaxBpm => new[] { Bpm }.Concat(TimeLines.Select(t => t.Bpm)).Max();

        /// <summary>
        /// 時間とTimeLineのマッピング
        /// </summary>
        public TimeLine[] TimeLines { get; set; } = Array.Empty<TimeLine>();

        public IEnumerable<long> AllTimes => TimeLines.Select(t => t.Time).ToArray();

        public long LastNoteMilliTime => TimeLines
            .LastOrDefault(tl => Enumerable.Range(0, Mode.Key)
                .Any(lane => tl.ExistNote(lane)))?.TimeMilliSeccond ?? 0;

        public int LastTime => (int)LastNoteMilliTime;

        /// <summary>
        /// 表記ランク
        /// </summary>
        public Difficulty Difficulty { get; set; }

        public string FullTitle => string.IsNullOrEmpty(SubTitle) ? Title : $"{Title} {SubTitle}";

        public string FullArtist => string.IsNullOrEmpty(SubArtist) ? Artist : $"{Artist} {SubArtist}";

        /// <summary>
        /// MD5値
        /// </summary>
        public string MD5 { get; set; }

        /// <summary>
        /// Sha256値
        /// </summary>
        public string Sha256 { get; set; }

        /// <summary>
        /// 使用するキー数
        /// </summary>
        public Mode Mode { get; set; }

        /// <summary>
        /// WAV定義のIDとファイル名のマップ
        /// </summary>
        public string[] WavList { get; set; } = Array.Empty<string>();

        /// <summary>
        /// BGA定義のIDとファイル名のマップ
        /// </summary>
        public string[] BgaList { get; set; } = Array.Empty<string>();

        public ChartInformation ChartInformation { get; set; }

        public int[] Random => ChartInformation?.SelectedRandoms;

        public string Path => ChartInformation?.Path;

        public LNType LNType => ChartInformation?.LNType ?? LNType.LongNote;

        /// <summary>
        /// ステージ画像
        /// </summary>
        public string StageFile { get; set; } = string.Empty;

        public string BackBmp { get; set; } = string.Empty;

        public bool ContainsUndefinedLongNote => TimeLines
            .Any(t => Enumerable.Range(0, Mode.Key)
                .Any(i => t.GetNote(i) != null && t.GetNote(i) is LongNote ln && ln.Type == LNMode.Undefined));

        public bool ContainsLongNote => TimeLines
            .Any(tl => Enumerable.Range(0, Mode.Key)
                .Any(i => tl.GetNote(i) is LongNote));

        public bool ContainsMineNote => TimeLines
            .Any(tl => Enumerable.Range(0, Mode.Key)
                .Any(i => tl.GetNote(i) is MineNote));

        public string Preview { get; set; }

        public LNMode LNMode { get; set; }

        public JudgeRankType JudgeRankType { get; set; }

        public TotalType TotalType { get; set; }

        public int LNObj { get; set; } = -1;

        public Dictionary<string, string> Values { get; } = new();

        public string ToChartString()
        {
            var sb = new StringBuilder();
            sb.Append($"JUDGERANK:{JudgeRank}\n");
            sb.Append($"TOTAL:{Total}\n");
            if (LNMode != LNMode.Undefined)
                sb.Append($"LNMODE:{LNMode}\n");
            var nowBpm = -double.MinValue;
            var tlsb = new StringBuilder();
            foreach (var tl in TimeLines)
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
                if (tl.StopTime != 0)
                {
                    tlsb.Append($"S({tl.StopTime})");
                    write = true;
                }
                if (tl.IsSectionLine)
                {
                    tlsb.Append('L');
                    write = true;
                }
                tlsb.Append('[');
                for (int lane = 0; lane < Mode.Key; lane++)
                {
                    Note n = tl.GetNote(lane);
                    if (n is NormalNote)
                    {
                        tlsb.Append('1');
                        write = true;
                    }
                    else if (n is LongNote ln)
                    {
                        if (!ln.IsEnd)
                        {
                            var lnchars = new[] { 'l', 'L', 'C', 'H' };
                            tlsb.Append(lnchars[(int)ln.Type] + ln.Duration);
                            write = true;
                        }
                    }
                    else if (n is MineNote mine)
                    {
                        tlsb.Append("m" + mine.Damage);
                        write = true;
                    }
                    else
                    {
                        tlsb.Append('0');
                    }
                    if (lane < Mode.Key - 1)
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

            return TimeLines.Where(tl => start <= tl.Time && tl.Time < end)
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
