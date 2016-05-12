using System.Collections.Generic;
using System.Diagnostics;

namespace VSCatchAdapter
{
    class CatchTestOutputReader
    {
        protected static List<string> FLines;
        protected static void RecieveData(object ASender, DataReceivedEventArgs AData)
        {
            var Result = AData.Data.Trim();
            if (Result.Length != 0)
                FLines.Add(Result);
        }
    }
}
