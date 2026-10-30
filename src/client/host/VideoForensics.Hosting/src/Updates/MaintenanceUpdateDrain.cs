using VideoForensics.Client.Hosting.Maintenance;

namespace VideoForensics.Client.Hosting.Updates
{
    /// <summary>
    /// The update drain. Holds back new work through the maintenance gate and waits for in-flight work to finish before
    /// the staged update is applied and the service restarts.
    /// </summary>
    public sealed class MaintenanceUpdateDrain(IMaintenanceGate gate) : IUpdateDrain
    {
        /// <inheritdoc />
        public Task DrainAsync(CancellationToken ct) => gate.BeginDrainAsync("Applying a server update", ct);
    }
}
