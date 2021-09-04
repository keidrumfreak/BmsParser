using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BmsParser
{
    public class Note
    {
        public static readonly Note[] EmptyArray = Array.Empty<Note>();

        /// <summary>
        /// アサインされている音源ID
        /// </summary>
        public int Wav { get; set; }

        /// <summary>
        /// ノーツの状態
        /// </summary>
        public int State { get; set; }

        /// <summary>
        /// 音源IDの音の開始時間(us)
        /// </summary>
        public long StartTimeMicrosecond { get; set; }

        public int Duration => (int)(DurationMicrosecond / 1000);

        public long DurationMillisecond => DurationMicrosecond / 1000;

        /// <summary>
        /// 音源IDの音を鳴らす流さ(us)
        /// </summary>
        public long DurationMicrosecond { get; set; }

        /// <summary>
        /// ノーツの演奏時間
        /// </summary>
        public int PlayTime { get; set; }

        /// <summary>
        /// ノートが配置されている小節
        /// </summary>
        public double Section { get; set; }

        /// <summary>
        /// ノートが配置されている時間(us)
        /// </summary>
        public long TimeMicrosecond { get; set; }

        public long TimeMillisccond => TimeMicrosecond / 1000;

        public int Time => (int)(TimeMicrosecond / 1000);

        private List<Note> layeredNotes;

        /// <summary>
        /// 同時演奏されるノート
        /// </summary>
        public IEnumerable<Note> LayeredNotes => layeredNotes;

        public void AddLayeredNote(Note note)
        {
            if (note == null)
                return;

            if (layeredNotes == default)
                layeredNotes = new List<Note>();

            note.Section = Section;
            note.TimeMicrosecond = TimeMicrosecond;
            layeredNotes.Add(note);
        }
    }
}
