using VideoForensics.Client.Hosting.Maintenance;
using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Client.WebApp.Services
{
    /// <summary>Thrown when a maintenance run is requested while another is already in progress.</summary>
    public sealed class MaintenanceInProgressException(string message, Exception inner) : Exception(message, inner);

    /// <summary>
    /// Runs one server maintenance operation with the same drain the update uses: block new work, wait for in-flight work
    /// to finish, release pooled database connections so the file can be rebuilt, run the operation, then end maintenance
    /// so paused work resumes on its next tick.
    /// </summary>
    public sealed class MaintenanceCoordinator(
        IMaintenanceGate gate,
        IDatabaseMaintenanceService lifecycle,
        IServiceScopeFactory scopes,
        ILogger<MaintenanceCoordinator> logger)
    {
        /// <summary>
        /// Runs <paramref name="operation"/> under maintenance and returns its result. Throws <see cref="InvalidOperationException"/>
        /// when another maintenance run is already in progress, so two runs never overlap.
        /// </summary>
        public async Task<T> RunAsync<T>(string description, Func<IServiceProvider, CancellationToken, Task<T>> operation, CancellationToken ct)
        {
            // The gate throws before it changes state when a run is already active, so this run never ends the other one.
            Task drained;
            try
            {
                drained = gate.BeginDrainAsync(description, ct);
            }
            catch (InvalidOperationException ex)
            {
                throw new MaintenanceInProgressException(ex.Message, ex);
            }

            try
            {
                await drained;
                gate.MarkRunning(description);
                await lifecycle.ReleaseAllConnectionsAsync(ct);

                using IServiceScope scope = scopes.CreateScope();
                T result = await operation(scope.ServiceProvider, ct);
                logger.LogInformation("Server maintenance finished: {Description}", description);
                return result;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError("Server maintenance failed: {Description}: {Message} ({ExceptionType})", description, ex.Message, ex.GetType().Name);
                throw;
            }
            finally
            {
                gate.End();
            }
        }
    }
}
