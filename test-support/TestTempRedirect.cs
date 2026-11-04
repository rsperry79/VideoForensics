using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace VideoForensics.Build
{
    /// <summary>
    /// Points each test process's temp folder inside the repository (<c>.testtemp</c>, git-ignored) so test leftovers stay with the
    /// checkout and are removed with it. Path.GetTempPath reads TMP and TEMP on every call, so setting them early redirects every
    /// test that uses it. Outside a git checkout nothing changes.
    /// </summary>
    internal static class TestTempRedirect
    {
        [ModuleInitializer]
        internal static void RedirectTempIntoRepository()
        {
            string? repoRoot = FindRepositoryRoot(AppContext.BaseDirectory);
            if (repoRoot is null)
            {
                return;
            }

            string tempRoot = Path.Combine(repoRoot, ".testtemp", Guid.NewGuid().ToString("N"));
            _ = Directory.CreateDirectory(tempRoot);
            Environment.SetEnvironmentVariable("TMP", tempRoot);
            Environment.SetEnvironmentVariable("TEMP", tempRoot);
        }

        /// <summary>Walks up from the test output folder to the folder that holds <c>.git</c> (a folder, or a file in a worktree).</summary>
        private static string? FindRepositoryRoot(string start)
        {
            DirectoryInfo? directory = new(start);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, ".git")) || Directory.Exists(Path.Combine(directory.FullName, ".git")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            return null;
        }
    }
}
