using System.Collections.Generic;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using System.IO;
using System.Text;
using System.Diagnostics;

namespace VSCatchAdapter
{
    [DefaultExecutorUri(CatchTestExecuter.ExecutorUriString)]
    [FileExtension(".exe")]
    class CatchTestDiscoverer : CatchTestOutputReader, ITestDiscoverer
    {
        public void DiscoverTests(IEnumerable<string> ASources, IDiscoveryContext ADiscoveryContext, IMessageLogger ALogger, ITestCaseDiscoverySink ADiscoverySink)
        {
            GetTests(ASources, ADiscoverySink);
        }

        public static List<TestCase> GetTests(IEnumerable<string> ASources, ITestCaseDiscoverySink ADiscoverySink)
        {
            const string Comparison1 = "is a Catch", Comparison2 = "host application";
            List<TestCase> TestCases = new List<TestCase>();
            foreach (var Source in ASources) {
                try
                {
                    var Text = File.ReadAllText(Source, Encoding.ASCII);
                    if (Text.Contains(Comparison1) && Text.Contains(Comparison2))
                    {
                        try
                        {
                            FLines = new List<string>();
                            {
                                Process P = new Process();
                                P.StartInfo.Arguments = "--list-test-names-only";
                                P.StartInfo.FileName = Source;
                                P.StartInfo.RedirectStandardOutput = true;
                                P.StartInfo.UseShellExecute = false;
                                P.OutputDataReceived += RecieveData;
                                P.Start();
                                P.BeginOutputReadLine();
                                P.WaitForExit();
                            }
                            foreach (var Line in FLines)
                                TestCases.Add(new TestCase(Line, CatchTestExecuter.ExecutorUri, Source));
                            if (ADiscoverySink != null)
                                foreach (var TestCase in TestCases)
                                    ADiscoverySink.SendTestCase(TestCase);
                            }
                        finally
                        {
                            FLines = null;
                        }

                    }
                }
                catch { }
            }
            return TestCases;
        }
    }
}
