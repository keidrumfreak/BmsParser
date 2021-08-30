using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BmsParser
{
    public record Mode(int ID, string Hint, int Player, int Key, int[] ScratchKey)
    {
        public static readonly Mode Beat5K = new(5, "beat-5k", 1, 6, new[] { 5 });
        public static readonly Mode Beat7K = new(7, "beat-7k", 1, 8, new[] { 7 });
        public static readonly Mode Beat10K = new(10, "beat-10k", 2, 12, new[] { 5, 11 });
        public static readonly Mode Beat14K = new(14, "beat-14k", 2, 16, new[] { 7, 15 });
        public static readonly Mode Popn5K = new(9, "popn-5k", 1, 5, new int[] { });
        public static readonly Mode Popn9K = new(9, "popn-9k", 1, 9, new int[] { });
        public static readonly Mode Keyboard24K = new(25, "keyboard-24k", 1, 26, new[] { 24, 25 });
        public static readonly Mode Keyboard24KDouble = new(50, "keyboard-24k-double", 2, 52, new[] { 24, 25, 50, 51 });

        /// <summary>
        /// 指定するkeyがスクラッチキーかどうかを返す
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool IsScratchKey(int key) => ScratchKey.Contains(key);

        static IEnumerable<Mode> modes = new[] { Beat5K, Beat7K, Beat10K, Beat14K, Popn5K, Popn9K, Keyboard24K, Keyboard24KDouble };

        public static Mode GetMode(string hint) => modes.FirstOrDefault(m => m.Hint == hint);
    }
}
