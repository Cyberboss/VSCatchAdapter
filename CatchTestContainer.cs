using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestWindow.Extensibility;
using Microsoft.VisualStudio.TestWindow.Extensibility.Model;

namespace VSCatchAdapter
{
    public class CatchTestContainer : ITestContainer
    {

        private readonly DateTime FTimeStamp;

        public string Source { get; set; }
        public Uri ExecutorUri { get; set; }
        public IEnumerable<Guid> DebugEngines { get; set; }
        public FrameworkVersion TargetFramework { get; set; }
        public Architecture TargetPlatform { get; set; }
        public IDeploymentData DeployAppContainer() { return null; }
        public bool IsAppContainerTestContainer { get { return false; } }
        public ITestContainerDiscoverer Discoverer { get; private set; }

        public override string ToString()
        {
            return ExecutorUri.ToString() + "/" + Source;
        }

        public CatchTestContainer(ITestContainerDiscoverer ADiscoverer, string ASource, Uri AExecutorUri)
            : this(ADiscoverer, ASource, AExecutorUri, Enumerable.Empty<Guid>())
        { }

        public CatchTestContainer(ITestContainerDiscoverer ADiscoverer, string ASource, Uri AExecutorUri, IEnumerable<Guid> ADebugEngines)
        {
            Source = ASource;
            ExecutorUri = AExecutorUri;
            DebugEngines = ADebugEngines;
            Discoverer = ADiscoverer;
            TargetFramework = FrameworkVersion.None;
            TargetPlatform = Architecture.AnyCPU;
            FTimeStamp = GetTimeStamp();
#if DEBUG
            System.Diagnostics.Debugger.Launch();
#endif
        }

        private CatchTestContainer(CatchTestContainer ACopy)
            : this(ACopy.Discoverer, ACopy.Source, ACopy.ExecutorUri)
        {
            FTimeStamp = ACopy.FTimeStamp;
        }

        private DateTime GetTimeStamp()
        {
            if (!String.IsNullOrEmpty(Source) && File.Exists(Source))
                return File.GetLastWriteTime(Source);
            else
                return DateTime.MinValue;
        }

        public int CompareTo(ITestContainer AOther)
        {
            var TestContainer = AOther as CatchTestContainer;
            if (TestContainer == null)
                return -1;

            var Result = String.Compare(Source, TestContainer.Source, StringComparison.OrdinalIgnoreCase);
            if (Result != 0)
                return Result;

            return FTimeStamp.CompareTo(TestContainer.FTimeStamp);
        }

        public ITestContainer Snapshot()
        {
            return new CatchTestContainer(this);
        }
    }
}
