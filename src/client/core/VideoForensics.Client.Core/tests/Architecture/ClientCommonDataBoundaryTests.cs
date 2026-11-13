using Xunit;

namespace VideoForensics.Client.Core.Tests.Architecture
{
    /// <summary>
    /// Enforces the client/server split for VideoForensics.Client.Common: it may not reference the data layer, in its
    /// project file or in its source. Client hosts depend on Client.Common, so a data reference here leaks into them.
    /// </summary>
    public class ClientCommonDataBoundaryTests
    {
        [Fact]
        public void ClientCommon_ProjectFile_HasNoDataLayerReference()
        {
            string projectFile = Path.Combine(FindClientCommonFolder(), "src", "VideoForensics.Client.Common.csproj");

            string[] references = File.ReadAllLines(projectFile)
                .Where(line => line.Contains("ProjectReference", StringComparison.Ordinal) && line.Contains("data/", StringComparison.Ordinal))
                .ToArray();

            Assert.True(references.Length == 0, $"Client.Common still references the data layer: {string.Join(" | ", references)}");
        }

        [Fact]
        public void ClientCommon_Source_DoesNotUseDataLayerNamespaces()
        {
            string sourceFolder = Path.Combine(FindClientCommonFolder(), "src");

            string[] violations = Directory.GetFiles(sourceFolder, "*.cs", SearchOption.AllDirectories)
                .Where(path => File.ReadAllText(path).Contains("VideoForensics.Data.", StringComparison.Ordinal))
                .Select(path => Path.GetRelativePath(sourceFolder, path))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            Assert.True(violations.Length == 0, $"{violations.Length} Client.Common file(s) use VideoForensics.Data.*: {string.Join(", ", violations)}");
        }

        private static string FindClientCommonFolder()
        {
            string? directory = AppContext.BaseDirectory;
            while (directory is not null)
            {
                string candidate = Path.Combine(directory, "src", "client", "common", "VideoForensics.Client.Common");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                directory = Path.GetDirectoryName(directory);
            }

            throw new DirectoryNotFoundException("Could not find src/client/common/VideoForensics.Client.Common above the test output folder.");
        }
    }
}
