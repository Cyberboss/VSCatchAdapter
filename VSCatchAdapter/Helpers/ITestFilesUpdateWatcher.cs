using System;
using VSCatchAdapter.EventWatchers.EventArgs;

namespace VSCatchAdapter.EventWatchers
{
    public interface ITestFilesUpdateWatcher
    {
        event EventHandler<TestFileChangedEventArgs> FileChangedEvent;
        void AddWatch(string path);
        void RemoveWatch(string path);
    }
}