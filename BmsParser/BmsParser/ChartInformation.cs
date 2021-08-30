using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BmsParser
{
    public record ChartInformation(string Path, LNType LNType, int[] SelectedRandoms);
}
