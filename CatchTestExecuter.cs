using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using System.Diagnostics;

namespace VSCatchAdapter
{
    [ExtensionUri(CatchTestExecuter.ExecutorUriString)]
    class CatchTestExecuter : CatchTestOutputReader, ITestExecutor
    {
        public const string ExecutorUriString = "executor://CatchTestExecutor/v1";
        public static readonly Uri ExecutorUri = new Uri(CatchTestExecuter.ExecutorUriString);
        bool FCancelled;
        public CatchTestExecuter()
        {
#if DEBUG
            Debugger.Launch();
#endif
        }
        public void RunTests(IEnumerable<string> ASources, IRunContext ARunContext,
            IFrameworkHandle AFrameworkHandle)
        {
            RunTests(CatchTestDiscoverer.GetTests(ASources, null), ARunContext, AFrameworkHandle);
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
                P.StartInfo.Arguments = '"' + Test.FullyQualifiedName + "\" -r compact";
                P.StartInfo.RedirectStandardOutput = true;
                P.StartInfo.FileName = Test.Source;
                P.StartInfo.CreateNoWindow = true;
                P.StartInfo.UseShellExecute = false;
                
                P.OutputDataReceived += RecieveData;

                var Result = new TestResult(Test);
                try
                {
                    FLines = new List<string>();
                    AFrameworkHandle.RecordStart(Test);
                    try
                    {
                        P.Start();
                        P.BeginOutputReadLine();
                        P.WaitForExit();
                    }
                    catch
                    {
                        Result.Outcome = TestOutcome.NotFound;
                        continue;
                    }


                    if (P.ExitCode != 0)
                    {
                        Result.ErrorMessage = "";
                        foreach (var Line in FLines)
                            Result.ErrorMessage += Line + System.Environment.NewLine;
                        Result.ErrorMessage = Result.ErrorMessage.Replace(Test.CodeFilePath, "Line ");
                    }
                    Result.Outcome = P.ExitCode == 0 ? TestOutcome.Passed : TestOutcome.Failed;
                }
                finally
                {
                    FLines = null;
                    Result.EndTime = DateTime.Now;
                    Result.Duration = Result.EndTime - Result.StartTime;
                    AFrameworkHandle.RecordEnd(Test, Result.Outcome);
                    AFrameworkHandle.RecordResult(Result);
                }
            }

        }

        public void Cancel()
        {
            FCancelled = true;
        }
    }
}
