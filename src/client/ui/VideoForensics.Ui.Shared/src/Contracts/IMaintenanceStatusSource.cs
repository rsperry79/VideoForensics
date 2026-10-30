namespace VideoForensics.Client.Ui.Shared.Contracts
{
    /// <summary>
    /// Host-agnostic source of server maintenance state for <c>MaintenanceBanner</c>. The banner subscribes instead of
    /// polling. Remote hosts forward the server's broadcast; the WebApp reads the server's own gate. Both replay the current
    /// state to a new subscriber.
    /// </summary>
    public interface IMaintenanceStatusSource
    {
        /// <summary>Stream of maintenance states. Subscribers must dispose their subscription.</summary>
        IObservable<MaintenanceStatusSnapshot> Status { get; }
    }

    /// <summary>Server maintenance state as the UI sees it.</summary>
    /// <param name="Phase">"Idle", "Draining" or "Running".</param>
    /// <param name="Message">What the server is doing, shown in the banner.</param>
    public sealed record MaintenanceStatusSnapshot(string Phase, string Message)
    {
        /// <summary>True while the server is blocking new work.</summary>
        public bool IsActive => Phase != "Idle";
    }
}
