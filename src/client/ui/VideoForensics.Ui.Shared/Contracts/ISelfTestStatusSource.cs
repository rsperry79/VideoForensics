using VideoForensics.Api.Contracts;

namespace VideoForensics.Ui.Shared.Contracts
{
    /// <summary>
    /// Host-agnostic source of Ring self-test run status for <c>RingSelfTest</c>. The page subscribes instead of
    /// polling, so it does not depend on how a host gets its data.
    /// <para>
    /// Local hosts (WebApp) sample <c>IRingSelfTestService.GetStatusAsync</c> and emit only when the status changes.
    /// Remote hosts (MAUI) forward the server's admin-only realtime stream. Both replay the most recent status to
    /// a new subscriber. A non-admin remote session receives no emissions, which is expected because the page is
    /// admin-gated.
    /// </para>
    /// <para>
    /// The status is the wire DTO itself, not a Ui.Shared copy. Ui.Shared already binds <see cref="SelfTestStatusDto"/>
    /// through its page, so a mirror record would add a mapping step and no isolation.
    /// </para>
    /// </summary>
    public interface ISelfTestStatusSource
    {
        /// <summary>Stream of status snapshots. Subscribers must dispose their subscription.</summary>
        IObservable<SelfTestStatusDto> Status { get; }
    }
}
