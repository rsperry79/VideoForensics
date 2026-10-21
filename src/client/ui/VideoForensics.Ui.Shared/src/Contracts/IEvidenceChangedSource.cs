using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Ui.Shared.Contracts
{
    /// <summary>
    /// Host-agnostic source of "evidence changed" events, so an open evidence view reloads without an operator pressing
    /// Refresh. The WebApp receives it in-process from the writers; remote hosts (MAUI) forward the server's realtime push.
    /// Events are not replayed: a new subscriber hears only later changes.
    /// </summary>
    public interface IEvidenceChangedSource
    {
        /// <summary>Stream of evidence changes. Subscribers must dispose their subscription.</summary>
        IObservable<EvidenceChangedDto> EvidenceChanged { get; }
    }
}
