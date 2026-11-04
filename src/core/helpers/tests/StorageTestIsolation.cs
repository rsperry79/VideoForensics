using System;
using System.IO;
using System.Runtime.CompilerServices;

using VideoForensics.Core.Helpers.Platform;

namespace VideoForensics.Core.Helpers.Tests
{
    /// <summary>
    /// Points the Database bootstrap pointer at a throwaway file before any test runs. Without this, tests that move the
    /// Database write the real machine pointer in ProgramData, and later tests then resolve the Database to a deleted folder.
    /// </summary>
    internal static class StorageTestIsolation
    {
        [ModuleInitializer]
        internal static void IsolatePointerStore()
        {
            string root = Path.Combine(Path.GetTempPath(), "vf-test-pointer-" + Guid.NewGuid().ToString("N"));
            DatabaseLocationPointerStore.Current = new FileDatabaseLocationPointerStore(root);
        }
    }
}
