using VideoForensics.Api.Contracts;
using VideoForensics.Hosting.Contracts;

namespace VideoForensics.Hosting
{
    /// <summary>
    /// Value-based comparison for <see cref="SelfTestStatusDto"/> send-on-change.
    /// <para>
    /// Every field is compared by value. SelfTestStatusDto has no collection members today, so no
    /// content-comparison is needed; if one is added, it must be compared by content here.
    /// </para>
    /// </summary>
    public sealed class SelfTestStatusChangeDetector : ISelfTestStatusChangeDetector
    {
        /// <inheritdoc />
        public bool HasChanged(SelfTestStatusDto? previous, SelfTestStatusDto current)
        {
            if (previous is null)
            {
                return true;
            }

            return previous.Status != current.Status
                || previous.StartedAtUtc != current.StartedAtUtc
                || previous.CompletedAtUtc != current.CompletedAtUtc
                || previous.Error != current.Error;
        }
    }
}
