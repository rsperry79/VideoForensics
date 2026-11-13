using Xunit;

namespace VideoForensics.Client.Core.Tests.Architecture
{
    /// <summary>
    /// Enforces plans/client-decouple.md (D0). Ui.Shared is a client host: it may not reference Client.Core or the data
    /// layer, and its pages may not inject a data-layer repository. Each repository group moves behind a Remote* class
    /// and an Api.Contracts endpoint (D1–D4) before this test can pass.
    /// </summary>
    public class ClientDecoupleBoundaryTests
    {
        [Fact]
        public void UiShared_ProjectFile_HasNoClientCoreOrDataReference()
        {
            string projectFile = Path.Combine(FindUiSharedFolder(), "src", "VideoForensics.Client.Ui.Shared.csproj");

            string[] references = File.ReadAllLines(projectFile)
                .Where(line => line.Contains("ProjectReference", StringComparison.Ordinal)
                    && (line.Contains("Client.Core", StringComparison.Ordinal) || line.Contains("data", StringComparison.OrdinalIgnoreCase)))
                .ToArray();

            Assert.True(references.Length == 0, $"Ui.Shared still references Client.Core or the data layer: {string.Join(" | ", references)}");
        }

        [Fact]
        public void UiShared_Source_DoesNotInjectDataLayerRepositories()
        {
            string sourceFolder = Path.Combine(FindUiSharedFolder(), "src");

            string[] violations = Directory.EnumerateFiles(sourceFolder, "*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".cs", StringComparison.Ordinal) || path.EndsWith(".razor", StringComparison.Ordinal))
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .Where(path => RepositoryInjection.IsMatch(File.ReadAllText(path)))
                .Select(path => Path.GetRelativePath(sourceFolder, path))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            Assert.True(violations.Length == 0, $"{violations.Length} Ui.Shared file(s) inject a data-layer repository: {string.Join(", ", violations)}");
        }

        private static readonly System.Text.RegularExpressions.Regex RepositoryInjection =
            new(@"\bI[A-Za-z]+Repository\b", System.Text.RegularExpressions.RegexOptions.Compiled);

        private static string FindUiSharedFolder()
        {
            string? directory = AppContext.BaseDirectory;
            while (directory is not null)
            {
                string candidate = Path.Combine(directory, "src", "client", "ui", "VideoForensics.Ui.Shared");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                directory = Path.GetDirectoryName(directory);
            }

            throw new DirectoryNotFoundException("Could not find src/client/ui/VideoForensics.Ui.Shared above the test output folder.");
        }
    }
}
