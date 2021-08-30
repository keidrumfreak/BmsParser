using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BmsParser
{
    /// <summary>
    /// 地雷ノート
    /// </summary>
    public class MineNote : Note
    {
        /// <summary>
        /// 地雷のダメージ量
        /// </summary>
        public double Damage { get; set; }

        public MineNote(int wav, double damage)
        {
            Wav = wav;
            Damage = damage;
        }
    }
}
