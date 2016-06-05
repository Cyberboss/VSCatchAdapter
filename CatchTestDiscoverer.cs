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
        static bool ContainsSequence(byte[] toSearch, byte[] toFind)
        {
            for (var i = 0; i + toFind.Length < toSearch.Length; i++)
            {
                var allSame = true;
                for (var j = 0; j < toFind.Length; j++)
                {
                    if (toSearch[i + j] != toFind[j])
                    {
                        allSame = false;
                        break;
                    }
                }

                if (allSame)
                {
                    return true;
                }
            }

            return false;
        }
        static public bool IsCatchTest(string APath)
        {
            const string Comparison1 = "is a Catch", Comparison2 = "host application";
            var Text = File.ReadAllBytes(APath);
            return ContainsSequence(Text, Encoding.ASCII.GetBytes(Comparison1)) && ContainsSequence(Text, Encoding.ASCII.GetBytes(Comparison2));
        }
        public static List<TestCase> GetTests(IEnumerable<string> ASources, ITestCaseDiscoverySink ADiscoverySink)
        {
            List<TestCase> TestCases = new List<TestCase>();
            foreach (var Source in ASources)
            {
                if (File.Exists(Source))
                {
                    try
                    {
                        Trace.WriteLine("Checking " + Source + " for Catch tests");
                        if (IsCatchTest(Source))
                        {
                            try
                            {
                                FLines = new List<string>();
                                {
                                    Process P = new Process();
                                    P.StartInfo.Arguments = "--list-test-names-and-sources";
                                    P.StartInfo.FileName = Source;
                                    P.StartInfo.RedirectStandardOutput = true;
                                    P.StartInfo.UseShellExecute = false;
                                    P.StartInfo.CreateNoWindow = true;
                                    P.OutputDataReceived += RecieveData;
                                    P.Start();
                                    P.BeginOutputReadLine();
                                    P.WaitForExit();
                                }
                                try {
                                    if (FLines.Count % 2 == 0)
                                    {
                                        bool IsSourceLine = false;
                                        TestCase TestCase = null;
                                        foreach (var Line in FLines)
                                        {
                                            if (!IsSourceLine)
                                                TestCase = new TestCase(Line, CatchTestExecuter.ExecutorUri, Source);
                                            else
                                            {
                                                int FirstParen = 0, SecondParen = 0;
                                                for (int I = Line.Length - 1; I > 0; --I)
                                                    if (Line[I] == ')')
                                                    {
                                                        SecondParen = I;
                                                        break;
                                                    }
                                                for (int I = SecondParen - 1; I > 0; --I)
                                                    if (Line[I] == '(')
                                                    {
                                                        FirstParen = I;
                                                        break;
                                                    }
                                                TestCase.CodeFilePath = Line.Substring(0, FirstParen);
                                                var SubStr = Line.Substring(FirstParen + 1, SecondParen - FirstParen - 1);
                                                TestCase.LineNumber = System.Int32.Parse(SubStr);
                                                TestCases.Add(TestCase);
                                                if (ADiscoverySink != null)
                                                    ADiscoverySink.SendTestCase(TestCase);
                                            }
                                            IsSourceLine = !IsSourceLine;
                                        }
                                    }
                                }catch { }
                            }
                            finally
                            {
                                FLines = null;
                            }

                        }
                    }
                    catch { }
                }
            }
            return TestCases;
        }
    }
}
