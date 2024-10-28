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
        private TimeLine[] sections;
        private int sectionbasepos;
        private int sectionseekpos;

        private TimeLine[] bpms;
        private int bpmbasepos;
        private int bpmseekpos;

        private TimeLine[] stops;
        private int stopbasepos;
        private int stopseekpos;

        public EventLane(BmsModel model)
        {
            var section = new List<TimeLine>();
            var bpm = new List<TimeLine>();
            var stop = new List<TimeLine>();

            TimeLine prev = null;
            foreach (TimeLine tl in model.Timelines)
            {
                if (tl.getSectionLine())
                {
                    section.Add(tl);
                }
                if (tl.getBPM() != (prev != null ? prev.getBPM() : model.Bpm))
                {
                    bpm.Add(tl);
                }
                if (tl.getStop() != 0)
                {
                    stop.Add(tl);
                }
                prev = tl;
            }
            sections = section.ToArray();
            bpms = bpm.ToArray();
            stops = stop.ToArray();
        }

        public TimeLine[] getSections()
        {
            return sections;
        }

        public TimeLine[] getBpmChanges()
        {
            return bpms;
        }

        public TimeLine[] getStops()
        {
            return stops;
        }

        public TimeLine getSection()
        {
            if (sectionseekpos < sections.Length)
            {
                return sections[sectionseekpos++];
            }
            return null;
        }

        public TimeLine getBpm()
        {
            if (bpmseekpos < bpms.Length)
            {
                return bpms[bpmseekpos++];
            }
            return null;
        }

        public TimeLine getStop()
        {
            if (stopseekpos < stops.Length)
            {
                return stops[stopseekpos++];
            }
            return null;
        }

        public void reset()
        {
            sectionseekpos = sectionbasepos;
            bpmseekpos = bpmbasepos;
            stopseekpos = stopbasepos;
        }

        public void mark(int time)
        {
            for (; sectionbasepos < sections.Length - 1 && sections[sectionbasepos + 1].getTime() > time; sectionbasepos++)
                ;
            for (; sectionbasepos > 0 && sections[sectionbasepos].getTime() < time; sectionbasepos--)
                ;
            for (; bpmbasepos < bpms.Length - 1 && bpms[bpmbasepos + 1].getTime() > time; bpmbasepos++)
                ;
            for (; bpmbasepos > 0 && bpms[bpmbasepos].getTime() < time; bpmbasepos--)
                ;
            for (; stopbasepos < stops.Length - 1 && stops[stopbasepos + 1].getTime() > time; stopbasepos++)
                ;
            for (; stopbasepos > 0 && stops[stopbasepos].getTime() < time; stopbasepos--)
                ;
            sectionseekpos = sectionbasepos;
            bpmseekpos = bpmbasepos;
            stopseekpos = stopbasepos;
        }
    }
}
