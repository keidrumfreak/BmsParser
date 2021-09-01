using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BmsParser
{
    /// <summary>
    /// 譜面デコーダー
    /// </summary>
    public abstract class ChartDecoder
    {
        protected LNType LNType { get; set; }

        protected List<DecodeLog> logs = new();

        public IEnumerable<DecodeLog> DecodeLog => logs.ToArray();

        /// <summary>
        /// パスで指定したファイルをBmsModelに変換する
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public BmsModel Decode(string path) => Decode(new ChartInformation(path, LNType, null));

        public abstract BmsModel Decode(ChartInformation info);

        public static bool TryParseInt36(string s, int index, out int value)
        {
            value = 0;

            if (s.Length < index + 2)
                return false;

            var c1 = s[index];
            if ('0' <= c1 && c1 <= '9')
            {
                value += (c1 - '0') * 36;
            }
            else if ('a' <= c1 && c1 <= 'z')
            {
                value += (c1 - 'a' + 10) * 36;
            }
            else if ('A' <= c1 && c1 <= 'Z')
            {
                value += (c1 - 'A' + 10) * 36;
            }
            else
            {
                return false;
            }

            var c2 = s[index + 1];
            if ('0' <= c2 && c2 <= '9')
            {
                value += c2 - '0';
            }
            else if ('a' <= c2 && c2 <= 'z')
            {
                value += c2 - 'a' + 10;
            }
            else if ('A' <= c2 && c2 <= 'Z')
            {
                value += c2 - 'A' + 10;
            }
            else
            {
                return false;
            }

            return true;
        }
    }
}
