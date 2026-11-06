using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Components;

public class TextColorTokens_Tests
{
    // Hard-coded grays and blue text vanish on the dark theme. Bootstrap text tokens follow data-bs-theme.
    private static readonly Regex HardCodedText = new(@"(?<![-\w])color\s*:\s*(#333|#666|#999|#0066cc|#212529)\b", RegexOptions.IgnoreCase);

    // Light chat bubbles keep their gray text: their background is hard-coded light, so a theme token would be unreadable there.
    private static readonly string[] ExcludedFiles = ["ChatPanel.razor"];

    [Fact]
    public void Sources_HardCodedText_NoBannedColors()
    {
        string srcRoot = Path.GetFullPath(Path.Combine(TestDirectory(), "..", "..", "src"));
        List<string> hits = [];

        foreach (string file in Directory.EnumerateFiles(srcRoot, "*.razor*", SearchOption.AllDirectories))
        {
            string normalized = file.Replace('\\', '/');
            if (normalized.Contains("/obj/") || normalized.Contains("/bin/") || normalized.Contains("/lib/")
                || ExcludedFiles.Any(name => normalized.EndsWith("/" + name, StringComparison.Ordinal)))
            {
                continue;
            }

            string[] lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                if (HardCodedText.IsMatch(lines[i]))
                {
                    hits.Add($"{normalized}:{i + 1}");
                }
            }
        }

        Assert.Empty(hits);
    }

    private static string TestDirectory([CallerFilePath] string path = "")
    {
        return Path.GetDirectoryName(path)!;
    }
}
