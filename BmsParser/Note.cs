using System.Collections.Generic;

namespace BmsParser
{
    public class Note
    {
        /// <summary>
        /// アサインされている音源ID
        /// </summary>
        public int Wav { get; set; }

        /// <summary>
        /// ノーツの状態
        /// </summary>
        public int State { get; set; }

        public long MilliStarttime => MicroStarttime / 1000;

        /// <summary>
        /// 音源IDの音の開始時間(us)
        /// </summary>
        public long MicroStarttime { get; set; }

        public long MilliDuration => MicroDuration / 1000;

        /// <summary>
        /// 音源IDの音を鳴らす流さ(us)
        /// </summary>
        public long MicroDuration { get; set; }

        public int PlayTime
        {
            get => (int)(MicroPlayTime / 1000);
            set => MicroPlayTime = value * 1000;
        }

        public long MilliPlayTime => MicroPlayTime / 1000;

        /// <summary>
        /// ノーツの演奏時間
        /// </summary>
        public long MicroPlayTime { get; set; }

        /// <summary>
        /// ノートが配置されている小節
        /// </summary>
        public double Section { get; set; }

        public int Time => (int)(MicroTime / 1000);

        public long MilliTime => MicroTime / 1000;

        /// <summary>
        /// ノートが配置されている時間(us)
        /// </summary>
        public long MicroTime { get; set; }

        private List<Note>? layeredNotes;
        /// <summary>
        /// 同時演奏されるノート
        /// </summary>
        public IEnumerable<Note> LayeredNotes => layeredNotes ?? [];

        public void AddLayeredNote(Note note)
        {
            if (note == null)
                return;

            if (layeredNotes == default)
                layeredNotes = [];

            note.Section = Section;
            note.MicroTime = MicroTime;
            layeredNotes.Add(note);
        }
    }
}
