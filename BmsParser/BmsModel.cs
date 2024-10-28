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
                foreach (TimeLine tl in Timelines)
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
                .Any(i => t.getNote(i) != null && t.getNote(i) is LongNote ln && ln.getType() == LongNote.TYPE_UNDEFINED));

        public bool ContainsLongNote => Timelines
            .Any(tl => Enumerable.Range(0, Mode.key)
                .Any(i => tl.getNote(i) is LongNote));

        public bool ContainsMineNote => Timelines
            .Any(tl => Enumerable.Range(0, Mode.key)
                .Any(i => tl.getNote(i) is MineNote));

        public string Preview { get; set; }

        public JudgeRankType JudgeRankType { get; set; } = JudgeRankType.BmsRank;

        public TotalType TotalType { get; set; } = TotalType.Bmson;

        public int LNObj { get; set; } = -1;

        public Dictionary<string, string> Values { get; } = new();

        /**
         * 進数指定
         */
        private int @base = 36;

        private int lnmode = LongNote.TYPE_UNDEFINED;

        public BmsModel()
        {
        }

        public int compareTo(BmsModel model)
        {
            return this.title.CompareTo(model.title);
        }


        public int GetTotalNotes()
        {
            return BMSModelUtils.getTotalNotes(this);
        }

        public EventLane getEventLane()
        {
            return new EventLane(this);
        }

        public Lane[] getLanes()
        {
            Lane[] lanes = new Lane[mode.key];
            for (int i = 0; i < lanes.Length; i++)
            {
                lanes[i] = new Lane(this, i);
            }
            return lanes;
        }

        public int getLnmode()
        {
            return lnmode;
        }

        public void setLnmode(int lnmode)
        {
            this.lnmode = lnmode;
        }

        public String ToChartString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("JUDGERANK:" + JudgeRank + "\n");
            sb.Append("TOTAL:" + Total + "\n");
            if (lnmode != 0)
            {
                sb.Append("LNMODE:" + lnmode + "\n");
            }
            double nowbpm = -Double.MinValue;
            StringBuilder tlsb = new StringBuilder();
            foreach (TimeLine tl in Timelines)
            {
                tlsb.Length = 0;
                tlsb.Append(tl.getTime() + ":");
                bool write = false;
                if (nowbpm != tl.getBPM())
                {
                    nowbpm = tl.getBPM();
                    tlsb.Append("B(" + nowbpm + ")");
                    write = true;
                }
                if (tl.getStop() != 0)
                {
                    tlsb.Append("S(" + tl.getStop() + ")");
                    write = true;
                }
                if (tl.getSectionLine())
                {
                    tlsb.Append('L');
                    write = true;
                }

                tlsb.Append('[');
                for (int lane = 0; lane < mode.key; lane++)
                {
                    Note n = tl.getNote(lane);
                    if (n is NormalNote)
                    {
                        tlsb.Append('1');
                        write = true;
                    }
                    else if (n is LongNote)
                    {
                        LongNote ln = (LongNote)n;
                        if (!ln.isEnd())
                        {
                            char[] lnchars = { 'l', 'L', 'C', 'H' };
                            tlsb.Append(lnchars[ln.getType()] + ln.getMilliDuration());
                            write = true;
                        }
                    }
                    else if (n is MineNote)
                    {
                        tlsb.Append("m" + ((MineNote)n).getDamage());
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

        public int getBase()
        {
            return @base;
        }

        public void setBase(int @base)
        {
            if (@base == 62)
            {
                this.@base = @base;
            }
            else
            {
                this.@base = 36;
            }
            return;
        }
    }
}
