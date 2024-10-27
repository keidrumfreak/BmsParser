﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using BmsParser;

namespace BmsParser
{
    internal class BMSModelUtils
    {
        public const int TOTALNOTES_ALL = 0;
        public const int TOTALNOTES_KEY = 1;
        public const int TOTALNOTES_LONG_KEY = 2;
        public const int TOTALNOTES_SCRATCH = 3;
        public const int TOTALNOTES_LONG_SCRATCH = 4;
        public const int TOTALNOTES_MINE = 5;

        /**
         * 総ノート数を返す。
         * 
         * @return 総ノート数
         */
        public static int getTotalNotes(BmsModel model)
        {
            return getTotalNotes(model, 0, int.MaxValue);
        }

        public static int getTotalNotes(BmsModel model, int type)
        {
            return getTotalNotes(model, 0, int.MaxValue, type);
        }

        /**
         * 指定の時間範囲の総ノート数を返す
         * 
         * @param start
         *            開始時間(ms)
         * @param end
         *            終了時間(ms)
         * @return 指定の時間範囲の総ノート数
         */
        public static int getTotalNotes(BmsModel model, int start, int end)
        {
            return getTotalNotes(model, start, end, TOTALNOTES_ALL);
        }

        /**
         * 指定の時間範囲、指定の種類のノートの総数を返す
         * 
         * @param start
         *            開始時間(ms)
         * @param end
         *            終了時間(ms)
         * @param type
         *            ノートの種類
         * @return 指定の時間範囲、指定の種類のの総ノート数
         */
        public static int getTotalNotes(BmsModel model, int start, int end, int type)
        {
            return getTotalNotes(model, start, end, type, 0);
        }

        /**
         * 指定の時間範囲、指定の種類、指定のプレイサイドのノートの総数を返す
         * 
         * @param start
         *            開始時間(ms)
         * @param end
         *            終了時間(ms)
         * @param type
         *            ノートの種類
         * @param side
         *            プレイサイド(0:両方, 1:1P側, 2:2P側)
         * @return 指定の時間範囲、指定の種類のの総ノート数
         */
        public static int getTotalNotes(BmsModel model, int start, int end, int type, int side)
        {
            Mode mode = model.getMode();
            if (mode.player == 1 && side == 2)
            {
                return 0;
            }
            int[] slane = new int[mode.scratchKey.Length / (side == 0 ? 1 : mode.player)];
            for (int i = (side == 2 ? slane.Length : 0), index = 0; index < slane.Length; i++)
            {
                slane[index] = mode.scratchKey[i];
                index++;
            }
            int[] nlane = new int[(mode.key - mode.scratchKey.Length) / (side == 0 ? 1 : mode.player)];
            for (int i = 0, index = 0; index < nlane.Length; i++)
            {
                if (!mode.isScratchKey(i))
                {
                    nlane[index] = i;
                    index++;
                }
            }

            int count = 0;
            foreach (TimeLine tl in model.getAllTimeLines())
            {
                if (tl.getTime() >= start && tl.getTime() < end)
                {
                    switch (type)
                    {
                        case TOTALNOTES_ALL:
                            count += tl.getTotalNotes(model.getLntype());
                            break;
                        case TOTALNOTES_KEY:
                            foreach (int lane in nlane)
                            {
                                if (tl.existNote(lane) && (tl.getNote(lane) is NormalNote))
                                {
                                    count++;
                                }
                            }
                            break;
                        case TOTALNOTES_LONG_KEY:
                            foreach (int lane in nlane)
                            {
                                if (tl.existNote(lane) && (tl.getNote(lane) is LongNote))
                                {
                                    LongNote ln = (LongNote)tl.getNote(lane);
                                    if (ln.getType() == LongNote.TYPE_CHARGENOTE
                                            || ln.getType() == LongNote.TYPE_HELLCHARGENOTE
                                            || (ln.getType() == LongNote.TYPE_UNDEFINED && model.getLntype() != BmsModel.LNTYPE_LONGNOTE)
                                            || !ln.isEnd())
                                    {
                                        count++;
                                    }
                                }
                            }
                            break;

                        case TOTALNOTES_SCRATCH:
                            foreach (int lane in slane)
                            {
                                if (tl.existNote(lane) && (tl.getNote(lane) is NormalNote))
                                {
                                    count++;
                                }
                            }
                            break;

                        case TOTALNOTES_LONG_SCRATCH:
                            foreach (int lane in slane)
                            {
                                Note n = tl.getNote(lane);
                                if (n is LongNote)
                                {
                                    LongNote ln = (LongNote)n;
                                    if (ln.getType() == LongNote.TYPE_CHARGENOTE
                                            || ln.getType() == LongNote.TYPE_HELLCHARGENOTE
                                            || (ln.getType() == LongNote.TYPE_UNDEFINED && model.getLntype() != BmsModel.LNTYPE_LONGNOTE)
                                            || !ln.isEnd())
                                    {
                                        count++;
                                    }
                                }
                            }
                            break;

                        case TOTALNOTES_MINE:
                            foreach (int lane in nlane)
                            {
                                if (tl.existNote(lane) && (tl.getNote(lane) is MineNote))
                                {
                                    count++;
                                }
                            }
                            foreach (int lane in slane)
                            {
                                if (tl.existNote(lane) && (tl.getNote(lane) is MineNote))
                                {
                                    count++;
                                }
                            }
                            break;
                    }
                }
            }
            return count;
        }

        public double getAverageNotesPerTime(BmsModel model, int start, int end)
        {
            return (double)getTotalNotes(model, start, end) * 1000 / (end - start);
        }

        public static void changeFrequency(BmsModel model, float freq)
        {
            model.setBpm(model.getBpm() * freq);
            foreach (TimeLine tl in model.getAllTimeLines())
            {
                tl.setBPM(tl.getBPM() * freq);
                tl.setStop((long)(tl.getMicroStop() / freq));
                tl.setMicroTime((long)(tl.getMicroTime() / freq));
            }
        }

        public static double getMaxNotesPerTime(BmsModel model, int range)
        {
            int maxnotes = 0;
            TimeLine[] tl = model.getAllTimeLines();
            for (int i = 0; i < tl.Length; i++)
            {
                int notes = 0;
                for (int j = i; j < tl.Length && tl[j].getTime() < tl[i].getTime() + range; j++)
                {
                    notes += tl[j].getTotalNotes(model.getLntype());
                }
                maxnotes = (maxnotes < notes) ? notes : maxnotes;
            }
            return maxnotes;
        }

        public static long setStartNoteTime(BmsModel model, long starttime)
        {
            long marginTime = 0;
            foreach (TimeLine tl in model.getAllTimeLines())
            {
                if (tl.getMilliTime() >= starttime)
                {
                    break;
                }
                if (tl.existNote())
                {
                    marginTime = starttime - tl.getMilliTime();
                    break;
                }
            }

            if (marginTime > 0)
            {
                double marginSection = marginTime * model.getAllTimeLines()[0].getBPM() / 240000;
                foreach (TimeLine tl in model.getAllTimeLines())
                {
                    tl.setSection(tl.getSection() + marginSection);
                    tl.setMicroTime(tl.getMicroTime() + marginTime * 1000);
                }

                TimeLine[] tl2 = new TimeLine[model.getAllTimeLines().Length + 1];
                tl2[0] = new TimeLine(0, 0, model.getMode().key);
                tl2[0].setBPM(model.getBpm());
                for (int i = 1; i < tl2.Length; i++)
                {
                    tl2[i] = model.getAllTimeLines()[i - 1];
                }
                model.setAllTimeLine(tl2);
            }

            return marginTime;
        }
    }
}
