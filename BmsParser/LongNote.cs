using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BmsParser
{
    /// <summary>
    /// ロングノートの種類
    /// </summary>
    public enum LNMode
    {
        /// <summary>
        /// 未定義
        /// </summary>
        Undefined = 0,
        /// <summary>
        /// ロングノート
        /// </summary>
        LongNote = 1,
        /// <summary>
        /// チャージノート
        /// </summary>
        ChargeNote = 2,
        /// <summary>
        /// ヘルチャージノート
        /// </summary>
        HellChargeNote = 3
    }

    public class LongNote : Note
    {
        /// <summary>
        /// ロングノートの種類
        /// </summary>
        public LNMode Type { get; set; }

        LongNote pair;
        /// <summary>
        /// ペアになっているロングノート
        /// </summary>
        public LongNote Pair
        { 
            get { return pair; }
            set
            {
                value.pair = this;
                pair = value;

                pair.IsEnd = pair.Section > Section;
                IsEnd = !pair.IsEnd;
                Type = (Type != LNMode.Undefined ? Type : pair.Type);
                pair.Type = Type;
            }
        }

        /// <summary>
        /// 終端かどうか
        /// </summary>
        public bool IsEnd { get; private set; }

        public LongNote(int wav)
        {
            Wav = wav;
        }

        public LongNote(int wav, long startTime, long duration)
        {
            Wav = wav;
            StartTimeMicrosecond = startTime;
            DurationMicrosecond = duration;
        }
    }
}
