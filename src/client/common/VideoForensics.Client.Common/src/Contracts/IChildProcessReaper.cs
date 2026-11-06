namespace VideoForensics.Client.Common.Contracts
{
    /// <summary>
    /// Ties child processes to the lifetime of this process, so they do not outlive it. A child normally survives its parent, and a
    /// service that exits without stopping its children (an update restart, or a crash) leaves them running for good.
    /// </summary>
    public interface IChildProcessReaper
    {
        /// <summary>
        /// Adopts the running child processes of this process whose executable is <paramref name="executableName"/>, so the
        /// operating system ends them when this process ends. Processes with that name that are not children of this process are
        /// left alone.
        /// </summary>
        /// <param name="executableName">File name of the executable, for example "cloudflared.exe".</param>
        /// <returns>How many matching children are now tied to this process.</returns>
        int AdoptChildren(string executableName);
    }

    /// <summary>Reaper for platforms with no implementation: adopts nothing.</summary>
    public sealed class NoOpChildProcessReaper : IChildProcessReaper
    {
        /// <inheritdoc />
        public int AdoptChildren(string executableName) => 0;
    }
}
