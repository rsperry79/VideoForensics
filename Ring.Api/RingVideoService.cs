using Microsoft.Extensions.Logging;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using KoenZomers.Ring.Api.Entities;
using KoenZomers.Ring.Api.Models;

namespace KoenZomers.Ring.Api
{
    /// <summary>
    /// Orchestrates a full Ring video/snapshot download run: authentication, device and location
    /// discovery, event filtering, the concurrent download/retry loop, settings and credential
    /// persistence, and report/log file generation (failure TSV, camera-health TSV, per-event JSON,
    /// raw API traffic). Everything user-facing is surfaced through <see cref="IDownloadReporter"/>
    /// so this class has no dependency on any particular console/UI framework.
    /// </summary>
    public class RingVideoService
    {
        private readonly ILogger log;
        private readonly IDownloadReporter reporter;
        private readonly ICredentialStore credentialStore;
        private readonly IReadOnlyDictionary<string, string> configLocationNames;
        private readonly Filter defaultFilter;
        private Session ringSession;
        private static SemaphoreSlim semaphore = new SemaphoreSlim(10, 10);
        private static int activeDls = 0;
        private static long totalBytesDownloaded = 0;
        private static DateTime downloadStartTime = DateTime.Now;
        public Filter Filter { get; set; } = new();
        public RingCredentials Auth { get; set; } = new();
        public readonly string SavedSettingsFolder;
        public readonly string SavedSettingsFile;
        public readonly string AuthFile;
        private readonly DownloadHelper downloadHelper;
        private ConcurrentBag<FailedDownload> newFailures = new();
        private HashSet<string> loadedEventIds = new();
        private string reportsDirectory;
        private string logsDirectory;
        public static volatile bool IsRunActive = false;
        private static readonly object rawApiLogLock = new object();
        private Dictionary<Guid, string> locationNameCache = new();
        private Dictionary<long, Guid> deviceIdToLocationId = new();

        public RingVideoService(
            ILogger<RingVideoService> logger,
            IDownloadReporter reporter,
            ICredentialStore credentialStore,
            string dataDirectory,
            IReadOnlyDictionary<string, string> configLocationNames = null,
            Filter defaultFilter = null)
        {
            this.log = logger;
            this.reporter = reporter;
            this.credentialStore = credentialStore;
            this.configLocationNames = configLocationNames;
            this.defaultFilter = defaultFilter;
            this.downloadHelper = new DownloadHelper();

            this.SavedSettingsFolder = dataDirectory;
            this.SavedSettingsFile = Path.Combine(dataDirectory, "RingVideosConfig.json");
            this.AuthFile = Path.Combine(dataDirectory, "auth.json");

            try
            {
                ReadSettings();
            }
            catch (Exception exe)
            {
                log.LogError(exe, "Failed to load saved settings");
                reporter.Warning("Failed to load saved settings");
            }
        }

        private void ReadSettings()
        {
            string contents = null;
            try
            {
                contents = System.IO.File.ReadAllText(SavedSettingsFile);
                var settings = JsonSerializer.Deserialize<Config>(contents);
                this.Filter = settings.Filter ?? new Filter();
            }
            catch (Exception)
            {
                this.Filter = defaultFilter ?? new Filter();
            }

            this.Auth = credentialStore.Load(AuthFile);
            if (string.IsNullOrWhiteSpace(this.Auth.RefreshToken) && string.IsNullOrWhiteSpace(this.Auth.Password))
            {
                MigrateLegacyAuth(contents);
            }

            if (credentialStore.SanitizeClearTextPassword(SavedSettingsFile, AuthFile))
            {
                log.LogWarning("Found and encrypted a clear-text password in {file}", SavedSettingsFile);
            }
        }

        /// <summary>
        /// One-time migration for credentials previously stored inline in RingVideosConfig.json's
        /// "Authentication" property, back when the console app owned encryption itself. Only runs
        /// when no auth.json exists yet; the migrated credentials are written out via CredentialStore
        /// so the old inline copy is dropped the next time settings are saved.
        /// </summary>
        private void MigrateLegacyAuth(string savedSettingsContents)
        {
            if (string.IsNullOrEmpty(savedSettingsContents))
                return;

            try
            {
                using var doc = JsonDocument.Parse(savedSettingsContents);
                if (!doc.RootElement.TryGetProperty("Authentication", out var authElement))
                    return;

                var legacyAuth = credentialStore.LoadFromJson(authElement.GetRawText());
                if (!string.IsNullOrWhiteSpace(legacyAuth.RefreshToken) || !string.IsNullOrWhiteSpace(legacyAuth.Password))
                {
                    this.Auth = legacyAuth;
                    credentialStore.Save(AuthFile, this.Auth);
                    log.LogInformation("Migrated saved credentials from {oldFile} to {authFile}", SavedSettingsFile, AuthFile);
                }
            }
            catch (Exception exe)
            {
                log.LogWarning(exe, "Failed to migrate legacy credentials from {oldFile}", SavedSettingsFile);
            }
        }

        private void SaveSettings(DateTime? lastSuccessUtc, DateTime? lastFailureUtc)
        {

            credentialStore.Save(AuthFile, Auth);
            //Set "next dates" on filter
            if (lastFailureUtc.HasValue && !Filter.Snapshots)
            {
                Filter.StartDateTime = lastFailureUtc.Value.AddMinutes(-1).ToLocalTime();
                Filter.EndDateTime = null;
            }
            else if (lastSuccessUtc.HasValue && !Filter.Snapshots)
            {
                Filter.StartDateTime = lastSuccessUtc.Value.ToLocalTime();
                Filter.EndDateTime = null;
            }

            if (!Directory.Exists(this.SavedSettingsFolder))
            {
                Directory.CreateDirectory(this.SavedSettingsFolder);
            }

            var conf = new Config()
            {
                Filter = this.Filter
            };
            var config = JsonUtil.Serialize(conf, JsonMode.Pretty);

            System.IO.File.WriteAllText(this.SavedSettingsFile, config);
            log.LogInformation("Settings saved to {settingsFile}", this.SavedSettingsFile);
            log.LogInformation($"Saved refresh token (length: {Auth.RefreshToken?.Length ?? 0})");
        }

