namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>Reads a file from the repository by its path relative to the solution's Ui.Shared source folder.</summary>
internal static class RepoFile
{
    private static readonly string Anchor = Path.Combine("src", "client", "ui", "VideoForensics.Ui.Shared", "src");

    /// <summary>Returns the text of a file under the Ui.Shared source folder. Walks up from the test binary to find the repository root.</summary>
    public static string ReadUiSharedSource(string relativePath)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, Anchor)))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new DirectoryNotFoundException($"Repository root with {Anchor} not found above {AppContext.BaseDirectory}.");
        }

        return File.ReadAllText(Path.Combine(directory.FullName, Anchor, relativePath));
    }
}
