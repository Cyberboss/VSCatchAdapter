using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using System.Diagnostics;
using System.Threading;

namespace VSCatchAdapter
{
    [ExtensionUri(CatchTestExecuter.ExecutorUriString)]
    class CatchTestExecuter : CatchTestOutputReader, ITestExecutor
    {
        bool FCancelled;
        public void RunTests(IEnumerable<string> ASources, IRunContext ARunContext,
            IFrameworkHandle AFrameworkHandle)
        {
            IEnumerable<TestCase> Tests = CatchTestDiscoverer.GetTests(ASources, null);
            RunTests(Tests, ARunContext, AFrameworkHandle);
        }

        public void RunTests(IEnumerable<TestCase> ATests, IRunContext ARunContext,
               IFrameworkHandle AFrameworkHandle)
        {
            FCancelled = false;

            foreach (TestCase Test in ATests)
            {
                if (FCancelled)
                    break;


                var P = new Process();
                P.StartInfo.Arguments = Test.FullyQualifiedName;
                P.StartInfo.RedirectStandardError = true;
                P.StartInfo.FileName = Test.Source;
                P.StartInfo.UseShellExecute = false;

                P.OutputDataReceived += RecieveData;

                var Result = new TestResult(Test);
                Result.StartTime = DateTime.Now;
                try
                {
                    try
                    {
                        FLines = new List<string>();
                        try
                        {
                            P.Start();
                            P.BeginErrorReadLine();
                            P.BeginOutputReadLine();

                            while (!P.HasExited)
                            {
                                if (FCancelled)
                                {
                                    P.Kill();
                                    return;
                                }
                                Thread.Yield();
                            }
                        }
                        catch
                        {
                            Result.Outcome = TestOutcome.NotFound;
                            continue;
                        }
                    }
                    finally
                    {
                        FLines = null;
                    }


                    if (P.ExitCode != 0)
                    {
                        Result.ErrorMessage = "";
                        foreach (var Line in FLines)
                            Result.ErrorMessage += Line + System.Environment.NewLine;
                    }

                    Result.Outcome = P.ExitCode == 0 ? TestOutcome.Passed : TestOutcome.Failed;
                }
                finally
                {
                    Result.EndTime = DateTime.Now;
                    Result.Duration = Result.EndTime - Result.StartTime;
                    AFrameworkHandle.RecordResult(Result);
                }
            }

        }

        public void Cancel()
        {
            FCancelled = true;
        }
        public const string ExecutorUriString = "executor://CatchTestExecutor/v1";
        public static readonly Uri ExecutorUri = new Uri(CatchTestExecuter.ExecutorUriString);
    }
}