        /// <summary>
        /// Appends every raw Ring API call made this run to a JSONL log file under the reports
        /// directory, so the full request/response traffic can be inspected for missing fields,
        /// undocumented endpoints, or data our entity classes don't currently map.
        /// </summary>
        private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

        private void LogRawApiCall(RawApiCall call)
        {
            if (string.IsNullOrEmpty(logsDirectory))
                return;

            try
            {
                var entry = new
                {
                    timestamp = call.Timestamp.ToString("o"),
                    method = call.Method,
                    url = call.Url,
                    statusCode = call.StatusCode,
                    bodyLength = call.Body?.Length ?? 0,
                    body = call.Body
                };
                var line = JsonUtil.Serialize(entry, JsonMode.Raw);

                var logPath = Path.Combine(logsDirectory, "api_raw_responses.jsonl");
                lock (rawApiLogLock)
                {
                    System.IO.File.AppendAllText(logPath, line + Environment.NewLine, Utf8NoBom);
                    log.LogInformation("RawApiResponse {method} {url} {statusCode}", entry.method, entry.url, entry.statusCode);
                }
            }
            catch (Exception exe)
            {
                log.LogWarning(exe, "Failed to write raw API response log entry");
            }
        }

        private static int ringEventsFileCounter = 0;

        /// <summary>
        /// Appends session/auth lifecycle events (authenticate, refresh, throttle, retry) to a
        /// human-readable debug log, separate from the full HTTP firehose.
        /// </summary>
        private void LogApiLifecycleEvent(ApiLifecycleEvent evt)
        {
            if (string.IsNullOrEmpty(logsDirectory))
                return;

            try
            {
                var line = $"{evt.Timestamp:o} [{evt.Category}] {evt.Message}";
                var logPath = Path.Combine(logsDirectory, "session_debug.log");
                lock (rawApiLogLock)
                {
                    System.IO.File.AppendAllText(logPath, line + Environment.NewLine, Utf8NoBom);
                }
            }
            catch (Exception exe)
            {
                log.LogWarning(exe, "Failed to write session debug log entry");
            }
        }

        /// <summary>
        /// Writes the parsed Ring event ("ding") list from a history call to its own pretty-printed
        /// JSON file, so it can be diffed against the raw HTTP body to spot anything our entity
        /// classes silently drop or leave null.
        /// </summary>
        private void LogRingEventsBatch(RingEventsBatch batch)
        {
            if (string.IsNullOrEmpty(logsDirectory))
                return;

            try
            {
                var localTime = batch.Timestamp.ToLocalTime();
                var seq = Interlocked.Increment(ref ringEventsFileCounter);
                var fileName = $"events-{localTime:yyyy-MM-dd}-T{localTime:HH_mm_ss}-{seq}.json";
                var filePath = Path.Combine(logsDirectory, fileName);

                using var doc = JsonDocument.Parse(batch.EventsJson);
                var pretty = JsonUtil.Serialize(doc, JsonMode.Pretty);

                System.IO.File.WriteAllText(filePath, pretty, Utf8NoBom);
                log.LogInformation("RingEventsBatch {fileName} written", Path.GetFileName(filePath));
            }
            catch (Exception exe)
            {
                log.LogWarning(exe, "Failed to write ring events batch log file");
            }
        }

        public string GetFilterMessage()
        {
            var expandedPath = Environment.ExpandEnvironmentVariables(Filter.DownloadPath ?? string.Empty);
            StringBuilder message = new StringBuilder();
            message.AppendLine("----------------------------");
            if (Filter.StartDateTime.HasValue)
            {
                message.AppendLine($"Start Date:\t{Filter.StartDateTime.Value} [UTC: {Filter.StartDateTimeUtc.Value}]");
            }
            if (Filter.EndDateTime.HasValue)
            {
                message.AppendLine($"End Date:\t{Filter.EndDateTime.Value} [UTC: {Filter.EndDateTimeUtc.Value}]");
            }
            else
            {
                message.AppendLine($"End Date:\tCurrent Time");
            }
            if (Filter.VideoCount != 10000)
            {
                message.AppendLine($"Max downloads:\t{Filter.VideoCount}");
            }
            message.AppendLine($"Only Starred:\t{Filter.OnlyStarred}");
            message.AppendLine($"Only Person:\t{Filter.OnlyPersonDetected}");
            if (!string.IsNullOrWhiteSpace(Filter.Kind))
            {
                message.AppendLine($"Event Kind:\t{Filter.Kind}");
            }
            if (!string.IsNullOrWhiteSpace(Filter.DetectionType))
            {
                message.AppendLine($"Detection:\t{Filter.DetectionType}");
            }
            message.AppendLine($"Snapshots:\t{Filter.Snapshots}");
            if (!string.IsNullOrWhiteSpace(expandedPath))
            {
                message.AppendLine($"Download Path:\t{expandedPath}");
            }
            message.AppendLine("----------------------------");
            return message.ToString();
        }

        public void PrintFilterMessage(string firstLine)
        {
            try
            {
                reporter.Info("----------------------------");
                reporter.Highlight(firstLine);
                reporter.Info(GetFilterMessage());
            }
            catch (Exception)
            {

            }
        }

        public async Task<Session> Authenticate()
        {
            Session session = null;
            try
            {
                session = await reporter.RunWithStatusAsync("Authenticating...", async updateStatus =>
                {
                    var progress = new Progress<AuthProgressEventArgs>(e =>
                    {
                        updateStatus(e.Message);
                        if (e.IsWarning)
                        {
                            reporter.Warning(e.Message);
                            log.LogWarning(e.Message);
                        }
                        else
                        {
                            reporter.Info(e.Message);
                        }
                    });

                    var s = await Session.AuthenticateWithCredentials(
                        this.Auth,
                        twoFactorAuthCodeProvider: async () =>
                        {
                            // Two factor authentication is enabled on the account - a text message with a
                            // code will have just been sent. Ask for it here.
                            reporter.Info("Two factor authentication enabled on this account, please enter the token received in the text message on your phone:");
                            var token = await reporter.PromptTwoFactorCodeAsync();
                            if (!string.IsNullOrEmpty(token))
                            {
                                log.LogInformation("2FA token received");
                            }
                            return token;
                        },
                        progress: progress);

                    await Task.Delay(500);
                    return s;
                });
            }
            catch (KoenZomers.Ring.Api.Exceptions.ThrottledException e)
            {
                reporter.Error(e.Message);
            }
            catch (KoenZomers.Ring.Api.Exceptions.AuthenticationFailedException e)
            {
                reporter.Error($"{e.Message}: Please validate your credentials");
            }
            catch (System.Net.WebException e)
            {
                reporter.Error($"{e.Message}: Connection failed, please validate your credentials.");
            }
            catch (Exception exe)
            {
                reporter.Error($"{exe.Message}");
            }

            if (session != null && session.OAuthToken != null)
            {
                SaveSettings(null, null);
            }
            return session;
        }

