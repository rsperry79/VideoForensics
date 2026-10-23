using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Services;
using VideoForensics.Providers.Ring.Exceptions;
using VideoForensics.Providers.Ring.Services;

using Device = VideoForensics.Data.Common.Entities.Device;
using Location = VideoForensics.Data.Common.Entities.Location;

namespace VideoForensics.Client.Hosting.BackgroundServices
{
    /// <summary>
    /// Captures a snapshot for every enabled device of every active Ring account, once per account interval, and records every
    /// attempt in <see cref="SnapshotCapture"/>. The interval is the account's stored SnapshotIntervalSeconds when set, otherwise 60 s.
    /// A device is skipped when its Device.SnapshotCaptureEnabled flag is off, when the provider's <see cref="IProviderDeviceStatus"/>
    /// reports it offline, or when its type has no camera (see <see cref="HasCamera"/>). An unknown online state (null) is not skipped.
    ///
    /// Each attempt runs through <see cref="IProviderSnapshotCapture"/>, which takes the account's pacer turns and handles rate limits
    /// (pause, alert, spacing penalty). A 429 is recorded as a gap and the account is held back for the pause the provider reported.
    /// Rows hold only the attempt record. A 200 also writes the JPEG under SnapshotRoot\&lt;account&gt;\&lt;doorbotId&gt;\yyyyMMddTHHmmssfffZ.jpg.
    ///
    /// Server-tier only (VideoForensics.Client.WebApp, registered through AddVideoForensicsSnapshotCapture). Logs use the provider name
    /// ("Ring account") and the device name, never account or device GUIDs.
    /// </summary>
    public class SnapshotCaptureService : BackgroundService
    {
        internal const string RingProviderName = "Ring";

        /// <summary>Capture interval for an account with no stored SnapshotIntervalSeconds.</summary>
        internal const int DefaultIntervalSeconds = 60;

        /// <summary>Metadata on a capture's Media row. The "snapshot" type is what the Devices tab and the roll-up look for.</summary>
        internal const string CaptureSnapshotMetadata = "{\"type\":\"snapshot\",\"source\":\"capture\"}";

        /// <summary>Wait between the trigger and the read of one attempt.</summary>
        internal static readonly TimeSpan SettleDelay = TimeSpan.FromSeconds(5);

        /// <summary>How often the loop checks which devices are due.</summary>
        internal static readonly TimeSpan TickPeriod = TimeSpan.FromSeconds(5);

        /// <summary>Hold-off for an account whose credentials could not be restored or whose pass threw, so it is not retried every tick.</summary>
        private static readonly TimeSpan AccountErrorBackoff = TimeSpan.FromMinutes(1);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IOptions<SnapshotTimelineOptions> _options;
        private readonly ILogger<SnapshotCaptureService> _logger;
        private readonly TimeProvider _time;

        // Process-lifetime guards. The Ring pacers live on a per-tick scoped instance, so a provider pause must be remembered here
        // to hold across ticks. Keyed by internal GUIDs; never logged.
        private readonly ConcurrentDictionary<Guid, DateTimeOffset> _accountPausedUntil = new();
        private readonly ConcurrentDictionary<Guid, DateTimeOffset> _deviceRetryAfter = new();
        private readonly ThreadHeartbeats? _heartbeats;

        /// <summary>Creates the capture service.</summary>
        public SnapshotCaptureService(
            IServiceScopeFactory scopeFactory,
            IOptions<SnapshotTimelineOptions> options,
            ILogger<SnapshotCaptureService> logger,
            TimeProvider time,
            ThreadHeartbeats? heartbeats = null)
        {
            _scopeFactory = scopeFactory;
            _options = options;
            _logger = logger;
            _time = time;
            _heartbeats = heartbeats;
        }

        /// <summary>
        /// Pauses the account until Ring's limit lifts. A hard ban gives an exact end time; otherwise Retry-After is used,
        /// and the account error backoff is the fallback. Logged as a warning because it is an expected external state.
        /// </summary>
        private void PauseForRingThrottle(ProviderAccount account, string accountName, ThrottledException throttled)
        {
            DateTimeOffset now = _time.GetUtcNow();
            DateTimeOffset until = throttled.BlockedUntilUtc is { } blockedUntil
                ? new DateTimeOffset(DateTime.SpecifyKind(blockedUntil, DateTimeKind.Utc))
                : throttled.RetryAfter is { } retryAfter
                    ? now + retryAfter
                    : now + AccountErrorBackoff;

            _accountPausedUntil[account.Id] = until;
            _logger.LogWarning("Ring is rate-limiting {AccountName}; snapshot capture is paused until {ResumeAt:t} local time", accountName, until.ToLocalTime());
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TickPeriod, _time);

            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    _heartbeats?.Beat(nameof(SnapshotCaptureService), _time.GetUtcNow());
                    try
                    {
                        await RunOneTickAsync(stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Snapshot capture tick failed; will retry on the next tick");
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown.
            }
        }

