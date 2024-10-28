using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BmsParser
{
    public class Lane
    {
        private readonly Note[] notes;
        private int notebasepos;
        private int noteseekpos;

        private readonly Note[] hiddens;
        private int hiddenbasepos;
        private int hiddenseekpos;

        public Lane(BmsModel model, int lane)
        {
            var note = new List<Note>();
            var hnote = new List<Note>();
            foreach (Timeline tl in model.Timelines)
            {
                if (tl.ExistNote(lane))
                {
                    note.Add(tl.GetNote(lane));
                }
                if (tl.GetHiddenNote(lane) != null)
                {
                    hnote.Add(tl.GetHiddenNote(lane));
                }
            }
            notes = [.. note];
            hiddens = [.. hnote];
        }

        public Note[] Notes => notes;

        public Note[] Hiddens => hiddens;

        public Note? Note
        {
            get
            {
                if (noteseekpos < notes.Length)
                {
                    return notes[noteseekpos++];
                }
                return null;
            }
        }

        public Note? Hidden
        {
            get
            {
                if (hiddenseekpos < hiddens.Length)
                {
                    return hiddens[hiddenseekpos++];
                }
                return null;
            }
        }

        public void Reset()
        {
            noteseekpos = notebasepos;
            hiddenseekpos = hiddenbasepos;
        }

        public void Mark(int time)
        {
            for (; notebasepos < notes.Length - 1 && notes[notebasepos + 1].Time < time; notebasepos++)
                ;
            for (; notebasepos > 0 && notes[notebasepos].Time > time; notebasepos--)
                ;
            noteseekpos = notebasepos;
            for (; hiddenbasepos < hiddens.Length - 1
                    && hiddens[hiddenbasepos + 1].Time < time; hiddenbasepos++)
                ;
            for (; hiddenbasepos > 0 && hiddens[hiddenbasepos].Time > time; hiddenbasepos--)
                ;
            hiddenseekpos = hiddenbasepos;
        }
    }
}