        public async Task<int> Run(CancellationToken ct)
        {
            try
            {
                log.LogInformation("Starting download run");
            }
            catch (IOException)
            {
                // Output redirected (e.g. running under CI or piped to a file) - nothing to clear
            }

            IsRunActive = true;
            try
            {
                DateTime? lastSuccess = null;
                DateTime? firstFailure = null;
                int failedCount = 0;
                List<(bool success, DoorbotHistoryEvent ding)> results = new();
                this.ringSession = await Authenticate();
                if (this.ringSession == null || !this.ringSession.IsAuthenticated)
                {
                    reporter.Error("Authentication failed. Please check your credentials.");
                    return 999;
                }
                if (Filter.DownloadPath == null)
                {
                    reporter.Error("A valid download path '--path' argument is required");
                    return -1;
                }

                var expandedPath = Environment.ExpandEnvironmentVariables(Filter.DownloadPath);
                if (!string.IsNullOrWhiteSpace(expandedPath))
                {
                    if (!Directory.Exists(expandedPath))
                    {
                        Directory.CreateDirectory(expandedPath);
                    }
                }
                else
                {
                    reporter.Error("A valid download path '--path' argument is required");
                    return -1;
                }

                // Initialize reports directory
                reportsDirectory = Path.Combine(expandedPath, "reports");
                if (!Directory.Exists(reportsDirectory))
                {
                    Directory.CreateDirectory(reportsDirectory);
                }

                // Initialize logs directory
                logsDirectory = Path.Combine(expandedPath, "logs");
                if (!Directory.Exists(logsDirectory))
                {
                    Directory.CreateDirectory(logsDirectory);
                }

                // Capture every raw API response for this run so we can inspect what Ring actually
                // sends back (e.g. to spot fields/devices/locations our entities don't map).
                // Unsubscribe first in case Run() is invoked more than once in this process (interactive mode).
                KoenZomers.Ring.Api.ApiRawLogger.OnRawResponse -= LogRawApiCall;
                KoenZomers.Ring.Api.ApiRawLogger.OnRawResponse += LogRawApiCall;
                KoenZomers.Ring.Api.ApiRawLogger.OnEvent -= LogApiLifecycleEvent;
                KoenZomers.Ring.Api.ApiRawLogger.OnEvent += LogApiLifecycleEvent;
                KoenZomers.Ring.Api.ApiRawLogger.OnRingEvents -= LogRingEventsBatch;
                KoenZomers.Ring.Api.ApiRawLogger.OnRingEvents += LogRingEventsBatch;

                // Load existing failures to check for duplicates
                LoadExistingFailures(reportsDirectory);

                this.PrintFilterMessage("Fetching videos with the following settings:");
                if (!Filter.Snapshots)
                {
                    DeviceList deviceList = new();
                    if (Filter.DeviceId.HasValue && Filter.DeviceId.Value > 0)
                    {
                        deviceList.Devices.Add(new DeviceInfo() { Id = Filter.DeviceId.Value });
                    }
                    else
                    {
                        deviceList = await GetDevicesList();
                    }

                    // Fetch all devices (across every location this account has been shared into) and use
                    // each device's LocationId to organize downloads
                    var allDevices = new Devices
                    {
                        Doorbots = new List<Doorbot>(),
                        AuthorizedDoorbots = new List<Doorbot>(),
                        Chimes = new List<Chime>(),
                        StickupCams = new List<StickupCam>()
                    };

                    try
                    {
                        var allDeviceResponse = await this.ringSession.GetRingDevices();
                        if (allDeviceResponse != null)
                        {
                            if (allDeviceResponse.Doorbots != null)
                                allDevices.Doorbots.AddRange(allDeviceResponse.Doorbots);
                            if (allDeviceResponse.AuthorizedDoorbots != null)
                                allDevices.AuthorizedDoorbots.AddRange(allDeviceResponse.AuthorizedDoorbots);
                            if (allDeviceResponse.Chimes != null)
                                allDevices.Chimes.AddRange(allDeviceResponse.Chimes);
                            if (allDeviceResponse.StickupCams != null)
                                allDevices.StickupCams.AddRange(allDeviceResponse.StickupCams);
                        }
                    }
                    catch (Exception ex)
                    {
                        reporter.Error($"Failed to fetch devices: {ex.Message}");
                        log.LogError(ex, "Failed to fetch devices");
                    }

                    reporter.Highlight($"Found {(allDevices.Doorbots?.Count ?? 0) + (allDevices.Chimes?.Count ?? 0) + (allDevices.StickupCams?.Count ?? 0)} total devices across all locations");

                    // Camera/doorbell health (connectivity, battery, wifi signal) comes embedded directly in
                    // the ring_devices response already fetched above - no separate health API call needed.
                    void PrintDeviceHealth(string name, DeviceHealth health)
                    {
                        if (health == null)
                        {
                            reporter.Info($"    {name}: (no health data returned)");
                            return;
                        }

                        var parts = new List<string>();
                        if (health.Connected.HasValue)
                            parts.Add(health.Connected.Value ? "connected" : "DISCONNECTED");
                        if (!string.IsNullOrEmpty(health.RssiCategory))
                            parts.Add($"wifi {health.RssiCategory}" + (health.Rssi.HasValue ? $" ({health.Rssi}dBm)" : ""));
                        if (health.BatteryPercentage.HasValue && health.BatteryPercentage.Value > 0)
                            parts.Add($"battery {health.BatteryPercentage}%");
                        else if (!string.IsNullOrEmpty(health.BatteryVoltageCategory))
                            parts.Add($"battery voltage {health.BatteryVoltageCategory}");
                        if (!string.IsNullOrEmpty(health.FirmwareVersionStatus))
                            parts.Add(health.FirmwareVersionStatus);

                        var line = $"    {name}: {(parts.Count > 0 ? string.Join(", ", parts) : "no telemetry")}";
                        var isConcern = health.Connected == false ||
                           string.Equals(health.RssiCategory, "poor", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(health.BatteryVoltageCategory, "poor", StringComparison.OrdinalIgnoreCase);
                        if (isConcern)
                            reporter.Warning(line);
                        else
                            reporter.Info(line);
                    }

                    if (allDevices.StickupCams.Count > 0 || allDevices.Doorbots.Count > 0 || allDevices.AuthorizedDoorbots.Count > 0)
                    {
                        reporter.Info("Camera health:");
                        foreach (var d in allDevices.StickupCams)
                            PrintDeviceHealth(d.Description ?? $"Device {d.Id}", d.Health);
                        foreach (var d in allDevices.Doorbots.Concat(allDevices.AuthorizedDoorbots))
                            PrintDeviceHealth(d.Description ?? $"Device {d.Id}", d.Health);
                    }

                    // Build a device-id -> location-id lookup. The doorbot history API (used per-download)
                    // does not embed location_id on its nested doorbot object, only the device-list APIs do -
                    // so downloads must resolve location via this lookup keyed on device id, not ding.Doorbot.LocationId.
                    deviceIdToLocationId.Clear();
                    foreach (var d in allDevices.Doorbots.Concat(allDevices.AuthorizedDoorbots).Where(d => d.LocationId.HasValue))
                        deviceIdToLocationId[d.Id] = d.LocationId.Value;
                    foreach (var d in allDevices.Chimes.Where(d => d.LocationId.HasValue))
                        deviceIdToLocationId[d.Id] = d.LocationId.Value;
                    foreach (var d in allDevices.StickupCams.Where(d => d.LocationId.HasValue))
                        deviceIdToLocationId[d.Id.Value] = d.LocationId.Value;

                    // Populate location name cache from the API
                    reporter.Info("Loading location names...");
                    try
                    {
                        var locations = await this.ringSession.GetLocations();
                        if (locations != null && locations.Count > 0)
                        {
                            foreach (var loc in locations)
                            {
                                if (loc.Id.HasValue && !string.IsNullOrEmpty(loc.Name))
                                {
                                    locationNameCache[loc.Id.Value] = loc.Name;
                                    var deviceCount = deviceIdToLocationId.Count(kvp => kvp.Value == loc.Id.Value);
                                    reporter.Info($"  {loc.Name} ({loc.Id}) - {deviceCount} device(s){(loc.IsOwner == false ? " [shared]" : string.Empty)}");

                                    // Ring only returns devices this account has been explicitly granted access to on
                                    // shared locations - a low count here can mean cameras exist but aren't visible to
                                    // this account, not that the app failed to find them.
                                    if (loc.IsOwner == false && deviceCount <= 1)
                                    {
                                        reporter.Warning($"    '{loc.Name}' is a shared location with only {deviceCount} visible device(s). " +
                                           "If you expect more cameras here, ask the Ring account owner to grant this account access " +
                                           "to them under Shared Users in the Ring app - newly added devices aren't shared automatically.");
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        reporter.Warning($"Failed to fetch locations from API: {ex.Message}");
                        log.LogWarning(ex, "Failed to fetch location list from API");
                    }

                    // Fall back to config-supplied names for any location the API didn't cover
                    if (configLocationNames != null)
                    {
                        foreach (var kvp in configLocationNames)
                        {
                            if (Guid.TryParse(kvp.Key, out var locId) && !locationNameCache.ContainsKey(locId))
                            {
                                locationNameCache[locId] = kvp.Value;
                                reporter.Info($"  {kvp.Value} ({locId}) [from config]");
                            }
                        }
                    }

                    GenerateCameraHealthReport(reportsDirectory, allDevices, locationNameCache);

                    // Build device list from all locations
                    var allDeviceList = new DeviceList().ExtractDevices(allDevices);

                    List<DoorbotHistoryEvent> dings = new();
                    try
                    {
                        foreach (var dev in allDeviceList.Devices)
                        {
                            dings.AddRange(await reporter.RunWithStatusAsync($"Querying for videos to download from {dev.Name}...", async updateStatus =>
                            {
                                var events = await ringSession.GetDoorbotsHistory(Filter.StartDateTimeUtc.Value, Filter.EndDateTimeUtc, dev.Id);
                                await Task.Delay(500);
                                return events;
                            }));
                        }
                    }
                    catch (Exception exe)
                    {
                        reporter.Error(exe.Message);
                        log.LogError(exe.ToString());
                        return -1;
                    }

                    if (Filter.OnlyStarred)
                    {
                        dings = dings.Where(d => d.Favorite == true).ToList();
                    }
                    if (Filter.OnlyPersonDetected)
                    {
                        dings = dings.Where(d => d.CvProperties != null &&
                                                 (d.CvProperties.PersonDetected == true ||
                                                  string.Equals(d.CvProperties.DetectionType, "human", StringComparison.OrdinalIgnoreCase))).ToList();
                    }
                    if (!string.IsNullOrWhiteSpace(Filter.DetectionType))
                    {
                        var detType = Filter.DetectionType.Trim();
                        dings = dings.Where(d => d.CvProperties != null &&
                                                 (string.Equals(d.CvProperties.DetectionType, detType, StringComparison.OrdinalIgnoreCase) ||
                                                  (string.Equals(detType, "human", StringComparison.OrdinalIgnoreCase) && d.CvProperties.PersonDetected == true))).ToList();
                    }
                    if (!string.IsNullOrWhiteSpace(Filter.Kind))
                    {
                        var kind = Filter.Kind.Trim();
                        dings = dings.Where(d => string.Equals(d.Kind, kind, StringComparison.OrdinalIgnoreCase)).ToList();
                    }
                    // Skip events that do not have a ready recording - snapshot/live-view events and
                    // in-progress recordings would otherwise fail when attempting to download.
                    dings = dings.Where(d => d.Recording != null &&
                                             string.Equals(d.Recording.Status, "ready", StringComparison.OrdinalIgnoreCase)).ToList();
                    dings = dings.OrderBy(d => d.CreatedAtDateTime).ToList();

                    int videoCount = 0;
                    if (dings.Count >= Filter.VideoCount)
                    {
                        videoCount = Filter.VideoCount;
                    }
                    else
                    {
                        videoCount = dings.Count;
                    }

                    // Print summary BEFORE starting downloads
                    string limitmessage = "";
                    int totalToDownload = videoCount;
                    if (dings.Count() > Filter.VideoCount)
                    {
                        limitmessage = $" (Will download {Filter.VideoCount} of {dings.Count()} based on MaxCount setting)";
                    }
                    reporter.Info("");
                    reporter.Highlight($"📊 Total Media: {dings.Count()} | Downloading: {totalToDownload}{limitmessage}");

                    // Print device breakdown
                    var byDevice = dings.Take(videoCount).GroupBy(d => d.Doorbot.Id).ToList();
                    StringBuilder sb = new();
                    if (!Filter.DeviceId.HasValue)
                    {
                        foreach (var grp in byDevice)
                        {
                            var name = deviceList.Devices.Where(d => d.Id == grp.FirstOrDefault().Doorbot.Id).FirstOrDefault().Name;
                            var count = grp.Count();
                            var s = "";
                            if (count > 1)
                            {
                                s = "s";
                            }
                            sb.Append($"{grp.Count()} video{s} from {name} and ");
                        }
                        if (sb.Length > 4)
                        {
                            sb.Length = sb.Length - 4;
                            reporter.Info($"Will download {sb.ToString()}");
                        }
                    }

                    reporter.Info(""); // Blank line before downloads start

                    // Every download gets its own persistent status row - grow the console buffer up front
                    // so a large batch never scrolls mid-run and corrupts previously written rows.
                    reporter.EnsureCapacity(videoCount);

                    // NOW start the downloads
                    List<Task<(bool success, DoorbotHistoryEvent ding)>> tasks = new();
                    for (int i = 0; i < videoCount; i++)
                    {
                        if (ct.IsCancellationRequested)
                        {
                            reporter.Warning($"Stopped queuing new downloads at {i}/{videoCount} (shutdown requested). Waiting for in-flight downloads to finish...");
                            break;
                        }
                        tasks.Add(SaveRecordingAsync(i + 1, dings[i], Filter, ct));
                    }

                    // Start background task to update speed every 5 seconds
                    using var speedUpdateCancellation = new CancellationTokenSource();
                    var speedUpdateTask = UpdateSpeedPeriodically(speedUpdateCancellation.Token);

                    results = (await Task.WhenAll(tasks.ToArray())).ToList();

                    // Stop the speed update task and do one final refresh so the footer reflects the
                    // now-zero active-download count instead of whatever was last drawn mid-run.
                    speedUpdateCancellation.Cancel();
                    UpdateFooterStatus();
                    var success = results.Count(r => r.success == true);
                    var successfulCreatedDates = results.Where(r => r.success == true).Select(r => r.ding.CreatedAtDateTime).ToList();
                    lastSuccess = successfulCreatedDates.Any() ? successfulCreatedDates.Max() : null;
                    failedCount = results.Count(r => r.success == false);
                    reporter.Highlight($"{Environment.NewLine}Successfully downloaded {success} videos");

                }
                else
                {
                    await DownloadSnapshots(Filter);
                }

                SaveSettings(lastSuccess, firstFailure);
                if (failedCount > 0)
                {
                    reporter.Error($"{Environment.NewLine}Failed to download {failedCount} videos.");
                    var failedCreatedDates = results.Where(r => r.success == false).Select(r => r.ding.CreatedAtDateTime).ToList();
                    firstFailure = failedCreatedDates.Any() ? failedCreatedDates.Min() : null;
                    if (firstFailure.HasValue)
                    {
                        TimeZoneInfo.Local.GetUtcOffset(firstFailure.Value);
                        var est = firstFailure.Value.ToLocalTime();
                        reporter.Warning($"Date of first failed download recorded ({est}). Rerun without a --start value to retry the downloads starting at that point");
                    }
                }

                // Generate failure report
                var existingFailures = LoadExistingFailuresList(reportsDirectory);
                GenerateFailureReport(reportsDirectory, existingFailures);

                reporter.Info($"{Environment.NewLine}Done!");
                reporter.ClearItems();
                return 0;
            }
            catch (Exception exe)
            {
                reporter.Error(exe.ToString());
                log.LogError(exe.ToString());
                return -1;
            }
            finally
            {
                IsRunActive = false;
            }
        }

        public async Task<bool> DownloadSnapshots(Filter filter)
        {
            var expandedPath = Environment.ExpandEnvironmentVariables(filter.DownloadPath);
            DateTime est = DateTime.Now;
            string fileNameFormat = Path.Combine(expandedPath,
                $"{est.Year}-{est.Month.ToString().PadLeft(2, '0')}-{est.Day.ToString().PadLeft(2, '0')}-T{est.Hour.ToString().PadLeft(2, '0')}_{est.Minute.ToString().PadLeft(2, '0')}_{est.Second.ToString().PadLeft(2, '0')}" + "--{0}.jpg");

            string fileName = string.Empty;
            var devices = await this.ringSession.GetRingDevices();
            if (devices == null)
                return false;

            if (devices.Doorbots != null)
            {
                foreach (var d in devices.Doorbots)
                {
                    if (d?.Id != null && !string.IsNullOrEmpty(d.Description))
                    {
                        fileName = string.Format(fileNameFormat, d.Description);
                        await GetSnapshot(d.Id, fileName);
                    }
                }
            }

            if (devices.Chimes != null)
            {
                foreach (var d in devices.Chimes)
                {
                    if (d?.Id != null && !string.IsNullOrEmpty(d.Description))
                    {
                        fileName = string.Format(fileNameFormat, d.Description);
                        await GetSnapshot(d.Id, fileName);
                    }
                }
            }

            if (devices.AuthorizedDoorbots != null)
            {
                foreach (var d in devices.AuthorizedDoorbots)
                {
                    if (d?.Id != null && !string.IsNullOrEmpty(d.Description))
                    {
                        fileName = string.Format(fileNameFormat, d.Description);
                        await GetSnapshot(d.Id, fileName);
                    }
                }
            }

            if (devices.StickupCams != null)
            {
                foreach (var d in devices.StickupCams)
                {
                    if (d?.Id != null && !string.IsNullOrEmpty(d.Description))
                    {
                        fileName = string.Format(fileNameFormat, d.Description);
                        await GetSnapshot((int)d.Id, fileName);
                    }
                }
            }

            return true;

        }
        internal async Task<bool> GetSnapshot(int doorbotId, string fileName)
        {
            try
            {
                await this.ringSession.UpdateSnapshot(doorbotId);
                await this.ringSession.GetLatestSnapshot(doorbotId, fileName);
                reporter.Info($"Downloaded snapshot {fileName}");
                log.LogInformation($"Downloaded snapshot {fileName}");
                return true;
            }
            catch (Exception exe)
            {
                reporter.Warning($"Failed to download snapshot for device {doorbotId}: {exe.Message}");
                log.LogError(exe, $"Failed to download snapshot for device {doorbotId}");
                return false;
            }
        }

        internal async Task<(bool, DoorbotHistoryEvent ding)> SaveRecordingAsync(int index, DoorbotHistoryEvent ding, Filter filter, CancellationToken ct)
        {

            await semaphore.WaitAsync();
            var item = reporter.BeginItem("");
            Interlocked.Increment(ref activeDls);
            try
            {

                string filename = string.Empty;
                var expandedPath = Environment.ExpandEnvironmentVariables(filter.DownloadPath);

                TimeZoneInfo.Local.GetUtcOffset(ding.CreatedAtDateTime.Value);
                var est = ding.CreatedAtDateTime.Value.ToLocalTime();
                var date = $"{est.Year}-{est.Month.ToString().PadLeft(2, '0')}-{est.Day.ToString().PadLeft(2, '0')}";
                var time = $"{est.Hour.ToString().PadLeft(2, '0')}_{est.Minute.ToString().PadLeft(2, '0')}_{est.Second.ToString().PadLeft(2, '0')}";
                var shortFileName = $"{date}-{time}-{ding.Kind}.mp4";

                // Organize by location/camera name
                string locationName = ResolveLocationName(ding.Doorbot.Id);

                var locationDir = Path.Combine(expandedPath, locationName);
                var cameraDir = Path.Combine(locationDir, ding.Doorbot.Description);
                if (!Directory.Exists(cameraDir))
                    Directory.CreateDirectory(cameraDir);

                filename = Path.Combine(cameraDir, shortFileName);

                LogEventJson(ding, date, time);

                string msg = $"{index.ToString().PadLeft(3, '0')}) {locationName}/{ding.Doorbot.Description}/{shortFileName} | {ding.CreatedAtDateTime.Value.ToLocalTime().ToString("MM/dd/yyyy hh:mm:ss tt")} | {ding.Kind} :: ";
                reporter.WriteItem(item, msg);
                reporter.UpdateItem(item, "Downloading");

                int attempt = 1;
                Exception lastException = null;
                string lastWebResponseBody = null;
                DownloadRecording downloadInfo = null;
                do
                {
                    attempt++;

                    try
                    {
                        // Fetching the download info (URL + size, when the Ring service provides it) is a
                        // required round-trip before we can download the bytes either way, so this doubles
                        // as the check for whether the file we'd end up with already exists on disk.
                        if (downloadInfo == null)
                        {
                            downloadInfo = await this.ringSession.GetDoorbotHistoryRecordingInfo(ding);
                        }

                        if (downloadHelper.ValidateMediaExists(filename, downloadInfo.Size))
                        {
                            reporter.CompleteItem(item, "Exists");
                            return (true, ding);
                        }

                        await this.ringSession.GetDoorbotHistoryRecording(downloadInfo, filename);
                        long fileSizeBytes = new FileInfo(filename).Length;
                        Interlocked.Add(ref totalBytesDownloaded, fileSizeBytes);
                        reporter.CompleteItem(item, $"Complete - ({fileSizeBytes / 1048576} MB)");
                        UpdateFooterStatus();
                        break;
                    }
                    catch (AggregateException e)
                    {
                        lastException = e;
                        if (e.InnerException != null && e.InnerException.GetType() == typeof(System.Net.WebException) && ((System.Net.WebException)e.InnerException).Response != null)
                        {
                            var webException = (System.Net.WebException)e.InnerException;
                            using (var streamReader = new StreamReader(webException.Response.GetResponseStream()))
                            {
                                lastWebResponseBody = streamReader.ReadToEnd();
                            }
                            reporter.ErrorItem(item, $"Failed: ({(e.InnerException != null ? e.InnerException.Message : e.Message)} - {lastWebResponseBody})");
                        }
                        else
                        {
                            reporter.ErrorItem(item, $"Failed: ({(e.InnerException != null ? e.InnerException.Message : e.Message)})");
                        }
                    }
                    catch (Exception exe)
                    {
                        lastException = exe;
                        reporter.ErrorItem(item, $"Failed ({(exe.InnerException != null ? exe.InnerException.Message : exe.Message)})");
                    }

                    if (attempt >= 10 || ct.IsCancellationRequested)
                    {
                        reporter.WarnItem(item, ct.IsCancellationRequested
                           ? "Giving up - shutdown requested."
                           : $"Giving up after {attempt} tries.");
                        RecordFailedDownload(ding, lastException, lastWebResponseBody);
                        return (false, ding);
                    }
                    else
                    {
                        reporter.WarnItem(item, $"Retrying: {attempt + 1}/10.");
                    }

                } while (attempt < 10 && !ct.IsCancellationRequested);

                return (true, ding);
            }
            finally
            {
                reporter.ReleaseItem(item);
                Interlocked.Decrement(ref activeDls);
                semaphore.Release();
            }
        }

        private string GetDownloadSpeed()
        {
            var elapsed = DateTime.Now - downloadStartTime;
            if (elapsed.TotalSeconds < 1)
                return "calculating...";

            double bytesPerSec = totalBytesDownloaded / elapsed.TotalSeconds;
            double mbPerSec = bytesPerSec / (1024 * 1024);
            return $"{mbPerSec:F2} MB/s";
        }

        private void UpdateFooterStatus()
        {
            var speed = GetDownloadSpeed();
            string status = $"▓ Active Downloads: {activeDls} | Speed: {speed} | Total: {totalBytesDownloaded / (1024 * 1024)} MB";
            reporter.UpdateFooter(status);
        }

        private async Task UpdateSpeedPeriodically(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(5000, ct);
                    if (!ct.IsCancellationRequested)
                    {
                        UpdateFooterStatus();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
        }

        private void LoadExistingFailures(string reportsDir)
        {
            try
            {
                if (!Directory.Exists(reportsDir))
                    return;

                string tsvPath = Path.Combine(reportsDir, "download_failures.tsv");
                if (!System.IO.File.Exists(tsvPath))
                    return;

                using (var reader = new StreamReader(tsvPath))
                {
                    string line;
                    bool isHeader = true;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (isHeader)
                        {
                            isHeader = false;
                            continue;
                        }

                        var parts = line.Split('\t');
                        if (parts.Length >= 5)
                        {
                            loadedEventIds.Add(parts[4]); // EventId is at index 4
                        }
                    }
                }
                log.LogInformation($"Loaded {loadedEventIds.Count} previously failed event IDs from existing report");
            }
            catch (Exception exe)
            {
                log.LogError(exe, "Failed to load existing failures from report");
            }
        }

        /// <summary>
        /// Resolves a device id to its location name. The doorbot history API does not embed
        /// location_id on its nested doorbot object, so this goes through the device-id -> location-id
        /// lookup (built from the device-list APIs) rather than reading ding.Doorbot.LocationId directly.
        /// </summary>
        private string ResolveLocationName(long deviceId)
        {
            if (deviceIdToLocationId.TryGetValue(deviceId, out var locationId))
            {
                if (locationNameCache.TryGetValue(locationId, out var name))
                    return name;
                return locationId.ToString().Substring(0, 8); // Fallback: first 8 chars of GUID
            }
            return "Unknown Location";
        }

        /// <summary>
        /// Writes the full JSON of a single Ring event ("ding") to its own file, named to match the
        /// downloaded video (camera-date-time-kind), so the two can be cross-referenced on disk.
        /// </summary>
        private void LogEventJson(DoorbotHistoryEvent ding, string date, string time)
        {
            if (string.IsNullOrEmpty(logsDirectory))
                return;

            try
            {
                var safeCameraName = string.Join("_", ding.Doorbot.Description.Split(Path.GetInvalidFileNameChars()));
                var fileName = $"{safeCameraName}-{date}-T{time}-{ding.Kind}.json";
                var filePath = Path.Combine(logsDirectory, fileName);

                var json = JsonUtil.Serialize(ding, JsonMode.Pretty);

                System.IO.File.WriteAllText(filePath, json, Utf8NoBom);
                log.LogInformation("PerEventJson {fileName} written", Path.GetFileName(filePath));
            }
            catch (Exception exe)
            {
                log.LogWarning(exe, "Failed to write per-event JSON log file");
            }
        }

        private void RecordFailedDownload(DoorbotHistoryEvent ding, Exception exception, string webResponseBody = null)
        {
            try
            {
                string eventId = ding.Id.ToString();

                // Skip if already recorded
                if (loadedEventIds.Contains(eventId))
                {
                    log.LogInformation($"Skipping duplicate failure record for EventId {eventId}");
                    return;
                }

                string errorDesc = ExtractErrorMessage(exception, webResponseBody);

                string locationName = ResolveLocationName(ding.Doorbot.Id);

                var failure = new FailedDownload
                {
                    Timestamp = DateTime.Now,
                    LocationName = locationName,
                    CameraName = ding.Doorbot.Description,
                    CameraId = ding.Doorbot.Id,
                    EventId = eventId,
                    EventType = ding.Kind,
                    CreatedAt = ding.CreatedAtDateTime.Value,
                    ErrorDescription = errorDesc
                };

                newFailures.Add(failure);
                loadedEventIds.Add(eventId); // Mark as recorded for this session
                log.LogInformation($"Recorded failed download for EventId {eventId}: {errorDesc}");
            }
            catch (Exception exe)
            {
                log.LogError(exe, "Failed to record download failure");
            }
        }

        private string ExtractErrorMessage(Exception exception, string webResponseBody = null)
        {
            if (webResponseBody != null)
                return webResponseBody.Length > 200 ? webResponseBody.Substring(0, 200) : webResponseBody;

            if (exception?.InnerException != null)
                return exception.InnerException.Message;

            return exception?.Message ?? "Unknown error";
        }

        private void GenerateFailureReport(string reportsDir, List<FailedDownload> existingFailures)
        {
            try
            {
                if (!Directory.Exists(reportsDir))
                    Directory.CreateDirectory(reportsDir);

                string tsvPath = Path.Combine(reportsDir, "download_failures.tsv");

                // Merge existing and new failures, sort chronologically
                var allFailures = existingFailures.Concat(newFailures.ToList())
                   .OrderBy(f => f.Timestamp)
                   .ToList();

                if (allFailures.Count == 0)
                    return;

                using (var writer = new StreamWriter(tsvPath, false, Encoding.UTF8))
                {
                    // Write header
                    writer.WriteLine("Date\tTime\tTimezone\tLocationName\tCameraName\tCameraId\tEventId\tEventType\tErrorDescription");

                    // Write rows
                    foreach (var failure in allFailures)
                    {
                        var eventTime = failure.CreatedAt.ToLocalTime();
                        var tzInfo = TimeZoneInfo.Local.GetUtcOffset(failure.CreatedAt);
                        string tzStr = $"UTC{(tzInfo.TotalHours >= 0 ? "+" : "")}{tzInfo.TotalHours:F0}";
                        string line = $"{eventTime:yyyy-MM-dd}\t{eventTime:HH:mm:ss}\t{tzStr}\t{failure.LocationName}\t{failure.CameraName}\t{failure.CameraId}\t{failure.EventId}\t{failure.EventType}\t{failure.ErrorDescription}";
                        writer.WriteLine(line);
                    }
                }

                if (newFailures.Count > 0)
                    log.LogInformation($"Generated failure report with {allFailures.Count} total entries ({newFailures.Count} new)");
            }
            catch (Exception exe)
            {
                log.LogError(exe, "Failed to generate failure report");
            }
        }

        /// <summary>
        /// Appends a snapshot of camera/doorbell connectivity, battery and wifi health to
        /// reports/camera_health.tsv on every run, using the "health" object already embedded in the
        /// ring_devices response - so a degraded battery or dropped-connection camera shows up in the
        /// report history even if it never causes a download failure.
        /// </summary>
        private void GenerateCameraHealthReport(string reportsDir, Devices allDevices, Dictionary<Guid, string> locationNames)
        {
            try
            {
                if (!Directory.Exists(reportsDir))
                    Directory.CreateDirectory(reportsDir);

                string tsvPath = Path.Combine(reportsDir, "camera_health.tsv");
                bool writeHeader = !System.IO.File.Exists(tsvPath);

                var rows = new List<string>();

                void AddRow(string name, long? id, string kind, Guid? locationId, DeviceHealth health)
                {
                    string locName = locationId.HasValue && locationNames.TryGetValue(locationId.Value, out var ln) ? ln : "";
                    string connected = health?.Connected?.ToString() ?? "";
                    string batteryPct = health?.BatteryPercentage?.ToString() ?? "";
                    string batteryVoltCat = health?.BatteryVoltageCategory ?? "";
                    string rssiCat = health?.RssiCategory ?? "";
                    string rssi = health?.Rssi?.ToString() ?? "";
                    string fwStatus = health?.FirmwareVersionStatus ?? "";
                    string ota = health?.OtaStatus ?? "";
                    rows.Add($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}\t{locName}\t{name}\t{id}\t{kind}\t{connected}\t{batteryPct}\t{batteryVoltCat}\t{rssiCat}\t{rssi}\t{fwStatus}\t{ota}");
                }

                foreach (var d in allDevices.StickupCams)
                    AddRow(d.Description ?? $"Device {d.Id}", d.Id, d.Kind, d.LocationId, d.Health);
                foreach (var d in allDevices.Doorbots.Concat(allDevices.AuthorizedDoorbots))
                    AddRow(d.Description ?? $"Device {d.Id}", d.Id, d.Kind, d.LocationId, d.Health);

                if (rows.Count == 0)
                    return;

                using (var writer = new StreamWriter(tsvPath, append: true, Encoding.UTF8))
                {
                    if (writeHeader)
                        writer.WriteLine("Timestamp\tLocationName\tCameraName\tCameraId\tKind\tConnected\tBatteryPercentage\tBatteryVoltageCategory\tWifiRssiCategory\tRssi\tFirmwareStatus\tOtaStatus");
                    foreach (var row in rows)
                        writer.WriteLine(row);
                }

                log.LogInformation($"Appended {rows.Count} camera health entries to {tsvPath}");
            }
            catch (Exception exe)
            {
                log.LogError(exe, "Failed to generate camera health report");
            }
        }

        private List<FailedDownload> LoadExistingFailuresList(string reportsDir)
        {
            var existing = new List<FailedDownload>();
            try
            {
                if (!Directory.Exists(reportsDir))
                    return existing;

                string tsvPath = Path.Combine(reportsDir, "download_failures.tsv");
                if (!System.IO.File.Exists(tsvPath))
                    return existing;

                using (var reader = new StreamReader(tsvPath))
                {
                    string line;
                    bool isHeader = true;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (isHeader)
                        {
                            isHeader = false;
                            continue;
                        }

                        var parts = line.Split('\t');
                        if (parts.Length >= 8)
                        {
                            // Handle both old format (8 columns) and new format (9 columns with LocationName)
                            bool isNewFormat = parts.Length >= 9;

                            if (DateTime.TryParse($"{parts[0]} {parts[1]}", out var eventTime))
                            {
                                string locationName = isNewFormat ? parts[3] : "Unknown Location";
                                string cameraName = isNewFormat ? parts[4] : parts[3];
                                string cameraIdStr = isNewFormat ? parts[5] : parts[4];
                                string eventId = isNewFormat ? parts[6] : parts[5];
                                string eventType = isNewFormat ? parts[7] : parts[6];
                                string errorDesc = isNewFormat ? parts[8] : parts[7];

                                if (int.TryParse(cameraIdStr, out var cameraId))
                                {
                                    existing.Add(new FailedDownload
                                    {
                                        Timestamp = eventTime,
                                        LocationName = locationName,
                                        CameraName = cameraName,
                                        CameraId = cameraId,
                                        EventId = eventId,
                                        EventType = eventType,
                                        CreatedAt = eventTime,
                                        ErrorDescription = errorDesc
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception exe)
            {
                log.LogError(exe, "Failed to load existing failures list");
            }
            return existing;
        }

        public async Task<DeviceList> GetDevicesList(string username = "", string password = "")
        {
            if (this.ringSession == null)
            {
                if (!string.IsNullOrEmpty(username))
                {
                    this.Auth.UserName = username;
                }
                if (!string.IsNullOrEmpty(password))
                {
                    // Keep as a fallback credential only - don't discard a cached refresh token here,
                    // see the matching note in Worker.SetFilterAndAuthValues.
                    this.Auth.Password = password;
                }
                this.ringSession = await Authenticate();
            }

            Devices devices = new();
            try
            {
                devices = await reporter.RunWithStatusAsync("Getting list of registered devices...", async updateStatus =>
                {
                    var d = await ringSession.GetRingDevices();
                    await Task.Delay(500);
                    return d;
                });
            }
            catch (Exception exe)
            {
                reporter.Error(exe.Message);
                log.LogError(exe.ToString());
                return null;
            }

            DeviceList deviceList = new DeviceList().ExtractDevices(devices);
            reporter.Highlight("Found registered devices:");
            foreach (var x in deviceList.Devices)
            {
                reporter.Info($"{x.Name}\tId: {x.Id}");
            }

            return deviceList;
        }

        /// <summary>
        /// Checks whether <paramref name="auth"/> has enough to attempt authentication with.
        /// A refresh token alone is sufficient - AuthenticateWithCredentials tries it before falling
        /// back to username/password, so username/password are only required when there's no refresh token.
        /// </summary>
        /// <returns>Null if authentication can proceed, otherwise a user-facing error message.</returns>
        public static string ResolveAuthError(RingCredentials auth)
        {
            if (!string.IsNullOrWhiteSpace(auth.RefreshToken))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(auth.UserName))
            {
                return "A Ring username is required";
            }

            if (string.IsNullOrWhiteSpace(auth.Password))
            {
                return "A Ring password is required";
            }

            return null;
        }
    }
}
