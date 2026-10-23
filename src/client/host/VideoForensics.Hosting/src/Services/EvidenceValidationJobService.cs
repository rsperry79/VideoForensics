using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Hosting.Services
{
    /// <summary>
    /// In-memory registry of evidence validation jobs. Work runs on a background task under its own DI scope with
    /// <see cref="CancellationToken.None"/>, so a page disconnecting never cancels it. Holds only the latest job per kind.
    /// </summary>
    public sealed class EvidenceValidationJobService : IEvidenceValidationJobs
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<EvidenceValidationJobService> _logger;
        private readonly object _gate = new();
        private readonly Dictionary<EvidenceValidationJobKind, JobRecord> _latest = new();

        /// <summary>Creates the registry.</summary>
        public EvidenceValidationJobService(IServiceScopeFactory scopeFactory, ILogger<EvidenceValidationJobService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        /// <inheritdoc />
        public Task<EvidenceValidationJob> StartIntegrityAsync(Guid? deviceId, CancellationToken ct) =>
            StartIntegrity(deviceId is { } id ? new[] { id } : null, ct);

        /// <inheritdoc />
        public Task<EvidenceValidationJob> StartIntegrityForDevicesAsync(IReadOnlyList<Guid> deviceIds, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(deviceIds);
            if (deviceIds.Count == 0)
            {
                throw new ArgumentException("At least one device is required.", nameof(deviceIds));
            }

            return StartIntegrity(deviceIds.Distinct().ToArray(), ct);
        }

        /// <summary>
        /// Starts the one integrity job, or returns the running one. A null list means every device; a list verifies
        /// those devices in turn inside the same job.
        /// </summary>
        private Task<EvidenceValidationJob> StartIntegrity(IReadOnlyList<Guid>? deviceIds, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            JobRecord job;
            EvidenceValidationJob snapshot;
            lock (_gate)
            {
                if (TryGetRunning(EvidenceValidationJobKind.Integrity, out JobRecord? running))
                {
                    // Joining is only safe for the same devices; otherwise the caller would get results for the wrong set.
                    if (SameScope(running!.Scope, deviceIds))
                    {
                        return Task.FromResult(running.Snapshot());
                    }

                    return Task.FromException<EvidenceValidationJob>(new InvalidOperationException(
                        "An integrity check is already running for a different set of devices. Wait for it to finish."));
                }

                job = NewJob(EvidenceValidationJobKind.Integrity);
                job.Scope = deviceIds;
                job.Append("Integrity check started");
                _latest[EvidenceValidationJobKind.Integrity] = job;
                snapshot = job.Snapshot();
            }

            _logger.LogInformation("Evidence integrity check started");
            _ = Task.Run(() => RunIntegrityAsync(job, deviceIds));
            return Task.FromResult(snapshot);
        }

        /// <inheritdoc />
        public Task<EvidenceValidationJob> StartReconcileAsync(
            Guid deviceId,
            string providerDeviceId,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            JobRecord job;
            EvidenceValidationJob snapshot;
            lock (_gate)
            {
                if (TryGetRunning(EvidenceValidationJobKind.Reconcile, out JobRecord? running))
                {
                    return Task.FromResult(running!.Snapshot());
                }

                job = NewJob(EvidenceValidationJobKind.Reconcile);
                job.Append("Reconcile started");
                _latest[EvidenceValidationJobKind.Reconcile] = job;
                snapshot = job.Snapshot();
            }

            _logger.LogInformation("Evidence provider reconcile started");
            _ = Task.Run(() => RunReconcileAsync(job, deviceId, providerDeviceId, fromUtc, toUtc));
            return Task.FromResult(snapshot);
        }

        /// <inheritdoc />
        public Task<EvidenceValidationJob?> GetLatestAsync(EvidenceValidationJobKind kind, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            lock (_gate)
            {
                return Task.FromResult(_latest.TryGetValue(kind, out JobRecord? job) ? job.Snapshot() : null);
            }
        }

        private async Task RunIntegrityAsync(JobRecord job, IReadOnlyList<Guid>? deviceIds)
        {
            try
            {
                await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
                IEvidenceValidationService validation = scope.ServiceProvider.GetRequiredService<IEvidenceValidationService>();
                var progress = new DelegateProgress(line => AppendLog(job, line));

                List<MediaVerificationResult> results = new();
                if (deviceIds is null)
                {
                    results.AddRange(await validation.VerifyLocalIntegrityAsync(null, CancellationToken.None, progress));
                }
                else
                {
                    // One job, one device after another; each result is tagged so the combined list stays attributable.
                    foreach (Guid deviceId in deviceIds)
                    {
                        IReadOnlyList<MediaVerificationResult> deviceResults =
                            await validation.VerifyLocalIntegrityAsync(deviceId, CancellationToken.None, progress);
                        foreach (MediaVerificationResult result in deviceResults)
                        {
                            result.DeviceId = deviceId;
                            results.Add(result);
                        }

                        // Publish as each device finishes, so a later device failing keeps the results already verified.
                        lock (_gate)
                        {
                            job.IntegrityResults = results.ToArray();
                        }
                    }
                }

                lock (_gate)
                {
                    job.IntegrityResults = results.ToArray();
                    job.Append($"Finished: {results.Count} results");
                    Complete(job, EvidenceValidationJobState.Succeeded, errorMessage: null);
                }

                _logger.LogInformation("Evidence integrity check finished");
            }
            catch (Exception ex)
            {
                Fail(job, ex);
            }
        }

        private async Task RunReconcileAsync(JobRecord job, Guid deviceId, string providerDeviceId, DateTime fromUtc, DateTime toUtc)
        {
            try
            {
                await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
                IEvidenceValidationService validation = scope.ServiceProvider.GetRequiredService<IEvidenceValidationService>();
                IReadOnlyList<ReconciliationDiscrepancy> discrepancies = await validation.ReconcileWithProviderAsync(
                    deviceId, providerDeviceId, fromUtc, toUtc, CancellationToken.None,
                    new DelegateProgress(line => AppendLog(job, line)));

                lock (_gate)
                {
                    job.Discrepancies = discrepancies.ToArray();
                    job.Append($"Finished: {discrepancies.Count} discrepancies");
                    Complete(job, EvidenceValidationJobState.Succeeded, errorMessage: null);
                }

                _logger.LogInformation("Evidence provider reconcile finished");
            }
            catch (Exception ex)
            {
                Fail(job, ex);
            }
        }

        /// <summary>Appends a live progress line while the run is still in progress.</summary>
        private void AppendLog(JobRecord job, string line)
        {
            lock (_gate)
            {
                job.Append(line);
            }
        }

        private void Fail(JobRecord job, Exception ex)
        {
            // Message only: the stack trace stays out of the job log and the logger output.
            string message = ex.Message;
            lock (_gate)
            {
                job.Append(message);
                Complete(job, EvidenceValidationJobState.Failed, message);
            }

            _logger.LogError("Evidence validation job failed: {Reason}", message);
        }

        /// <summary>Two integrity scopes match when both mean every device, or both list the same devices.</summary>
        private static bool SameScope(IReadOnlyList<Guid>? running, IReadOnlyList<Guid>? requested)
        {
            if (running is null || requested is null)
            {
                return running is null && requested is null;
            }

            return running.ToHashSet().SetEquals(requested);
        }

        /// <summary>Caller must hold <see cref="_gate"/>.</summary>
        private bool TryGetRunning(EvidenceValidationJobKind kind, out JobRecord? running)
        {
            if (_latest.TryGetValue(kind, out JobRecord? existing) && existing.State == EvidenceValidationJobState.Running)
            {
                running = existing;
                return true;
            }

            running = null;
            return false;
        }

        /// <summary>Caller must hold <see cref="_gate"/>.</summary>
        private static void Complete(JobRecord job, EvidenceValidationJobState state, string? errorMessage)
        {
            job.State = state;
            job.ErrorMessage = errorMessage;
            job.CompletedUtc = DateTime.UtcNow;
        }

        private static JobRecord NewJob(EvidenceValidationJobKind kind) => new()
        {
            Id = Guid.NewGuid(),
            Kind = kind,
            State = EvidenceValidationJobState.Running,
            StartedUtc = DateTime.UtcNow
        };

        /// <summary>
        /// Synchronous <see cref="IProgress{T}"/> that runs its action inline. <see cref="System.Progress{T}"/> is
        /// not used because it posts to the captured SynchronizationContext, which would defer log lines.
        /// </summary>
        private sealed class DelegateProgress : IProgress<string>
        {
            private readonly Action<string> _report;

            public DelegateProgress(Action<string> report)
            {
                _report = report;
            }

            public void Report(string value) => _report(value);
        }

        /// <summary>Mutable job state. Only touched while holding <see cref="_gate"/>.</summary>
        private sealed class JobRecord
        {
            private readonly List<string> _log = new();

            public required Guid Id { get; init; }
            public required EvidenceValidationJobKind Kind { get; init; }
            public EvidenceValidationJobState State { get; set; }
            public required DateTime StartedUtc { get; init; }

            /// <summary>Devices this integrity run covers; null means every device.</summary>
            public IReadOnlyList<Guid>? Scope { get; set; }
            public DateTime? CompletedUtc { get; set; }
            public MediaVerificationResult[]? IntegrityResults { get; set; }
            public ReconciliationDiscrepancy[]? Discrepancies { get; set; }
            public string? ErrorMessage { get; set; }

            /// <summary>
            /// Appends a timestamped log line. A long run (one line per file) keeps the newest lines: the log is capped
            /// so polling snapshots stay small, and the oldest lines drop first.
            /// </summary>
            public void Append(string text)
            {
                if (_log.Count >= MaxLogLines)
                {
                    _log.RemoveAt(0);
                }

                _log.Add($"{DateTime.UtcNow:HH:mm:ss} {text}");
            }

            private const int MaxLogLines = 1000;

            /// <summary>Copies the current state so the caller never shares the mutable lists.</summary>
            public EvidenceValidationJob Snapshot() => new()
            {
                Id = Id,
                Kind = Kind,
                State = State,
                StartedUtc = StartedUtc,
                CompletedUtc = CompletedUtc,
                Log = _log.ToArray(),
                IntegrityResults = IntegrityResults?.ToArray(),
                Discrepancies = Discrepancies?.ToArray(),
                ErrorMessage = ErrorMessage
            };
        }
    }
}
