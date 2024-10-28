using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BmsParser
{
    public record ChartInformation(string Path, LNType LNType, int[] SelectedRandoms);

    //public enum State { Info, Warning, Error }

    //public record DecodeLog(State State, string Message);

    //public enum EventType { Always, Play, Miss }

    //public record Event(EventType Type, int Interval);

    //public record TimeLineCache(double Time, TimeLine TimeLine);

    //public record Layer(Event Event, Sequence[][] Sequence)
    //{
    //    public static readonly Layer[] Empty = Array.Empty<Layer>();
    //}

    //public record Sequence(long Time, int ID = int.MinValue)
    //{
    //    public static readonly int End = int.MinValue;

    //    public bool IsEnd => ID == End;
    //}
}
