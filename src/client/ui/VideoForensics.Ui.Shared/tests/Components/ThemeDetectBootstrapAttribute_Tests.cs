using System.Runtime.CompilerServices;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Components;

public class ThemeDetectBootstrapAttribute_Tests
{
    // Bootstrap's text tokens (e.g. --bs-secondary-color, used by .form-text) switch only under data-bs-theme.
    // Without it, hint text stays dark on the dark theme and reads as blank.
    [Fact]
    public void ThemeDetectScript_SetsBootstrapThemeAttribute_OnDocumentElement()
    {
        string script = File.ReadAllText(Path.Combine(TestDirectory(), "..", "..", "src", "wwwroot", "scripts", "theme-detect.js"));

        Assert.Contains("setAttribute('data-bs-theme', isDark ? 'dark' : 'light')", script);
    }

    private static string TestDirectory([CallerFilePath] string path = "")
    {
        return Path.GetDirectoryName(path)!;
    }
}
