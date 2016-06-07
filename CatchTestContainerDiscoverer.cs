using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestWindow.Extensibility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.VisualStudio.VCProjectEngine;
using EnvDTE;

namespace VSCatchAdapter
{
    [Export(typeof(ITestContainerDiscoverer))]
    public class CatchTestContainerDiscoverer : ITestContainerDiscoverer
    {
        public const string ExecutorUriString = CatchTestExecuter.ExecutorUriString;

        public event EventHandler TestContainersUpdated;
        EnvDTE.DTE FDTE;
        private IServiceProvider FServiceProvider;
        private ILogger FLogger;
        private readonly List<ITestContainer> FCachedContainers;
        static protected string FileExtension { get { return ".exe"; } }
        public Uri ExecutorUri { get { return CatchTestExecuter.ExecutorUri; } }
        public IEnumerable<ITestContainer> TestContainers { get { return GetTestContainers(); } }

        [ImportingConstructor]
        public CatchTestContainerDiscoverer(
            [Import(typeof(SVsServiceProvider))] IServiceProvider AServiceProvider,
            ILogger ALogger)
        {
            Trace.WriteLine("Initializing Catch Test Discoverer");
            
            FCachedContainers = new List<ITestContainer>();
            FServiceProvider = AServiceProvider;
            FLogger = ALogger;
            FDTE = (EnvDTE.DTE)FServiceProvider.GetService(typeof(EnvDTE.DTE));
            FDTE.Events.BuildEvents.OnBuildDone += OnBuild;
            FDTE.Events.SolutionEvents.Opened += EnumerateProjectExes;
            FDTE.Events.SolutionEvents.ProjectAdded += ProjectAdded;
#if DEBUG
            System.Diagnostics.Debugger.Launch();
#endif
        }
        void ProjectAdded(Project AProject)
        {
            EnumerateProjectExes();
        }
        void EnumerateProjectExes()
        {
            FCachedContainers.Clear();
            try
            {
                for (int I = 0; I <= FDTE.Solution.Projects.Count; ++I)
                {
                    try
                    {
                        var ProjOrg = FDTE.Solution.Projects.Item(I).Object;
                        VCProject Proj = FDTE.Solution.Projects.Item(I).Object as VCProject;
                        if (Proj != null)
                        {
                            try
                            {
                                IVCCollection Configs = Proj.Configurations as IVCCollection;
                                if (Configs != null)
                                {
                                    foreach (var UCConfig in Configs)
                                        try
                                        {
                                            var Config = UCConfig as VCConfiguration;
                                            var File = EvaluatePrimaryOutput(Config);
                                            AddTestContainerIfTestFile(File);
                                        }
                                        catch { }
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
            }
            catch { }
            TestContainersUpdated(this, new EventArgs());
        }
        private string EvaluateOutputDir(VCConfiguration AConfig)
        {
            var SolutionDir = Path.GetDirectoryName(FDTE.Solution.FileName);
            var ProjectDir = (AConfig.project as VCProject).ProjectDirectory.Replace("$(SolutionDir)", SolutionDir + Path.DirectorySeparatorChar);
            return AConfig.OutputDirectory.Replace("$(SolutionDir)", SolutionDir + Path.DirectorySeparatorChar).Replace("$(ProjectDir)", ProjectDir + Path.DirectorySeparatorChar);
        }
        private string EvaluatePrimaryOutput(VCConfiguration AConfig)
        {
            var SolutionDir = Path.GetDirectoryName(FDTE.Solution.FileName);
            var ProjectDir = (AConfig.project as VCProject).ProjectDirectory.Replace("$(SolutionDir)", SolutionDir + Path.DirectorySeparatorChar);
            return AConfig.PrimaryOutput.Replace("$(SolutionDir)", SolutionDir + Path.DirectorySeparatorChar).Replace("$(ProjectDir)", ProjectDir + Path.DirectorySeparatorChar);
        }

        private void AddTestContainerIfTestFile(string AFile)
        {
            RemoveTestContainer(AFile); // Remove if there is an existing container

            // If this is a test file
            if (IsTestFile(AFile))
                FCachedContainers.Add(new CatchTestContainer(this, AFile, ExecutorUri));
        }

        private void RemoveTestContainer(string AFile)
        {
            var Index = FCachedContainers.FindIndex(x => x.Source.Equals(AFile, StringComparison.OrdinalIgnoreCase));
            if (Index >= 0)
            {
                FCachedContainers.RemoveAt(Index);
            }
        }

        void OnBuild(vsBuildScope Scope, vsBuildAction Action)
        {
            EnumerateProjectExes();
        }

        private IEnumerable<ITestContainer> GetTestContainers()
        {
            return FCachedContainers;
        }

        private static bool IsExeFile(string APath)
        {
            return FileExtension.Equals(Path.GetExtension(APath), StringComparison.OrdinalIgnoreCase);
        }

        private bool IsTestFile(string APath)
        {
            try
            {
                if (File.Exists(APath) && IsExeFile(APath) && CatchTestDiscoverer.IsCatchTest(APath))
                {
                    return true;
                }
                else
                    return false;
            }
            catch { }

            return false;
        }


        public void Dispose()
        {
            Dispose(true);
            // Use SupressFinalize in case a subclass
            // of this type implements a finalizer.
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool ADisposing)
        {}

    }
}
