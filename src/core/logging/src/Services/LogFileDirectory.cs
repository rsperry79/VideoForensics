namespace VideoForensics.Core.Logging.Services
{
    /// <summary>The folder that holds the daily audit log files, registered so history readers can find them.</summary>
    /// <param name="Path">Absolute path to the logs folder.</param>
    public sealed record LogFileDirectory(string Path);
}
