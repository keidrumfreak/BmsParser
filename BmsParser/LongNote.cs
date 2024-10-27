using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace BmsParser
{
    public class LongNote : Note
    {
        /**
             * ロングノート終端かどうか
             */
        private bool end;
        /**
         * ペアになっているロングノート
         */
        private LongNote pair;
        /**
         * ロングノートの種類
         */
        private int type;

        /**
         * ロングノートの種類:未定義
         */
        public static readonly int TYPE_UNDEFINED = 0;
        /**
         * ロングノートの種類:ロングノート
         */
        public static readonly int TYPE_LONGNOTE = 1;
        /**
         * ロングノートの種類:チャージノート
         */
        public static readonly int TYPE_CHARGENOTE = 2;
        /**
         * ロングノートの種類:ヘルチャージノート
         */
        public static readonly int TYPE_HELLCHARGENOTE = 3;

        /**
         * 指定のTimeLineを始点としたロングノートを作成する
         * @param start
         */
        public LongNote(int wav)
        {
            this.setWav(wav);
        }

        public LongNote(int wav, long starttime, long duration)
        {
            this.setWav(wav);
            this.setMicroStarttime(starttime);
            this.setMicroDuration(duration);
        }

        public int getType()
        {
            return type;
        }

        public void setType(int type)
        {
            this.type = type;
        }

        public void setPair(LongNote pair)
        {
            pair.pair = this;
            this.pair = pair;

            pair.end = pair.getSection() > this.getSection();
            this.end = !pair.end;
            type = pair.type = (type != TYPE_UNDEFINED ? type : pair.type);
        }

        public LongNote getPair()
        {
            return pair;
        }

        public bool isEnd()
        {
            return end;
        }
    }
}
