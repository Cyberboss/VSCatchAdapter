using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using System.Diagnostics;
using System.Threading.Tasks;

namespace VSCatchAdapter
{
    [ExtensionUri(CatchTestExecuter.ExecutorUriString)]
    class CatchTestExecuter : CatchTestOutputReader, ITestExecutor
    {
        public const string ExecutorUriString = "executor://CatchTestExecutor/v1";
        public static readonly Uri ExecutorUri = new Uri(CatchTestExecuter.ExecutorUriString);
        bool FCancelled;
        public void RunTests(IEnumerable<string> ASources, IRunContext ARunContext,
            IFrameworkHandle AFrameworkHandle)
        {
            RunTests(CatchTestDiscoverer.GetTests(ASources, null), ARunContext, AFrameworkHandle);
        }

        void RunTest(TestCase ATest, IFrameworkHandle AFrameworkHandle)
        {

            var P = new Process();
            P.StartInfo.Arguments = '"' + ATest.FullyQualifiedName + "\" -r compact";
            P.StartInfo.RedirectStandardOutput = true;
            P.StartInfo.FileName = ATest.Source;
            P.StartInfo.CreateNoWindow = true;
            P.StartInfo.UseShellExecute = false;

            P.OutputDataReceived += RecieveData;

            var Result = new TestResult(ATest);
            try
            {
                FLines = new List<string>();
                AFrameworkHandle.RecordStart(ATest);
                try
                {
                    P.Start();
                    P.BeginOutputReadLine();
                    while (!FCancelled && !P.WaitForExit(1)) ;
                }
                catch
                {
                    Result.Outcome = TestOutcome.NotFound;
                    return;
                }
                if (FCancelled)
                    Result.Outcome = TestOutcome.Skipped;
                else
                {
                    if (P.ExitCode != 0)
                    {
                        Result.ErrorMessage = "";
                        foreach (var Line in FLines)
                            Result.ErrorMessage += Line + System.Environment.NewLine;
                        Result.ErrorMessage = Result.ErrorMessage.Replace(ATest.CodeFilePath, "Line ");
                    }
                    Result.Outcome = P.ExitCode == 0 ? TestOutcome.Passed : TestOutcome.Failed;
                }
            }
            finally
            {
                try
                {
                    P.Kill();
                }
                catch { }
                FLines = null;
                Result.EndTime = DateTime.Now;
                Result.Duration = Result.EndTime - Result.StartTime;
                AFrameworkHandle.RecordEnd(ATest, Result.Outcome);
                AFrameworkHandle.RecordResult(Result);
            }
        }
        public void RunTests(IEnumerable<TestCase> ATests, IRunContext ARunContext,
               IFrameworkHandle AFrameworkHandle)
        {
            FCancelled = false;
            if (ARunContext.InIsolation)
                foreach (TestCase Test in ATests)
                {
                    if (FCancelled)
                        break;

                    RunTest(Test, AFrameworkHandle);
                }
            else
                Parallel.ForEach<TestCase>(ATests, (ATestCase) => {
                    if(!FCancelled)
                        RunTest(ATestCase, AFrameworkHandle);
                });
            if (ARunContext.KeepAlive)
                AFrameworkHandle.EnableShutdownAfterTestRun = true;
        }

        public void Cancel()
        {
            FCancelled = true;
        }
    }
}
