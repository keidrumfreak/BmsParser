using System;
using System.Collections.Generic;
using System.Linq;

namespace BmsParser
{
    /// <summary>
    /// タイムライン
    /// </summary>
    public class Timeline(double section, long time, int noteSize)
    {
        /// <summary>
        /// タイムラインの時間(us)
        /// </summary>
        public long MicroTime
        {
            get => time;
            set
            {
                time = value;
                foreach(var note in notes.Where(n => n != null))
                {
                    note.MicroTime = value;
                }
                foreach (var note in hiddenNotes.Where(n => n != null))
                {
                    note.MicroTime = value;
                }
                foreach (var note in bgNotes.Where(n => n != null))
                {
                    note.MicroTime = value;
                }
            }
        }

        public long MilliTime => MicroTime / 1000;

        public int Time => (int)(MicroTime / 1000);

        /// <summary>
        /// タイムラインの小節
        /// </summary>
        public double Section
        {
            get => section;
            set
            {
                section = value;
                foreach (var note in notes.Where(n => n != null))
                {
                    note.Section = value;
                }
                foreach (var note in hiddenNotes.Where(n => n != null))
                {
                    note.Section = value;
                }
                foreach (var note in bgNotes.Where(n => n != null))
                {
                    note.Section = value;
                }
            }
        }

        public int Stop => (int)(MicroStop / 1000);

        public long MilliStop => MicroStop / 1000;

        /// <summary>
        /// ストップ時間(us)
        /// </summary>
        public long MicroStop { get; set; }

        public int LaneCount
        {
            get => notes.Length;
            set
            {
                if (notes.Length == value) return;
                var newNotes = new Note[value];
                var newHiddenNotes = new Note[value];
                for (var i = 0; i < value; i++)
                {
                    if (i < notes.Length)
                    {
                        newNotes[i] = notes[i];
                        newHiddenNotes[i] = newHiddenNotes[i];
                    }
                }
                notes = newNotes;
                hiddenNotes = newHiddenNotes;
            }
        }

        /// <summary>
        /// タイムライン上からのBPM変化
        /// </summary>
        public double Bpm { get; set; }

        /// <summary>
        /// 小節線の有無
        /// </summary>
        public bool IsSectionLine { get; set; } = false;

        /// <summary>
        /// 表示するBGAのID
        /// </summary>
        public int BgaID { get; set; } = -1;

        /// <summary>
        /// 表示するレイヤーのID
        /// </summary>
        public int LayerID { get; set; } = -1;

        /// <summary>
        /// POORレイヤー
        /// </summary>
        public Layer[] EventLayer { get; set; } = [];

        /// <summary>
        /// スクロール速度
        /// </summary>
        public double Scroll { get; set; } = 1.0;

        private Note[] notes = new Note[noteSize];
        private Note[] hiddenNotes = new Note[noteSize];
        private readonly List<Note> bgNotes = [];

        /// <summary>
        /// タイムライン上の総ノート数を返す
        /// </summary>
        /// <param name="lnType"></param>
        /// <returns></returns>
        public int GetTotalNotes(LNType lnType = LNType.LongNote)
        {
            return notes.Count(n => n != null
            && (n is LongNote ln
            && (ln.Type == LNMode.ChargeNote || ln.Type == LNMode.HellChargeNote
                || (ln.Type == LNMode.Undefined && lnType != LNType.LongNote)
                || !ln.IsEnd)) || n is NormalNote);
        }

        public bool ExistNote() => notes.Any(n => n != null);

        public bool ExistNote(int lane) => notes[lane] != null;

        public Note GetNote(int lane) => notes[lane];

        public void SetNote(int lane, Note note)
        {
            notes[lane] = note;
            if (note == null) return;
            note.Section = Section;
            note.MicroTime = MicroTime;
        }

        public bool ExistHiddenNote() => hiddenNotes.Any(n => n != null);

        public bool ExistHiddenNote(int lane) => hiddenNotes[lane] != null;

        public Note GetHiddenNote(int lane) => hiddenNotes[lane];

        public void SetHiddenNote(int lane, Note note)
        {
            hiddenNotes[lane] = note;
            if (note == null) return;
            note.Section = Section;
            note.MicroTime = MicroTime;
        }

        public void AddBackGroundNote(Note note)
        {
            if (note == null) return;
            note.Section = Section;
            note.MicroTime = MicroTime;
            bgNotes.Add(note);
        }

        public void RemoveBackGroundNote(Note note)
        {
            bgNotes.Remove(note);
        }

        public Note[] BackGroundNotes => [.. bgNotes];
    }
}
