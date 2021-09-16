using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BmsParser
{
    static class Utility
    {
        public static void Put<TKey, TValue>(this IDictionary<TKey, TValue> dic, TKey key, TValue value)
        {
            lock (dic)
            {
                if (dic.ContainsKey(key))
                {
                    dic[key] = value;
                }
                else
                {
                    dic.Add(key, value);
                }
            }
        }

        public static bool TryParseInt36(string s, out int value)
        {
            value = 0;

            if (s.Length != 2)
                return false;

            var c1 = s[0];
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

            var c2 = s[1];
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
