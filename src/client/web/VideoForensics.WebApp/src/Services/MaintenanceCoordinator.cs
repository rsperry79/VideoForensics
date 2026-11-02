using VideoForensics.Client.Hosting.Maintenance;
using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Client.WebApp.Services
{
    /// <summary>Thrown when a maintenance run is requested while another is already in progress.</summary>
    public sealed class MaintenanceInProgressException(string message, Exception? inner = null) : Exception(message, inner);

    /// <summary>
    /// Runs one server maintenance operation with the same drain the update uses: block new work, wait for in-flight work
    /// to finish, release pooled database connections so the file can be rebuilt, run the operation, then end maintenance
    /// so paused work resumes on its next tick.
    /// </summary>
    public sealed class MaintenanceCoordinator(
        IMaintenanceGate gate,
        IDatabaseMaintenanceService lifecycle,
        IServiceScopeFactory scopes,
        ILogger<MaintenanceCoordinator> logger,
        MaintenanceHoldTimer holdTimer)
    {
        private const string AlreadyInProgressMessage = "Server maintenance is already in progress.";

        /// <summary>
        /// Runs <paramref name="operation"/> under maintenance and returns its result. Throws <see cref="InvalidOperationException"/>
        /// when another maintenance run is already in progress, so two runs never overlap.
        /// </summary>
        /// <param name="holdOnSuccess">
        /// Leave maintenance running after a successful operation, so paused work stays blocked. Use when the operation
        /// leaves the running process unsafe to continue, such as a database file that has moved. The caller must then
        /// stop the process. A failed operation always ends maintenance.
        /// </param>
        public async Task<T> RunAsync<T>(string description, Func<IServiceProvider, CancellationToken, Task<T>> operation, CancellationToken ct, bool holdOnSuccess = false)
        {
            bool succeeded = false;
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
                succeeded = true;
                return result;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError("Server maintenance failed: {Description}: {Message} ({ExceptionType})", description, ex.Message, ex.GetType().Name);
                throw;
            }
            finally
            {
                if (!(holdOnSuccess && succeeded))
                {
                    gate.End();
                }
            }
        }

        /// <summary>
        /// Puts the server into maintenance and keeps it there until <see cref="EndHold"/> is called with the returned token,
        /// <see cref="RenewHold"/> stops being called, or the safety timeout ends it. For an offline tool that opens the database
        /// file itself: the server drains work, releases its pooled connections, then waits. Throws
        /// <see cref="MaintenanceInProgressException"/> when another hold or maintenance run is already in progress.
        /// </summary>
        /// <returns>The hold token. Only this token can end the hold.</returns>
        public async Task<Guid> BeginHoldAsync(string description, CancellationToken ct)
        {
            // Why: the slot is taken before the gate changes state, so a second begin fails here without touching the gate.
            // The safety timer is armed only after the drain, so a slow drain never counts against the hold's 30 minutes.
            Guid? token = holdTimer.TryReserve(OnHoldExpired);
            if (token is null)
            {
                throw new MaintenanceInProgressException(AlreadyInProgressMessage, new InvalidOperationException(AlreadyInProgressMessage));
            }

            // Why: only a gate that this call began draining is ours to end. A gate busy with another run must be left alone.
            bool gateClaimed = false;
            try
            {
                Task drained;
                try
                {
                    drained = gate.BeginDrainAsync(description, ct);
                }
                catch (InvalidOperationException ex)
                {
                    throw new MaintenanceInProgressException(ex.Message, ex);
                }

                gateClaimed = true;
                await drained;
                gate.MarkRunning(description);
                await lifecycle.ReleaseAllConnectionsAsync(ct);
                _ = holdTimer.ArmSafetyTimer(token.Value);
            }
            catch (Exception ex)
            {
                // Why: a failed or cancelled drain must not leave the server stuck in Draining with no hold to end.
                _ = holdTimer.TryEnd(token.Value, () =>
                {
                    if (gateClaimed)
                    {
                        ReleaseGate();
                    }
                });

                if (ex is not OperationCanceledException and not MaintenanceInProgressException)
                {
                    logger.LogError("Server maintenance hold failed: {Description}: {Message} ({ExceptionType})", description, ex.Message, ex.GetType().Name);
                }

                throw;
            }

            logger.LogInformation("Server maintenance hold started: {Description}", description);
            return token.Value;
        }

        /// <summary>
        /// Ends the hold when <paramref name="token"/> is the active one. Returns false, and leaves maintenance alone, when the
        /// token does not match an active hold. Safe to call more than once.
        /// </summary>
        public bool EndHold(Guid token)
        {
            bool ended = holdTimer.TryEnd(token, ReleaseGate);
            if (ended)
            {
                logger.LogInformation("Server maintenance hold ended");
            }

            return ended;
        }

        /// <summary>Restarts the safety timeout of the active hold. Returns false when the token does not match an active hold or the hold reached its maximum age.</summary>
        public bool RenewHold(Guid token) => RenewHoldOutcome(token) == HoldRenewResult.Renewed;

        /// <summary>Restarts the safety timeout of the active hold and reports why it was not renewed, when it was not.</summary>
        public HoldRenewResult RenewHoldOutcome(Guid token) => holdTimer.RenewOutcome(token);

        // Runs on the timer thread when a hold is not ended in time. Never throws: an exception here would crash the process.
        private void OnHoldExpired()
        {
            ReleaseGate();
            logger.LogInformation("Maintenance hold expired at its safety timeout");
        }

        // Why: the hold is reported as ended even when the gate fails to release, so a failure here is logged and never returned to the caller.
        private void ReleaseGate()
        {
            try
            {
                gate.End();
            }
            catch (Exception ex)
            {
                logger.LogError("Server maintenance end failed: {Message} ({ExceptionType})", ex.Message, ex.GetType().Name);
            }
        }
    }
}
