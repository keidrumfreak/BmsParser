using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BmsParser
{
    /// <summary>
    /// タイムライン
    /// </summary>
    public class TimeLine
    {
        long timeMicrosecond;
        /// <summary>
        /// タイムラインの時間(us)
        /// </summary>
        public long TimeMicrosecond
        {
            get { return timeMicrosecond; }
            set
            {
                timeMicrosecond = value;
                foreach (var note in notes.Where(n => n != null))
                {
                    note.TimeMicrosecond = value;
                }
                foreach (var note in hiddenNotes.Where(n => n != null))
                {
                    note.TimeMicrosecond = value;
                }
                foreach (var note in bgnotes.Where(n => n != null))
                {
                    note.TimeMicrosecond = value;
                }
            }
        }

        public long TimeMilliSeccond => TimeMicrosecond / 1000;

        public long Time => (int)(TimeMicrosecond / 1000);

        double section;
        /// <summary>
        /// タイムラインの小節
        /// </summary>
        public double Section
        { 
            get { return section; }
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
                foreach (var note in bgnotes.Where(n => n != null))
                {
                    note.Section = value;
                }
            }
        }

        public int StopTime => (int)(StopMicrosecond / 1000);

        public long StopMilliSecond => StopMicrosecond / 1000;

        /// <summary>
        /// ストップ時間(us)
        /// </summary>
        public long StopMicrosecond { get; set; }

        public int LaneCount
        {
            get { return notes.Length; }
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
        public Layer[] EventLayer { get; set; } = Layer.Empty;

        /// <summary>
        /// スクロール速度
        /// </summary>
        public double Scroll { get; set; } = 1.0;

        private Note[] notes;

        private Note[] hiddenNotes;

        private List<Note> bgnotes = new();

        public TimeLine(double section, long time, int noteSize)
        {
            Section = section;
            timeMicrosecond = time;
            notes = new Note[noteSize];
            hiddenNotes = new Note[noteSize];
        }

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
            note.TimeMicrosecond = TimeMicrosecond;
        }

        public bool ExistHiddenNote() => hiddenNotes.Any(n => n != null);

        public bool ExistHiddenNote(int lane) => hiddenNotes[lane] != null;

        public Note GetHiddenNote(int lane) => hiddenNotes[lane];

        public void SetHiddenNote(int lane, Note note)
        {
            hiddenNotes[lane] = note;
            if (note == null) return;
            note.Section = Section;
            note.TimeMicrosecond = TimeMicrosecond;
        }

        public void AddBackGroundNote(Note note)
        {
            if (note == null) return;
            note.Section = Section;
            note.TimeMicrosecond = TimeMicrosecond;
            bgnotes.Add(note);
        }

        public void RemoveBackGroundNote(Note note)
        {
            bgnotes.Remove(note);
        }

        public Note[] BackGroundNotes => bgnotes.ToArray();
    }
}
