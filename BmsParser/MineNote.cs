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
        /**
             * 地雷のダメージ量
             */
        private double damage;

        public MineNote(int wav, double damage)
        {
            Wav = wav;
            this.setDamage(damage);
        }

        /**
         * 地雷ノーツのダメージ量を取得する
         * @return 地雷ノーツのダメージ量
         */
        public double getDamage()
        {
            return damage;
        }

        /**
         * 地雷ノーツのダメージ量を設定する
         * @param damage 地雷ノーツのダメージ量
         */
        public void setDamage(double damage)
        {
            this.damage = damage;
        }
    }
}
