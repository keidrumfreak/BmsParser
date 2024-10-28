using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using BmsParser;

namespace BmsParser
{
    /// <summary>
    /// 譜面デコーダー
    /// </summary>
    public abstract class ChartDecoder
    {
        protected LNType lntype;

        protected List<DecodeLog> log = new List<DecodeLog>();

        /**
         * パスで指定したファイルをBMSModelに変換する
         * 
         * @param file
         *            譜面ファイル
         * @return 変換したBMSModel。失敗した場合はnull
         */
        public BmsModel decode(FileInfo file)
        {
            return Decode(file.FullName);
        }

        /**
         * パスで指定したファイルをBMSModelに変換する
         * 
         * @param path
         *            譜面ファイルのパス
         * @return 変換したBMSModel。失敗した場合はnull
         */
        public BmsModel Decode(string path)
        {
            return Decode(new ChartInformation(path, lntype, null));
        }

        /**
         * デコードログを取得する
         * 
         * @return デコードログ
         */
        public DecodeLog[] getDecodeLog()
        {
            return log.ToArray();
        }

        public abstract BmsModel Decode(ChartInformation info);

        /**
         * パスで指定したファイルに対応するChartDecoderを取得する
         * 
         * @param p
         *            譜面ファイルのパス
         * @return 対応するChartDecoder。存在しない場合はnull
         */
        public static ChartDecoder getDecoder(string p)
        {
            String s = Path.GetFileName(p).ToLower();
            if (s.EndsWith(".bms") || s.EndsWith(".bme") || s.EndsWith(".bml") || s.EndsWith(".pms"))
            {
                return new BmsDecoder(LNType.LongNote);
            }
            else if (s.EndsWith(".bmson"))
            {
                return new BmsonDecoder(LNType.LongNote);
            }
            return null;
        }

        public static int parseInt36(String s, int index)
        {

            int result = parseInt36(s[index], s[index + 1]);
            if (result == -1)
            {
                throw new FormatException();
            }
            return result;
        }

        public static int parseInt36(char c1, char c2)
        {
            int result = 0;
            if (c1 >= '0' && c1 <= '9')
            {
                result = (c1 - '0') * 36;
            }
            else if (c1 >= 'a' && c1 <= 'z')
            {
                result = ((c1 - 'a') + 10) * 36;
            }
            else if (c1 >= 'A' && c1 <= 'Z')
            {
                result = ((c1 - 'A') + 10) * 36;
            }
            else
            {
                return -1;
            }

            if (c2 >= '0' && c2 <= '9')
            {
                result += (c2 - '0');
            }
            else if (c2 >= 'a' && c2 <= 'z')
            {
                result += (c2 - 'a') + 10;
            }
            else if (c2 >= 'A' && c2 <= 'Z')
            {
                result += (c2 - 'A') + 10;
            }
            else
            {
                return -1;
            }

            return result;
        }

        public static int parseInt62(String s, int index)
        {

            int result = parseInt62(s[index], s[index + 1]);
            if (result == -1)
            {
                throw new FormatException();
            }
            return result;
        }

        public static int parseInt62(char c1, char c2)
        {
            int result = 0;
            if (c1 >= '0' && c1 <= '9')
            {
                result = (c1 - '0') * 62;
            }
            else if (c1 >= 'A' && c1 <= 'Z')
            {
                result = ((c1 - 'A') + 10) * 62;
            }
            else if (c1 >= 'a' && c1 <= 'z')
            {
                result = ((c1 - 'a') + 36) * 62;
            }
            else
            {
                return -1;
            }

            if (c2 >= '0' && c2 <= '9')
            {
                result += (c2 - '0');
            }
            else if (c2 >= 'A' && c2 <= 'Z')
            {
                result += (c2 - 'A') + 10;
            }
            else if (c2 >= 'a' && c2 <= 'z')
            {
                result += (c2 - 'a') + 36;
            }
            else
            {
                return -1;
            }

            return result;
        }

        public static String toBase62(int @decimal)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < 2; i++)
            {
                int mod = (int)(@decimal % 62);
                if (mod < 10)
                {
                    sb.Append(mod);
                }
                else if (mod < 36)
                {
                    mod = mod - 10 + 'A';
                    sb.Append((char)mod);
                }
                else if (mod < 62)
                {
                    mod = mod - 36 + 'a';
                    sb.Append((char)mod);
                }
                else
                {
                    sb.Append("0");
                }
                @decimal = (int)(@decimal / 62);
            }
            return new String(sb.ToString().Reverse().ToArray());
        }

        protected void printLog(string path)
        {
            log.ForEach(log =>
            {
                switch (log.getState())
                {
                    case DecodeLog.State.INFO:
                        //Logger.getGlobal().info(path + " : " + log.getMessage());
                        break;
                    case DecodeLog.State.WARNING:
                        //Logger.getGlobal().warning(path + " : " + log.getMessage());
                        break;
                    case DecodeLog.State.ERROR:
                        //Logger.getGlobal().severe(path + " : " + log.getMessage());
                        break;
                }
            });
        }

        public class TimeLineCache
        {

            public double time;
            public Timeline timeline;

            public TimeLineCache(double time, Timeline timeline)
            {
                this.time = time;
                this.timeline = timeline;
            }
        }
    }
}
