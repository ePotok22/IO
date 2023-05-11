using System;
using System.Threading.Tasks;

namespace FFF.IO.Helper
{
    internal static class ParallelHelper
    {
        public static ParallelOptions Options =>
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 2 };
    }
}
