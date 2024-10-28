using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BmsParser
{
    public class EventLane
    {
        private readonly Timeline[] sections;
        private int sectionbasepos;
        private int sectionseekpos;

        private readonly Timeline[] bpms;
        private int bpmbasepos;
        private int bpmseekpos;

        private readonly Timeline[] stops;
        private int stopbasepos;
        private int stopseekpos;

        public EventLane(BmsModel model)
        {
            var section = new List<Timeline>();
            var bpm = new List<Timeline>();
            var stop = new List<Timeline>();

            Timeline? prev = null;
            foreach (Timeline tl in model.Timelines)
            {
                if (tl.IsSectionLine)
                {
                    section.Add(tl);
                }
                if (tl.Bpm != (prev != null ? prev.Bpm : model.Bpm))
                {
                    bpm.Add(tl);
                }
                if (tl.Stop != 0)
                {
                    stop.Add(tl);
                }
                prev = tl;
            }
            sections = [.. section];
            bpms = [.. bpm];
            stops = [.. stop];
        }

        public Timeline[] Sections => sections;

        public Timeline[] BpmChanges => bpms;

        public Timeline[] Stops => stops;

        public Timeline? Section
        {
            get
            {
                if (sectionseekpos < sections.Length)
                {
                    return sections[sectionseekpos++];
                }
                return null;
            }
        }

        public Timeline? Bpm
        {
            get
            {
                if (bpmseekpos < bpms.Length)
                {
                    return bpms[bpmseekpos++];
                }
                return null;
            }
        }

        public Timeline? Stop
        {
            get
            {
                if (stopseekpos < stops.Length)
                {
                    return stops[stopseekpos++];
                }
                return null;
            }
        }

        public void Reset()
        {
            sectionseekpos = sectionbasepos;
            bpmseekpos = bpmbasepos;
            stopseekpos = stopbasepos;
        }

        public void Mark(int time)
        {
            for (; sectionbasepos < sections.Length - 1 && sections[sectionbasepos + 1].Time > time; sectionbasepos++)
                ;
            for (; sectionbasepos > 0 && sections[sectionbasepos].Time < time; sectionbasepos--)
                ;
            for (; bpmbasepos < bpms.Length - 1 && bpms[bpmbasepos + 1].Time > time; bpmbasepos++)
                ;
            for (; bpmbasepos > 0 && bpms[bpmbasepos].Time < time; bpmbasepos--)
                ;
            for (; stopbasepos < stops.Length - 1 && stops[stopbasepos + 1].Time > time; stopbasepos++)
                ;
            for (; stopbasepos > 0 && stops[stopbasepos].Time < time; stopbasepos--)
                ;
            sectionseekpos = sectionbasepos;
            bpmseekpos = bpmbasepos;
            stopseekpos = stopbasepos;
        }
    }
}