        /// <summary>Internal for direct testability (VideoForensics.Client.Hosting.Tests) without driving the BackgroundService timer.</summary>
        internal async Task RunOneTickAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            using IServiceScope scope = _scopeFactory.CreateScope();
            IServiceProvider sp = scope.ServiceProvider;

            IReadOnlyList<ProviderAccount> accounts = await sp.GetRequiredService<IProviderAccountRepository>().ListActiveAsync(ct);
            var counts = new TickCounts();

            foreach (ProviderAccount account in accounts)
            {
                if (account.RemovedUtc != null || !account.IsActive
                    || !string.Equals(account.ProviderName, RingProviderName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ct.ThrowIfCancellationRequested();
                try
                {
                    await CaptureAccountAsync(sp, account, counts, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // One account's failure must never stop the others.
                    _accountPausedUntil[account.Id] = _time.GetUtcNow() + AccountErrorBackoff;
                    _logger.LogError(ex, "Snapshot capture failed for {ProviderName} account", account.ProviderName);
                }
            }

            if (counts.Attempted > 0)
            {
                _logger.LogInformation("Snapshot capture tick: {Captured} captured, {Failed} failed, {Throttled} throttled",
                    counts.Captured, counts.Failed, counts.Throttled);
            }
        }

        private async Task CaptureAccountAsync(IServiceProvider sp, ProviderAccount account, TickCounts counts, CancellationToken ct)
        {
            Guid accountId = account.Id;
            string accountName = $"{account.ProviderName} account";
            DateTimeOffset now = _time.GetUtcNow();

            if (IsPaused(accountId, now))
            {
                return;
            }

            SyncSchedule? schedule = await sp.GetRequiredService<ISyncScheduleRepository>().GetByProviderAccountIdAsync(accountId, ct);
            // The schedule's snapshot interval (with any jamming window) sets the spacing. A stored SnapshotIntervalSeconds raised by
            // a Ring rate-limit incident is a floor on top of it, so the adaptive back-off is never undone by the form.
            int seconds = schedule is null
                ? DefaultIntervalSeconds
                : Math.Max(ScheduleCalculator.EffectiveSnapshotIntervalSeconds(schedule, _time.GetUtcNow().UtcDateTime), schedule.SnapshotIntervalSeconds ?? 0);
            var interval = TimeSpan.FromSeconds(seconds);

            IDeviceRepository devices = sp.GetRequiredService<IDeviceRepository>();
            ISnapshotCaptureRepository captures = sp.GetRequiredService<ISnapshotCaptureRepository>();
            IProviderDeviceStatus deviceStatus = sp.GetRequiredKeyedService<IProviderDeviceStatus>(RingProviderName);
            var due = new List<Device>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (Location location in await sp.GetRequiredService<ILocationRepository>().ListByProviderAccountIdAsync(accountId, ct))
            {
                foreach (Device device in await devices.GetByLocationIdAsync(location.Id, ct))
                {
                    // Offline or camera-less devices would only log a failed attempt each interval, so they are not due.
                    if (!seen.Add(device.ProviderDeviceId) || !device.SnapshotCaptureEnabled
                        || deviceStatus.IsOffline(device.Type, device.IsOnline) || !HasCamera(device.Type))
                    {
                        continue;
                    }

                    if (_deviceRetryAfter.TryGetValue(device.Id, out DateTimeOffset retryAfter) && retryAfter > now)
                    {
                        continue;
                    }

                    // Any attempt inside the window (current or failed) means the device has already been captured this interval.
                    IReadOnlyList<SnapshotCapture> recent = await captures.GetAttemptsForDeviceAsync(device.Id, now - interval, now, ct);
                    if (recent.Count == 0)
                    {
                        due.Add(device);
                    }
                }
            }

            if (due.Count == 0)
            {
                return;
            }

            if (!await EnsureSessionAsync(sp, accountId, accountName, ct))
            {
                _accountPausedUntil[accountId] = _time.GetUtcNow() + AccountErrorBackoff;
                return;
            }

            IProviderSnapshotCapture provider = sp.GetRequiredKeyedService<IProviderSnapshotCapture>(RingProviderName);
            foreach (Device device in due)
            {
                ct.ThrowIfCancellationRequested();

                // A rate limit earlier in this tick (or an earlier tick) holds the whole account until its pause ends.
                if (IsPaused(accountId, _time.GetUtcNow()))
                {
                    break;
                }

                await CaptureDeviceAsync(sp, provider, account, accountName, device, interval, counts, ct);
            }
        }

        private async Task<bool> EnsureSessionAsync(IServiceProvider sp, Guid accountId, string accountName, CancellationToken ct)
        {
            if (sp.GetRequiredService<ISessionProvider>().GetSession(accountId) != null)
            {
                return true;
            }

            bool restored = await sp.GetRequiredKeyedService<IProviderAuthService>(RingProviderName).RestoreFromSavedCredentialsAsync(accountId, ct);
            if (!restored)
            {
                _logger.LogWarning("Snapshot capture skipped: could not restore saved credentials for {AccountName}; retrying later", accountName);
            }

            return restored;
        }

        private async Task CaptureDeviceAsync(
            IServiceProvider sp,
            IProviderSnapshotCapture provider,
            ProviderAccount account,
            string accountName,
            Device device,
            TimeSpan interval,
            TickCounts counts,
            CancellationToken ct)
        {
            // Each capture is two Ring calls, so the shared Ring budget is checked first. A busy window defers the device to a later tick.
            IProviderApiBudgetGuard? budgetGuard = sp.GetService<IProviderApiBudgetGuard>();
            if (budgetGuard != null && !await budgetGuard.TryConsumeAsync(RingProviderName, ct))
            {
                _logger.LogInformation("Snapshot capture deferred for {DeviceName} on {AccountName}: Ring API budget exhausted for this window", device.Name, accountName);
                return;
            }

            // The clock read before the provider call is the attempt time: Ring gives no capture timestamp of its own.
            DateTimeOffset attemptedAt = _time.GetUtcNow();
            SnapshotCaptureAttempt attempt;

            try
            {
                attempt = await provider.CaptureAsync(account.Id, device.ProviderDeviceId, SettleDelay, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (ThrottledException throttled)
            {
                // Ring is rate-limiting the account. Pause it until the limit lifts instead of retrying every device each tick.
                PauseForRingThrottle(account, accountName, throttled);
                Hold(device.Id, interval);
                counts.Failed++;
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Snapshot capture failed for {DeviceName} on {AccountName}", device.Name, accountName);
                Hold(device.Id, interval);
                counts.Failed++;
                return;
            }

            if (budgetGuard != null)
            {
                await budgetGuard.RecordCallAsync(RingProviderName, ct);
                await budgetGuard.RecordCallAsync(RingProviderName, ct);
            }

            if (attempt.ThrottlePause is { } pause)
            {
                _accountPausedUntil[account.Id] = _time.GetUtcNow() + pause;
            }

            if (attempt.HttpStatus is not int status)
            {
                _logger.LogError("Snapshot capture for {DeviceName} on {AccountName} returned no HTTP status", device.Name, accountName);
                Hold(device.Id, interval);
                counts.Failed++;
                return;
            }

            string? mediaFilePath = null;
            if (status == 200)
            {
                if (attempt.Image is not { Length: > 0 } image)
                {
                    _logger.LogError("Snapshot capture for {DeviceName} on {AccountName} returned 200 without an image", device.Name, accountName);
                    Hold(device.Id, interval);
                    counts.Failed++;
                    return;
                }

                try
                {
                    mediaFilePath = WriteImage(account.Id, device.ProviderDeviceId, attemptedAt, image);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Could not write the snapshot for {DeviceName} on {AccountName}", device.Name, accountName);
                    Hold(device.Id, interval);
                    counts.Failed++;
                    return;
                }
            }

            try
            {
                var row = SnapshotCapture.Create(device.Id, attemptedAt, "fresh", status, mediaFilePath, deviceHealthId: null);
                _ = await sp.GetRequiredService<ISnapshotCaptureRepository>().AddAsync(row, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Could not record the snapshot attempt for {DeviceName} on {AccountName}", device.Name, accountName);
                counts.Failed++;
                return;
            }

            if (mediaFilePath is not null)
            {
                // WriteImage returns a path relative to the snapshot root; Media rows hold the full path.
                await RecordCaptureMediaAsync(sp, device, Path.Combine(_options.Value.SnapshotRoot, mediaFilePath), attemptedAt, accountName, ct);
            }

            switch (status)
            {
                case 200:
                    counts.Captured++;
                    break;
                case 429:
                    // The rate limit itself is logged and alerted by the provider; the attempt is a gap.
                    counts.Throttled++;
                    break;
                default:
                    _logger.LogError("Snapshot capture for {DeviceName} on {AccountName} returned HTTP {HttpStatus}", device.Name, accountName, status);
                    counts.Failed++;
                    break;
            }
        }

        /// <summary>
        /// Writes the JPEG under SnapshotRoot\&lt;account folder&gt;\&lt;doorbotId&gt;\ and returns the path relative to SnapshotRoot. The account folder
        /// is the account GUID without dashes, the same folder the event media pull uses.
        /// <summary>
        /// Registers a successful capture as a snapshot Media row, so it appears on the Devices tab and joins the roll-up loop
        /// alongside the snapshots the scheduled pull records. Best-effort: a failed write is logged and never fails the capture.
        /// </summary>
        private async Task RecordCaptureMediaAsync(IServiceProvider sp, Device device, string mediaFilePath, DateTimeOffset capturedAt, string accountName, CancellationToken ct)
        {
            try
            {
                byte[] hash;
                await using (FileStream stream = File.OpenRead(mediaFilePath))
                {
                    hash = await SHA256.HashDataAsync(stream, ct);
                }

                await sp.GetRequiredService<IMediaItemRepository>().AddAsync(new MediaItem
                {
                    Id = Guid.NewGuid(),
                    DeviceId = device.Id,
                    FileName = Path.GetFileName(mediaFilePath),
                    FilePath = mediaFilePath,
                    MediaFormat = "jpg",
                    FileSizeBytes = new FileInfo(mediaFilePath).Length,
                    RecordedAtUtc = capturedAt.UtcDateTime,
                    DownloadedAtUtc = DateTime.UtcNow,
                    Sha256Hash = Convert.ToHexString(hash).ToLowerInvariant(),
                    IntegrityVerified = false,
                    MetadataJson = CaptureSnapshotMetadata,
                }, ct);

                IEvidenceChangeNotifier? evidenceChanges = sp.GetService<IEvidenceChangeNotifier>();
                if (evidenceChanges != null)
                {
                    await evidenceChanges.EvidenceChangedAsync(device.Id, ct);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Could not record the captured snapshot in Media for {DeviceName} on {AccountName}", device.Name, accountName);
            }
        }

        /// </summary>
        private string WriteImage(Guid accountId, string providerDeviceId, DateTimeOffset capturedAtUtc, byte[] image)
        {
            if (!int.TryParse(providerDeviceId, NumberStyles.None, CultureInfo.InvariantCulture, out _))
            {
                throw new ArgumentException("Ring device ids are numeric.", nameof(providerDeviceId));
            }

            string accountFolder = accountId.ToString("N");
            string fileName = capturedAtUtc.UtcDateTime.ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture) + ".jpg";
            string relativePath = Path.Combine(accountFolder, providerDeviceId, fileName);
            string fullPath = Path.Combine(_options.Value.SnapshotRoot, relativePath);

            _ = Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllBytes(fullPath, image);
            return relativePath;
        }

        /// <summary>
        /// True for the Ring device types that carry a camera. Allowlisted rather than denylisted: a chime, a range extender or any
        /// unrecognised type has no camera to snapshot, so it is skipped by default.
        /// </summary>
        internal static bool HasCamera(string? type)
        {
            return type is "doorbot" or "authorized_doorbot" or "stickup_cam";
        }

        /// <summary>Holds a device back for one interval after a failure that left no attempt row, so it is not retried every tick.</summary>
        private void Hold(Guid deviceId, TimeSpan interval)
        {
            _deviceRetryAfter[deviceId] = _time.GetUtcNow() + interval;
        }

        private bool IsPaused(Guid accountId, DateTimeOffset now)
        {
            return _accountPausedUntil.TryGetValue(accountId, out DateTimeOffset until) && until > now;
        }

        /// <summary>Per-tick outcome counts, logged once per tick. Counts only, never identifiers.</summary>
        private sealed class TickCounts
        {
            public int Captured { get; set; }

            public int Failed { get; set; }

            public int Throttled { get; set; }

            public int Attempted => Captured + Failed + Throttled;
        }
    }
}
