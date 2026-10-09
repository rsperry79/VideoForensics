using VideoForensics.Api.Contracts;
using VideoForensics.Hosting.Contracts;
using VideoForensics.Ui.Shared.Contracts;

namespace VideoForensics.Hosting.Remote
{
    /// <summary>
    /// <see cref="ISelfTestStatusSource"/> for remote (MAUI) hosts. It forwards the server's self-test status from the
    /// shared <see cref="IRealtimeStore"/>, which is the only subscriber to the <c>SelfTestStatus</c> stream.
    /// <para>
    /// The stream is admin-only on the server. A non-admin session receives no emissions, so the page stays silent
    /// for it. The page is admin-gated, so this does not reach a user who should see status.
    /// </para>
    /// <para>
    /// No mapping is needed: Ui.Shared uses the wire <see cref="SelfTestStatusDto"/> directly, so the stream is
    /// forwarded as-is. The store replays its latest status to a new subscriber, which is the page's initial state.
    /// </para>
    /// </summary>
    public sealed class RemoteSelfTestStatusSource : ISelfTestStatusSource
    {
        private readonly IRealtimeStore _store;

        /// <summary>Creates the source over the singleton realtime store.</summary>
        public RemoteSelfTestStatusSource(IRealtimeStore store)
        {
            _store = store;
        }

        /// <inheritdoc />
        public IObservable<SelfTestStatusDto> Status => _store.SelfTestStatus;
    }
}
