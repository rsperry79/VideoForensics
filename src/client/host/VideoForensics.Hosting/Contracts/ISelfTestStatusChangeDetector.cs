using VideoForensics.Api.Contracts;

namespace VideoForensics.Hosting.Contracts
{
    /// <summary>
    /// Decides whether a sampled self-test status needs to be pushed to admin clients.
    /// Kept as a pure function so the send-on-change rule is testable without a hub or timer.
    /// </summary>
    public interface ISelfTestStatusChangeDetector
    {
        /// <summary>
        /// Returns true when <paramref name="current"/> differs from <paramref name="previous"/> in any
        /// field of <see cref="SelfTestStatusDto"/>, compared by value.
        /// </summary>
        /// <param name="previous">The last status sent, or null if nothing has been sent yet.</param>
        /// <param name="current">The freshly sampled status.</param>
        bool HasChanged(SelfTestStatusDto? previous, SelfTestStatusDto current);
    }
}
