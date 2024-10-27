using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace BmsParser
{
    public class Layer
    {
        public static readonly Layer[] EMPTY = [];

        public Event @event;


        public Sequence[][] sequence;

        public Layer(Event @event, Sequence[]
            []
            sequence)
        {
            this.@event = @event;
            this.sequence = sequence;
        }

        public class Event
        {
            public EventType type;
            public int interval;

            public Event(EventType type, int interval)
            {
                this.type = type;
                this.interval = interval;
            }
        }

        public enum EventType
        {
            ALWAYS, PLAY, MISS
        }

        public class Sequence
        {

            public static readonly int END = int.MinValue;

            public long time;
            public int id;

            public Sequence(long time)
            {
                this.time = time;
                this.id = END;
            }

            public Sequence(long time, int id)
            {
                this.time = time;
                this.id = id;
            }

            public bool isEnd()
            {
                return id == END;
            }
        }
    }
}
