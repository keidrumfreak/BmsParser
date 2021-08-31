using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BmsParser
{
    public record DecodeLog(State State, string Message);

    public enum State { Info, Warning, Error }
}
