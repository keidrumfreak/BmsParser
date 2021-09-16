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

        public IEnumerable<DecodeLog> DecodeLogs => logs.ToArray();

        /// <summary>
        /// パスで指定したファイルをBmsModelに変換する
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public BmsModel Decode(string path) => Decode(new ChartInformation(path, LNType, null));

        public abstract BmsModel Decode(ChartInformation info);
    }
}
