using System;
using System.IO;
using System.Runtime.CompilerServices;

// HttpUtility's throttle and hard-ban state is process-wide static by design. Test classes that run in
// parallel share it, so one class's throttled call (or a ban loaded from another class's store swap) makes
// unrelated classes fail with ThrottledException. Running this assembly's tests serially keeps that state
// per-test. Verified: the full project fails 5 tests in parallel and passes all 342 serially.
[assembly: Xunit.v3.ParallelizationAttribute(Mode = Xunit.Sdk.ParallelMode.None)]

namespace VideoForensics.Providers.Ring.Core.Tests
{
    /// <summary>
    /// Points the hard-ban store at a per-process temp directory before any test runs. Without this,
    /// throttled test calls persist a 1-hour ban to the machine-wide AppData file and later test
    /// processes fail with ThrottledException.
    /// </summary>
    internal static class RingTestHardBanIsolation
    {
        /// <summary>Per-process store used for every test in this assembly.</summary>
        internal static readonly FileHardBanStateStore Store = new FileHardBanStateStore(
            Path.Combine(Path.GetTempPath(), "vf-ring-core-tests-" + Guid.NewGuid().ToString("N")));

        [ModuleInitializer]
        internal static void Isolate()
        {
            HttpUtility.UseHardBanStateStoreForTesting(Store);
            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                try
                {
                    string? folder = Path.GetDirectoryName(Store.FilePath);
                    if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                    {
                        Directory.Delete(folder, true);
                    }
                }
                catch (IOException)
                {
                    // Best-effort temp cleanup.
                }
            };
        }
    }
}
