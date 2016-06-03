using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestWindow.Extensibility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using VSCatchAdapter.EventWatchers;
using VSCatchAdapter.EventWatchers.EventArgs;
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
        private ISolutionEventsListener FSolutionListener;
        private ITestFilesUpdateWatcher FTestFilesUpdateWatcher;
        private ITestFileAddRemoveListener FTestFilesAddRemoveListener;
        private bool FInitialContainerSearch;
        private readonly List<ITestContainer> FCachedContainers;
        private List<string> FKnownOtherExes;
        static protected string FileExtension { get { return ".exe"; } }
        public Uri ExecutorUri { get { return CatchTestExecuter.ExecutorUri; } }
        public IEnumerable<ITestContainer> TestContainers { get { return GetTestContainers(); } }

        [ImportingConstructor]
        public CatchTestContainerDiscoverer(
            [Import(typeof(SVsServiceProvider))] IServiceProvider AServiceProvider,
            ILogger ALogger,
            ISolutionEventsListener ASolutionListener,
            ITestFilesUpdateWatcher ATestFilesUpdateWatcher,
            ITestFileAddRemoveListener ATestFilesAddRemoveListener)
        {
            Trace.WriteLine("Initializing Catch Test Discoverer");

            FInitialContainerSearch = true;
            FCachedContainers = new List<ITestContainer>();
            FKnownOtherExes = new List<string>();
            FServiceProvider = AServiceProvider;
            FLogger = ALogger;
            FSolutionListener = ASolutionListener;
            FTestFilesUpdateWatcher = ATestFilesUpdateWatcher;
            FTestFilesAddRemoveListener = ATestFilesAddRemoveListener;

            FDTE = (EnvDTE.DTE)FServiceProvider.GetService(typeof(EnvDTE.DTE));
            
            FTestFilesAddRemoveListener.StartListeningForTestFileChanges();

            FSolutionListener.SolutionUnloaded += SolutionListenerOnSolutionUnloaded;
            FSolutionListener.SolutionUnloaded += SolutionListenerOnSolutionLoaded;
            FSolutionListener.SolutionProjectChanged += OnSolutionProjectChanged;
            FSolutionListener.StartListeningForChanges();

            FTestFilesUpdateWatcher.FileChangedEvent += OnTestFileItemChanged;
            try
            {
                SolutionListenerOnSolutionLoaded(null, null);
            }
            catch { }
        }

        private void OnTestContainersChanged()
        {
            if (TestContainersUpdated != null && !FInitialContainerSearch)
            {
                TestContainersUpdated(this, EventArgs.Empty);
            }
        }

        private void SolutionListenerOnSolutionUnloaded(object ASender, EventArgs AEventArgs)
        {
            FInitialContainerSearch = true;
            FCachedContainers.Clear();
        }

        private void SolutionListenerOnSolutionLoaded(object ASender, EventArgs AEventArgs)
        {
            EnumerateProjectExes();
            FDTE.Events.BuildEvents.OnBuildDone += OnBuild;
        }

        void OnBuild(vsBuildScope Scope, vsBuildAction Action)
        {
            EnumerateProjectExes();
        }

        private void OnSolutionProjectChanged(object ASender, SolutionEventsListenerEventArgs AEventArgs)
        {
            FInitialContainerSearch = true;
            FCachedContainers.Clear();
            EnumerateProjectExes();
        }

        void EnumerateProjectExes()
        {
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
                                            List<string> Files = new List<string>();
                                            Files.Add(EvaluatePrimaryOutput(Config));
                                            UpdateFileWatcher(Files, true);
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
        private void UpdateFileWatcher(IEnumerable<string> AFiles, bool AIsAdd)
        {
            foreach (var F in AFiles)
            {
                if (AIsAdd)
                {
                    foreach (var Existing in FCachedContainers)
                        if (Existing.Source == F)
                            continue;
                    if (AddTestContainerIfTestFile(F))
                        FTestFilesUpdateWatcher.AddWatch(F);
                }
                else
                {
                    FTestFilesUpdateWatcher.RemoveWatch(F);
                    RemoveTestContainer(F);
                }
            }
        }


        private void OnTestFileItemChanged(object ASender, TestFileChangedEventArgs AEventArgs)
        {
            if(AEventArgs.ChangedReason == TestFileChangedReason.Removed)
            {
                var List = new List<string>();
                List.Add(AEventArgs.File);
                UpdateFileWatcher(List, false);
            }
        }

        private bool AddTestContainerIfTestFile(string AFile)
        {
            RemoveTestContainer(AFile); // Remove if there is an existing container

            // If this is a test file
            if (IsTestFile(AFile))
            {
                FCachedContainers.Add(new CatchTestContainer(this, AFile, ExecutorUri));
                return true;
            }
            return false;
        }

        private void RemoveTestContainer(string AFile)
        {
            var Index = FCachedContainers.FindIndex(x => x.Source.Equals(AFile, StringComparison.OrdinalIgnoreCase));
            if (Index >= 0)
            {
                FCachedContainers.RemoveAt(Index);
            }
        }

        private IEnumerable<ITestContainer> GetTestContainers()
        {
            if (FInitialContainerSearch)
            {
                FCachedContainers.Clear();
                FKnownOtherExes.Clear();
            }

            UpdateFileWatcher(CheckForNewExes(), true);

            return FCachedContainers;
        }

        private IEnumerable<string> CheckForNewExes()
        {
            var Solution = (IVsSolution)FServiceProvider.GetService(typeof(SVsSolution));
            string Unused1, Unused2, SolutionDirectory;
            Solution.GetSolutionInfo(out SolutionDirectory, out Unused1, out Unused2);
            List<string> NewExes = new List<string>();
            try
            {
                DirSearch(SolutionDirectory, NewExes);   
            }
            catch { }
            return NewExes;
        }
        void DirSearch(string ADir, List<string> AAddToHere)
        {
            foreach (var D in Directory.GetDirectories(ADir))
            {
                DirSearch(D, AAddToHere);
            }
            foreach (var F in Directory.GetFiles(ADir))
            {
                if (Path.GetExtension(F) == FileExtension && !FKnownOtherExes.Contains(F))
                {
                    FKnownOtherExes.Add(F);
                    AAddToHere.Add(F);
                }
            }
        }

        private List<string> FindExeFiles(string APath)
        {
            var Files = new List<string>();

            var SysFiles = Directory.GetFiles(APath);

            foreach (var File in SysFiles)
                if (IsTestFile(File))
                    Files.Add(File);
            return Files;
        }

        private static bool IsExeFile(string APath)
        {
            return FileExtension.Equals(Path.GetExtension(APath), StringComparison.OrdinalIgnoreCase);
        }

        private bool IsTestFile(string APath)
        {
            try
            {
                if (IsExeFile(APath) && CatchTestDiscoverer.IsCatchTest(APath))
                {
                    FKnownOtherExes.Add(APath);
                    return true;
                }
                else
                    return false;
            }
            catch (IOException AException)
            {
                FLogger.Log(MessageLevel.Error, "IO error when detecting a catch test file during Test Container Discovery" + AException.Message);
            }

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
        {
            if (ADisposing)
            {
                if (FTestFilesUpdateWatcher != null)
                {
                    FTestFilesUpdateWatcher.FileChangedEvent -= OnTestFileItemChanged;
                    ((IDisposable)FTestFilesUpdateWatcher).Dispose();
                    FTestFilesUpdateWatcher = null;
                }

                if (FTestFilesAddRemoveListener != null)
                {
                    FTestFilesAddRemoveListener.TestFileChanged -= OnTestFileItemChanged;
                    FTestFilesAddRemoveListener.StopListeningForTestFileChanges();
                    FTestFilesAddRemoveListener = null;
                }

                if (FSolutionListener != null)
                {
                    FSolutionListener.SolutionProjectChanged -= OnSolutionProjectChanged;
                    FSolutionListener.StopListeningForChanges();
                    FSolutionListener = null;
                }
            }
        }


    }
}
