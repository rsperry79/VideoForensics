using System.Runtime.CompilerServices;

using VideoForensics.Core.Helpers.Platform;

namespace VideoForensics.Data.Core.Tests
{
    /// <summary>
    /// Points the Database bootstrap pointer at a throwaway file before any test runs, so no test writes the real machine pointer.
    /// </summary>
    internal static class StorageTestIsolation
    {
        [ModuleInitializer]
        internal static void IsolatePointerStore()
        {
            string root = Path.Combine(VideoForensics.Build.TestTempRedirect.Root ?? Path.GetTempPath(), "vf-test-pointer-" + Guid.NewGuid().ToString("N"));
            DatabaseLocationPointerStore.Current = new FileDatabaseLocationPointerStore(root);
        }
    }
}
