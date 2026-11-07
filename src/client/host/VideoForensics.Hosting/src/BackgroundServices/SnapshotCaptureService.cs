using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting.Maintenance;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Client.Ui.Shared.Services;
using VideoForensics.Core.Helpers.Platform;
using VideoForensics.Core.Providers.Common.Contracts;
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
    /// Rows hold only the attempt record. A 200 also writes the JPEG under the media root's &lt;provider&gt;\&lt;location&gt;\&lt;device&gt;\ folder as &lt;device&gt;_yyyyMMdd_HHmmss_snapshot.jpg (MediaFileNamer).
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
        private readonly ILogger<SnapshotCaptureService> _logger;
        private readonly TimeProvider _time;
        private readonly IStorageLocationProvider _storage;
        private readonly IForensicsConfiguration _config;

        /// <summary>
        /// How often an offline camera is probed, from the Services page setting. Offline cameras are repolled, not captured every
        /// interval, so a camera that stays offline costs one or two Ring calls per repoll rather than per capture.
        /// </summary>
        internal TimeSpan OfflineRepollWindow => TimeSpan.FromMinutes(_config.OfflineRepollIntervalMinutes);

        // Process-lifetime guards. The Ring pacers live on a per-tick scoped instance, so a provider pause must be remembered here
        // to hold across ticks. Keyed by internal GUIDs; never logged.
        private readonly ConcurrentDictionary<Guid, DateTimeOffset> _accountPausedUntil = new();
        private readonly ConcurrentDictionary<Guid, DateTimeOffset> _deviceRetryAfter = new();
        private readonly ThreadHeartbeats? _heartbeats;

        /// <summary>Creates the capture service.</summary>
        public SnapshotCaptureService(
            IServiceScopeFactory scopeFactory,
            ILogger<SnapshotCaptureService> logger,
            TimeProvider time,
            IStorageLocationProvider storage,
            IForensicsConfiguration config,
            ThreadHeartbeats? heartbeats = null)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _time = time;
            _storage = storage;
            _config = config;
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
                        _heartbeats?.RecordError(nameof(SnapshotCaptureService), _time.GetUtcNow(), "run failed");
                        _logger.LogError("Snapshot capture tick failed; will retry on the next tick: {Message} ({ErrorType})", ex.Message, ex.GetType().Name);
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
            using IDisposable? lease = sp.TryEnter();
            if (lease is null)
            {
                // Another sync or maintenance holds the lease, so this tick captures nothing. Logged so a silent gap is visible.
                _logger.LogInformation("Snapshot capture tick skipped: another operation holds the lease");
                return;
            }

            bool itemFailed = false;
            IReadOnlyList<ProviderAccount> accounts = await sp.GetRequiredService<IProviderAccountRepository>().ListActiveAsync(ct);
            _logger.LogDebug("Snapshot capture tick started: {AccountCount} active accounts", accounts.Count);
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
                    itemFailed = true;
                    _logger.LogError("Snapshot capture failed for {ProviderName} account: {Message} ({ErrorType})", account.ProviderName, ex.Message, ex.GetType().Name);
                }
            }

            if (counts.Attempted > 0)
            {
                _logger.LogInformation("Snapshot capture tick: {Captured} captured, {Failed} failed, {Throttled} throttled",
                    counts.Captured, counts.Failed, counts.Throttled);
            }
            if (counts.Failed > 0 || counts.AlertFailed)
            {
                itemFailed = true;
            }
            if (itemFailed) { _heartbeats?.RecordError(nameof(SnapshotCaptureService), _time.GetUtcNow(), "run failed"); } else { _heartbeats?.RecordRun(nameof(SnapshotCaptureService), _time.GetUtcNow()); }
        }

        private async Task CaptureAccountAsync(IServiceProvider sp, ProviderAccount account, TickCounts counts, CancellationToken ct)
        {
            Guid accountId = account.Id;
            string accountName = $"{account.ProviderName} account";
            DateTimeOffset now = _time.GetUtcNow();

            if (IsPaused(accountId, now))
            {
                _logger.LogDebug("Snapshot capture skipped for {AccountName}: paused until {PausedUntil:t} local time", accountName, _accountPausedUntil[accountId].ToLocalTime());
                return;
            }

            SyncSchedule? schedule = await sp.GetRequiredService<ISyncScheduleRepository>().GetByProviderAccountIdAsync(accountId, ct);
            // The schedule's snapshot interval (with any jamming window) sets the spacing. A stored SnapshotIntervalSeconds raised by
            // a Ring rate-limit incident is a floor on top of it, so the adaptive back-off is never undone by the form.
            int seconds = schedule is null
                ? DefaultIntervalSeconds
                : SnapshotIntervals.CaptureSpacing(schedule, _time.GetUtcNow().UtcDateTime);
            var interval = TimeSpan.FromSeconds(seconds);

            IDeviceRepository devices = sp.GetRequiredService<IDeviceRepository>();
            ISnapshotCaptureRepository captures = sp.GetRequiredService<ISnapshotCaptureRepository>();
            IProviderDeviceStatus deviceStatus = sp.GetRequiredKeyedService<IProviderDeviceStatus>(RingProviderName);
            var candidates = new List<(Device Device, TimeSpan Window)>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (Location location in await sp.GetRequiredService<ILocationRepository>().ListByProviderAccountIdAsync(accountId, ct))
            {
                foreach (Device device in await devices.GetByLocationIdAsync(location.Id, ct))
                {
                    // Disabled or camera-less devices are never due. An offline camera is repolled once per OfflineRepollWindow, not
                    // captured every interval. The window check also covers startup: a camera with no probe in the last window is due at once.
                    if (!seen.Add(device.ProviderDeviceId))
                    {
                        continue;
                    }

                    // Each skip logs its own reason, so a silent camera shows whether its capture is off or its type has no camera.
                    if (!device.SnapshotCaptureEnabled)
                    {
                        _logger.LogDebug("Snapshot capture skipped {DeviceName} on {AccountName}: capture is off for this device", device.Name, accountName);
                        continue;
                    }

                    if (!HasCamera(device.Type))
                    {
                        _logger.LogDebug("Snapshot capture skipped {DeviceName} on {AccountName}: device type {DeviceType} has no camera", device.Name, accountName, device.Type ?? "unknown");
                        continue;
                    }

                    if (_deviceRetryAfter.TryGetValue(device.Id, out DateTimeOffset retryAfter) && retryAfter > now)
                    {
                        _logger.LogDebug("Snapshot capture held for {DeviceName} on {AccountName} until {RetryAt:t} after a failure", device.Name, accountName, retryAfter.ToLocalTime());
                        continue;
                    }

                    TimeSpan window = deviceStatus.IsOffline(device.Type, device.IsOnline) ? OfflineRepollWindow : interval;
                    candidates.Add((device, window));
                }
            }

            // Which candidates already have an attempt inside their window (current or failed). One query per distinct window,
            // not one per device, so a tick costs at most two lookups per account.
            var due = new List<Device>();
            foreach (TimeSpan window in candidates.Select(c => c.Window).Distinct())
            {
                IReadOnlyCollection<Guid> ids = candidates.Where(c => c.Window == window).Select(c => c.Device.Id).ToList();
                IReadOnlySet<Guid> attempted = await captures.GetDeviceIdsWithAttemptsAsync(ids, now - window, now, ct);
                due.AddRange(candidates.Where(c => c.Window == window && !attempted.Contains(c.Device.Id)).Select(c => c.Device));
            }

            if (due.Count == 0)
            {
                _logger.LogDebug("Snapshot capture: no devices due for {AccountName} ({CandidateCount} candidates already attempted this window)", accountName, candidates.Count);
                return;
            }

            _logger.LogDebug("Snapshot capture: {DueCount} devices due for {AccountName} at {IntervalSeconds}s spacing", due.Count, accountName, seconds);

            if (!await EnsureSessionAsync(sp, accountId, accountName, ct))
            {
                _logger.LogWarning("Snapshot capture backed off for {AccountName} for {BackoffSeconds}s: no active session", accountName, AccountErrorBackoff.TotalSeconds);
                _accountPausedUntil[accountId] = _time.GetUtcNow() + AccountErrorBackoff;
                return;
            }

            // Why: a Ring rate-limit ban refuses every snapshot until it lifts. Holding the account for the ban's length, instead of
            // discovering it one device at a time, stops these calls from extending the ban.
            IMediaDownloadService? media = sp.GetKeyedService<IMediaDownloadService>(RingProviderName);
            if (media?.GetRateLimitBanUntilUtc() is { } banUntilUtc && banUntilUtc > _time.GetUtcNow().UtcDateTime)
            {
                _logger.LogWarning("Snapshot capture paused for {AccountName}: rate-limit ban active until {BanUntilUtc:u}", accountName, banUntilUtc);
                _accountPausedUntil[accountId] = new DateTimeOffset(DateTime.SpecifyKind(banUntilUtc, DateTimeKind.Utc));
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

            // Read before the call, because the call writes this attempt's health row. Offline is a health transition, so the
            // alert needs the reading that was current before this attempt.
            DeviceHealth? previousHealth = sp.GetService<IDeviceHealthRepository>() is { } healthRepository
                ? await healthRepository.GetLatestAsync(device.Id, ct)
                : null;

            try
            {
                attempt = await provider.CaptureAsync(account.Id, device.ProviderDeviceId, device.Id, SettleDelay, ct);
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
                _logger.LogError("Snapshot capture failed for {DeviceName} on {AccountName}: {Message} ({ErrorType})", device.Name, accountName, ex.Message, ex.GetType().Name);
                Hold(device.Id, interval);
                counts.Failed++;
                return;
            }

            if (budgetGuard != null)
            {
                await budgetGuard.RecordCallAsync(RingProviderName, ct);
                await budgetGuard.RecordCallAsync(RingProviderName, ct);
            }

            // One line per attempt, so a failing device shows its HTTP status and how long the provider took.
            _logger.LogInformation("Snapshot capture attempt for {DeviceName} on {AccountName}: HTTP {HttpStatus} after {ElapsedMs} ms, image {ImageBytes} bytes",
                device.Name, accountName, attempt.HttpStatus?.ToString(CultureInfo.InvariantCulture) ?? "none",
                (long)(_time.GetUtcNow() - attemptedAt).TotalMilliseconds, attempt.Image?.Length ?? 0);

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
                    Location? location = await sp.GetRequiredService<ILocationRepository>().GetAsync(device.LocationId, ct);
                    string mediaRoot = await ResolveMediaRootAsync(sp, ct);
                    mediaFilePath = WriteImage(mediaRoot, account.ProviderName, location?.Name ?? string.Empty, device.Name, device.ProviderDeviceId, attemptedAt, image);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError("Could not write the snapshot for {DeviceName} on {AccountName}: {Message} ({ErrorType})", device.Name, accountName, ex.Message, ex.GetType().Name);
                    Hold(device.Id, interval);
                    counts.Failed++;
                    return;
                }
            }

            try
            {
                string? storedPath = mediaFilePath is null
                    ? null
                    : await (sp.GetService<IMediaPathResolver>() ?? PassThroughMediaPathResolver.Instance).ToStoredAsync(mediaFilePath, ct);
                var row = SnapshotCapture.Create(device.Id, attemptedAt, "fresh", status, storedPath, deviceHealthId: attempt.DeviceHealthId);
                _ = await sp.GetRequiredService<ISnapshotCaptureRepository>().AddAsync(row, ct);
                _logger.LogDebug("Snapshot attempt recorded for {DeviceName} on {AccountName}: HTTP {HttpStatus}", device.Name, accountName, status);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError("Could not record the snapshot attempt for {DeviceName} on {AccountName}: {Message} ({ErrorType})", device.Name, accountName, ex.Message, ex.GetType().Name);
                counts.Failed++;
                return;
            }

            if (mediaFilePath is not null)
            {
                // WriteImage returns the full path under the media root; the capture row and the Media row both store it relative to that root.
                await RecordCaptureMediaAsync(sp, device, mediaFilePath, attemptedAt, accountName, ct);
            }

            switch (status)
            {
                case 200:
                    counts.Captured++;
                    break;
                case 404:
                    // Ring has no frame. Offline is decided by the health reading, not by a missing frame, so this leaves the
                    // device's state alone. A 404 while Ring reports the camera connected is a bug.
                    if (attempt.Connected == true)
                    {
                        _logger.LogError("Snapshot capture for {DeviceName} on {AccountName} got HTTP 404 while Ring reports the device connected; this is a bug", device.Name, accountName);
                    }

                    counts.Failed++;
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

            // The provider's connected reading is the device's state. It overrides a 404's offline mark, so a camera Ring reports
            // as connected shows online even when its latest frame is not ready yet.
            if (attempt.Connected is bool connected && device.IsOnline != connected)
            {
                _logger.LogInformation("Snapshot capture set {DeviceName} on {AccountName} to {OnlineState} from its health reading",
                    device.Name, accountName, connected ? "online" : "offline");
                device.IsOnline = connected;
                await sp.GetRequiredService<IDeviceRepository>().UpdateAsync(device, ct);
            }

            if (attempt.Connected == false && previousHealth is { IsOnline: true })
            {
                if (!await RaiseOfflineAlertAsync(sp, device, account.Id, ct))
                {
                    counts.AlertFailed = true;
                }
            }
        }

        /// <summary>
        /// Writes the JPEG under the media root's &lt;provider&gt;\&lt;location&gt;\&lt;device&gt;\ folder, next to the event videos, and returns the full path.
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
                    FilePath = await (sp.GetService<IMediaPathResolver>() ?? PassThroughMediaPathResolver.Instance).ToStoredAsync(mediaFilePath, ct),
                    MediaFormat = "jpg",
                    MediaKind = VideoForensics.Data.Common.Entities.MediaKinds.Snapshot,
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
                _logger.LogError("Could not record the captured snapshot in Media for {DeviceName} on {AccountName}: {Message} ({ErrorType})", device.Name, accountName, ex.Message, ex.GetType().Name);
            }
        }

        /// <summary>
        /// Resolves the Media root for one write, so a change on the Storage Settings page applies without a restart.
        /// Falls back to the default root when no configuration service is registered.
        /// </summary>
        private async Task<string> ResolveMediaRootAsync(IServiceProvider sp, CancellationToken ct)
        {
            IForensicsConfigurationService? configuration = sp.GetService<IForensicsConfigurationService>();
            if (configuration is null)
            {
                return _storage.GetDefaultRoot(StorageCategory.Media);
            }

            IForensicsConfiguration config = await configuration.LoadConfigurationAsync(string.Empty, ct);
            return _storage.GetEffectiveRoot(StorageCategory.Media, config.DownloadLocation);
        }

        private string WriteImage(string mediaRoot, string providerName, string locationName, string deviceName, string providerDeviceId, DateTimeOffset capturedAtUtc, byte[] image)
        {
            if (!int.TryParse(providerDeviceId, NumberStyles.None, CultureInfo.InvariantCulture, out _))
            {
                throw new ArgumentException("Ring device ids are numeric.", nameof(providerDeviceId));
            }

            string folder = MediaFolderLayout.GetDeviceFolder(mediaRoot, providerName, locationName, deviceName);
            string fileName = MediaFileNamer.FormatMediaFileName(deviceName, capturedAtUtc.UtcDateTime, "snapshot", "jpg");
            string fullPath = Path.Combine(folder, fileName);

            _ = Directory.CreateDirectory(folder);
            File.WriteAllBytes(fullPath, image);
            return fullPath;
        }

        /// <summary>
        /// True for the Ring device types that carry a camera. Allowlisted rather than denylisted: a chime, a range extender or any
        /// unrecognised type has no camera to snapshot, so it is skipped by default.
        /// </summary>
        internal static bool HasCamera(string? type)
        {
            return DeviceKinds.IsCamera(type);
        }

        /// <summary>
        /// Raises the DeviceOffline alert when a health reading turns a camera offline. The link opens the account's sync page, where the
        /// device's polling mode is set. A dispatch failure is logged and never fails the capture.
        /// </summary>
        private async Task<bool> RaiseOfflineAlertAsync(IServiceProvider sp, Device device, Guid accountId, CancellationToken ct)
        {
            INotificationDispatcher? dispatcher = sp.GetService<INotificationDispatcher>();
            if (dispatcher is null)
            {
                return true;
            }

            try
            {
                await dispatcher.DispatchAsync(new NotificationEvent(
                    EventType: "DeviceOffline",
                    TimestampUtc: _time.GetUtcNow().UtcDateTime,
                    OperatorId: null,
                    PairedDeviceId: device.Id,
                    SourceIp: null,
                    Details: $"{device.Name} went offline",
                    Audience: NotificationAudience.AdminsOnly,
                    Severity: NoticeSeverity.Alert,
                    ActionPath: $"/accounts/{accountId}/sync"), ct);
                return true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning("Could not dispatch the offline alert for {DeviceName} (non-critical): {Message} ({ErrorType})", device.Name, ex.Message, ex.GetType().Name);
                return false;
            }
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

            /// <summary>True when an offline alert failed to dispatch this tick. The capture itself still counts as recorded.</summary>
            public bool AlertFailed { get; set; }

            public int Attempted => Captured + Failed + Throttled;
        }
    }
}
