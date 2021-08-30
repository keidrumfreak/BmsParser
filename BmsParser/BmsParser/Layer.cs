using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BmsParser
{
    public record Layer(Event Event, Sequece[][] Sequence)
    {
        public static readonly Layer[] Empty = Array.Empty<Layer>();
    }

    public record Event(EventType Type, int Interval);

    public enum EventType { Always, Play, Miss }

    public record Sequece(long Time, int ID = int.MinValue)
    {
        public static readonly int End = int.MinValue;

        public bool IsEnd => ID == End;
    }
}
