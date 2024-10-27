using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BmsParser;

namespace BmsParser
{
    public class Mode
    {
        public static readonly Mode BEAT_5K = new Mode(5, "beat-5k", 1, 6, new int[] { 5 });
        public static readonly Mode BEAT_7K = new Mode(7, "beat-7k", 1, 8, new int[] { 7 });
        public static readonly Mode BEAT_10K = new Mode(10, "beat-10k", 2, 12, new int[] { 5, 11 });
        public static readonly Mode BEAT_14K = new Mode(14, "beat-14k", 2, 16, new int[] { 7, 15 });
        public static readonly Mode POPN_5K = new Mode(9, "popn-5k", 1, 5, new int[] { });
        public static readonly Mode POPN_9K = new Mode(9, "popn-9k", 1, 9, new int[] { });
        public static readonly Mode KEYBOARD_24K = new Mode(25, "keyboard-24k", 1, 26, new int[] { 24, 25 });
        public static readonly Mode KEYBOARD_24K_DOUBLE = new Mode(50, "keyboard-24k-double", 2, 52, new int[] { 24, 25, 50, 51 });

        static readonly Mode[] values = [BEAT_5K, BEAT_7K, BEAT_10K, BEAT_14K, POPN_5K, POPN_9K, KEYBOARD_24K, KEYBOARD_24K_DOUBLE];

        public int id;
        /**
         * モードの名称。bmsonのmode_hintに対応
         */
        public String hint;
        /**
         * プレイヤー数
         */
        public int player;
        /**
         * 使用するキーの数
         */
        public int key;
        /**
         * スクラッチキーアサイン
         */
        public int[] scratchKey;

        private Mode(int id, String hint, int player, int key, int[] scratchKey)
        {
            this.id = id;
            this.hint = hint;
            this.player = player;
            this.key = key;
            this.scratchKey = scratchKey;
        }

        /**
         * 指定するkeyがスクラッチキーかどうかを返す
         * 
         * @param key キー番号
         * @return スクラッチであればtrue
         */
        public bool isScratchKey(int key)
        {
            foreach (int sc in scratchKey)
            {
                if (key == sc)
                {
                    return true;
                }
            }
            return false;
        }

        /**
         * mode_hintに対応するModeを取得する
         * 
         * @param hint
         *            mode_hint
         * @return 対応するMode
         */
        public static Mode getMode(String hint)
        {
            foreach (Mode mode in values)
            {
                if (mode.hint.Equals(hint))
                {
                    return mode;
                }
            }
            return null;
        }
    }
}
