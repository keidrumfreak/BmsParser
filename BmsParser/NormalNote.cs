using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BmsParser
{
    public class NormalNote : Note
    {
        public NormalNote(int wav)
        {
            Wav = wav;
        }

        public NormalNote(int wav, long start, long duration)
        {
            Wav = wav;
            MicroStarttime = start;
            MicroDuration = duration;
        }
    }
}
