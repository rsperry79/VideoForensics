using System.Reflection;
using System.Runtime.CompilerServices;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>
    /// Points the hard-ban store at a per-process temp directory before any test runs. The health source now skips calls
    /// while a ban is active, so a ban persisted to the machine's AppData file (from the live app) would make these tests skip.
    /// The hook is internal to the core library, so it is reached by reflection; this is test-only.
    /// </summary>
    internal static class RingProviderTestHardBanIsolation
    {
        private static readonly string Directory = Path.Combine(Path.GetTempPath(), "vf-ring-provider-tests-" + Guid.NewGuid().ToString("N"));

        [ModuleInitializer]
        internal static void Isolate()
        {
            var store = new FileHardBanStateStore(Directory);
            _ = typeof(HttpUtility)
                .GetMethod("UseHardBanStateStoreForTesting", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, new object?[] { store });

            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                try
                {
                    if (System.IO.Directory.Exists(Directory))
                    {
                        System.IO.Directory.Delete(Directory, true);
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
