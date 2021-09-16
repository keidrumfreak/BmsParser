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
    }
}
