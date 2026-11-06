using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using VideoForensics.Client.Hosting;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Sqlite.Migrations;
using VideoForensics.Providers.Ring;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Utils.SelfTest.Tests
{
    /// <summary>
    /// Runs the real Ring sync (device list, config, account settings, location data, event history, health capture) once against
    /// placeholder-valued copies of the saved Ring responses, writing into a real migrated SQLite file, then proves by value that
    /// every mapped response leaf is stored in the column the field map names. No network and no credentials: the Ring session
    /// talks to a stub HTTP handler.
    /// </summary>
    public sealed class FieldMapRoundTripTests : IClassFixture<FieldMapRoundTripTests.SyncedDatabase>
    {
        private readonly SyncedDatabase _synced;
        private readonly ITestOutputHelper _output;

        public FieldMapRoundTripTests(SyncedDatabase synced, ITestOutputHelper output)
        {
            _synced = synced;
            _output = output;
        }

        [Fact]
        public void Sync_EveryMappedFixtureValue_IsStoredInItsMappedColumn()
        {
            FieldValueReport report = _synced.Report;
            _output.WriteLine(report.Summary());
            foreach (ValueFinding s in report.Shadowed)
            {
                _output.WriteLine($"shadowed {s.Endpoint}:{s.Path} -> {s.Target}");
            }

            foreach (string line in _synced.SyncLog)
            {
                _output.WriteLine(line);
            }

            string[] failures =
            [
                .. report.Mismatches.Select(f => $"MISMATCH {f.Endpoint}:{f.Path} -> {f.Target}: {f.Detail}"),
                .. report.MissingRows.Select(f => $"MISSING {f.Endpoint}:{f.Path} -> {f.Target}: {f.Detail}")
            ];
            foreach (string failure in failures)
            {
                _output.WriteLine(failure);
            }

            Assert.True(failures.Length == 0, $"{failures.Length} fixture value(s) are not stored as mapped:{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
        }

        [Fact]
        public void Sync_Report_ComparesAMeaningfulNumberOfValues()
        {
            // Guards against the check passing because nothing was compared (an unreadable fixture, an empty sync).
            Assert.True(_synced.Report.Matched > 800, $"only {_synced.Report.Matched} values were compared");
            foreach (string endpoint in new[] { "devices", "device-settings", "profile", "ringtones", "locations", "location-mode", "account-monitoring-status" })
            {
                Assert.True(_synced.Report.ByEndpoint.TryGetValue(endpoint, out EndpointValueCounts? counts) && counts.Matched > 0, $"{endpoint} matched nothing");
            }
        }

        [Fact]
        public async Task Verifier_StoredValueChangedAfterSync_ReportsAMismatch()
        {
            await using var connection = new SqliteConnection($"Data Source={_synced.DbPath}");
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            using (SqliteCommand tamper = connection.CreateCommand())
            {
                tamper.CommandText = "UPDATE RingLocationModes SET Mode = 'tampered-value'";
                _ = await tamper.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }

            var verifier = new FieldMapValueVerifier(new SqliteDbValueSource(connection, _synced.Model));
            FieldValueReport report = verifier.Evaluate(FieldMap.LoadEmbedded(), _synced.Documents);

            Assert.Contains(report.Mismatches, f => f.Endpoint == "location-mode" && f.Target == "RingLocationModes.Mode");
        }

        [Fact]
        public async Task Verifier_RowsDeletedAfterSync_ReportsMissingRows()
        {
            await using var connection = new SqliteConnection($"Data Source={_synced.DbPath}");
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            using (SqliteCommand tamper = connection.CreateCommand())
            {
                tamper.CommandText = "PRAGMA foreign_keys = OFF; DELETE FROM RingLocationModeSharings";
                _ = await tamper.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }

            var verifier = new FieldMapValueVerifier(new SqliteDbValueSource(connection, _synced.Model));
            FieldValueReport report = verifier.Evaluate(FieldMap.LoadEmbedded(), _synced.Documents);

            Assert.Contains(report.MissingRows, f => f.Endpoint == "location-mode-sharing");
        }

        // ------------------------------------------------------------------------------------------------------------
        // The synced database
        // ------------------------------------------------------------------------------------------------------------

        private const string LocationId = "dc5ff64d-bb2e-4c1f-9b74-ee0db6a8d2e9";
        private const string AccessToken = "SESSION-TOKEN-NEVER-STORED";

        private const string ProfileUrl = "https://api.ring.com/clients_api/profile";
        private const string RingtonesUrl = "https://api.ring.com/clients_api/ringtones";
        private const string LocationsUrl = "https://api.ring.com/devices/v1/locations";
        private const string RingDevicesUrl = "https://api.ring.com/clients_api/ring_devices";
        private const string GroupsUrl = $"https://api.ring.com/groups/v1/locations/{LocationId}/groups";
        private const string MonitoringUrl = $"https://prd-api-us.prd.rings.solutions/api/v1/rs/monitoring/accounts/{LocationId}";
        private const string ModeUrl = $"https://prd-api-us.prd.rings.solutions/api/v1/mode/location/{LocationId}";
        private const string VideoSearchUrl = "https://api.ring.com/clients_api/video_search/history";
        private const string LocationEventsUrl = $"https://api.ring.com/clients_api/locations/{LocationId}/events";
        private const string LocationHistoryUrl = $"https://api.ring.com/evm/v2/history/locations/{LocationId}";
        private const string AppLocationHistoryUrl = "https://prd-api-us.prd.rings.solutions/api/v1/rs/history";

        /// <summary>Endpoints no sync code fetches: nothing writes them, so their leaves are reported as not synced.</summary>
        private static readonly HashSet<string> UnsyncedEndpoints = new(StringComparer.Ordinal)
        {
            "doorbot-health", "chime-health", "shared-users", "linked-chime-doorbots", "snapshot-timestamps", "signalling-ticket", "active-dings", "amazon-key-locks"
        };

        public sealed class SyncedDatabase : IAsyncLifetime
        {
            private string _directory = string.Empty;
            private ServiceProvider? _services;

            public string DbPath { get; private set; } = string.Empty;
            public Microsoft.EntityFrameworkCore.Metadata.IModel Model { get; private set; } = null!;
            internal List<FixtureDocument> Documents { get; } = [];
            public List<string> SyncLog { get; } = [];
            internal FieldValueReport Report { get; private set; } = new();

            public async ValueTask InitializeAsync()
            {
                CancellationToken ct = TestContext.Current.CancellationToken;
                _directory = Path.Combine(Path.GetTempPath(), $"vf-roundtrip-{Guid.NewGuid():N}");
                _ = Directory.CreateDirectory(_directory);
                DbPath = Path.Combine(_directory, "videoforensics.db");

                var logger = new RecordingLogger();
                var services = new ServiceCollection();
                _ = services.AddLogging();
                _ = services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
                _ = services.AddVideoForensicsDataLayer(DbPath);
                _ = services.AddVideoForensicsServerCore("Ring");
                _services = services.BuildServiceProvider();

                IDbContextFactory<VideoForensicsDbContext> factory = _services.GetRequiredService<IDbContextFactory<VideoForensicsDbContext>>();
                await DatabaseInitializer.InitializeAsync(factory, logger, ct, _services.GetServices<IReferenceDataSource>());
                await using (VideoForensicsDbContext model = await factory.CreateDbContextAsync(ct))
                {
                    Model = model.Model;
                }

                Dictionary<string, FixtureSet> fixtures = FixtureSet.LoadAll();
                StubHandler handler = BuildHandler(fixtures);

                using IServiceScope scope = _services.CreateScope();
                IServiceProvider sp = scope.ServiceProvider;
                IVideoForensicsDataClient data = sp.GetRequiredService<IVideoForensicsDataClient>();

                // The account, location and device rows exist before a sync, created by the same identity resolver the download path uses.
                (_, ProviderAccount account) = await data.EnsureUserAndAccountAsync("Ring", "roundtrip-user", "Roundtrip User", null, ct);
                await using (VideoForensicsDbContext db = await factory.CreateDbContextAsync(ct))
                {
                    _ = db.RingAccounts.Add(new RingAccount { Id = account.Id, ProviderAccountId = account.Id, SubscriptionLevel = "unknown" });
                    _ = await db.SaveChangesAsync(ct);
                }

                // Rows the download path creates before any sync: the location (named from the location list) and each downloadable
                // device with its time zone and detail fields, exactly as RingMediaDownloadService.EnsureDeviceIdentityAsync passes them.
                // Chimes are never downloaded, so theirs is created bare.
                string locationName = JsonDocument.Parse(fixtures["locations"].Text).RootElement.GetProperty("user_locations")[0].GetProperty("name").GetString()!;
                VideoForensics.Data.Common.Entities.Location location = await data.EnsureLocationAsync(account.Id, LocationId, locationName, null, ct: ct);
                var deviceIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
                VideoForensics.Providers.Ring.Entities.Devices deviceList =
                    System.Text.Json.JsonSerializer.Deserialize<VideoForensics.Providers.Ring.Entities.Devices>(fixtures["devices"].Text)!;
                foreach (JsonProperty group in JsonDocument.Parse(fixtures["devices"].Text).RootElement.EnumerateObject().Where(g => g.Value.ValueKind == JsonValueKind.Array))
                {
                    foreach (JsonElement device in group.Value.EnumerateArray())
                    {
                        string id = device.GetProperty("id").ToString();
                        bool chime = group.Name == "chimes";
                        VideoForensics.Providers.Ring.Services.RingDeviceDetails? details = chime ? null : RingDeviceDetailsMapper.Find(deviceList, id);
                        Device row = await data.EnsureDeviceAsync(
                            location.Id, id, device.GetProperty("description").GetString()!, chime ? "chime" : group.Name == "doorbots" ? "doorbell" : "camera", null,
                            ct: ct, timeZoneId: chime ? device.GetProperty("time_zone").GetString() : details?.TimeZone, details: details?.DeviceFields);
                        deviceIds[id] = row.Id;
                    }
                }

                var session = new Session("user@example.com", "password", handler);
                var sessionProvider = new SessionProvider();
                sessionProvider.SetSession(account.Id, session);
                _ = await session.Authenticate();

                var service = new RingDeviceDiscoveryService(
                    logger,
                    sessionProvider,
                    metadataRepository: sp.GetRequiredService<ILocationMetadataRepository>(),
                    locationRepository: sp.GetRequiredService<ILocationRepository>(),
                    settingsPersister: new RingAccountSettingsPersister(sp.GetRequiredService<IRingAccountSettingsWriter>(), logger),
                    eventHistoryWriter: sp.GetRequiredService<IRingEventHistoryWriter>(),
                    deviceSettingsPersister: new RingDeviceSettingsPersister(data, sp.GetRequiredService<IDeviceRepository>(), logger));

                IReadOnlyList<VideoForensics.Core.Providers.Common.Contracts.Location> locations = await service.GetLocationsAsync(account.Id, ct);

                // The location metadata task is fire-and-forget inside the sync; wait for its rows, then run it once more as a no-op check.
                await WaitForRowsAsync(factory, "LocationMetadata", ct);
                await WaitForRowsAsync(factory, "RingLocationDetails", ct);
                await service.PersistLocationMetadataAsync(locations, ct);

                // The health snapshot the download path takes for each device (device list health object, DeviceReportedHealths and friends).
                var health = new RingDeviceHealthCapture(data, logger);
                foreach ((string providerId, Guid deviceGuid) in deviceIds)
                {
                    await health.CaptureDeviceHealthSnapshotAsync(session, providerId, deviceGuid, ct);
                }

                SyncLog.AddRange(logger.Entries.Where(e => e.Level >= LogLevel.Warning).Select(e => $"log {e.Level}: {e.Message}"));
                SyncLog.AddRange(handler.UnroutedRequests.Select(u => $"unrouted request: {u}"));

                foreach (FixtureSet set in fixtures.Values)
                {
                    Documents.AddRange(set.Documents(UnsyncedEndpoints));
                }

                if (Environment.GetEnvironmentVariable("VF_ROUNDTRIP_KEEP_DB") == "1")
                {
                    foreach (FixtureDocument doc in Documents)
                    {
                        File.WriteAllText(Path.Combine(_directory, $"{doc.Name}.json"), doc.Root.GetRawText());
                    }
                }

                SqliteConnection.ClearAllPools();
                await using var connection = new SqliteConnection($"Data Source={DbPath}");
                await connection.OpenAsync(ct);
                Report = new FieldMapValueVerifier(new SqliteDbValueSource(connection, Model)).Evaluate(FieldMap.LoadEmbedded(), Documents);
            }

            public ValueTask DisposeAsync()
            {
                _services?.Dispose();
                SqliteConnection.ClearAllPools();
                if (Environment.GetEnvironmentVariable("VF_ROUNDTRIP_KEEP_DB") == "1")
                {
                    // Debugging aid: keep the synced database so its rows can be read with any SQLite tool.
                    File.WriteAllText(Path.Combine(Path.GetTempPath(), "vf-roundtrip-last.txt"), DbPath);
                    return ValueTask.CompletedTask;
                }

                try
                {
                    Directory.Delete(_directory, recursive: true);
                }
                catch (IOException)
                {
                    // A temp file still held by the test host is cleaned up by the OS temp sweep.
                }

                return ValueTask.CompletedTask;
            }

            private static async Task WaitForRowsAsync(IDbContextFactory<VideoForensicsDbContext> factory, string table, CancellationToken ct)
            {
                for (int attempt = 0; attempt < 100; attempt++)
                {
                    await using VideoForensicsDbContext db = await factory.CreateDbContextAsync(ct);
                    // Why: table names are test-owned and cannot be parameters; they are checked as plain identifiers first.
                    if (!System.Text.RegularExpressions.Regex.IsMatch(table, "^[A-Za-z_][A-Za-z0-9_]*$"))
                    {
                        throw new ArgumentException("Table name must be a plain identifier.", nameof(table));
                    }
#pragma warning disable EF1003 // table is validated above
                    int count = await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM " + table).SingleAsync(ct);
#pragma warning restore EF1003
                    if (count > 0)
                    {
                        return;
                    }

                    await Task.Delay(50, ct);
                }
            }

            /// <summary>Serves the placeholder-valued responses for every URL the sync fetches; anything else is a 404.</summary>
            private static StubHandler BuildHandler(Dictionary<string, FixtureSet> fixtures)
            {
                var handler = new StubHandler(AccessToken);
                handler.Routes[ProfileUrl] = fixtures["profile"].Text;
                handler.Routes[RingtonesUrl] = fixtures["ringtones"].Text;
                handler.Routes[LocationsUrl] = fixtures["locations"].Text;
                handler.Routes[RingDevicesUrl] = fixtures["devices"].Text;
                handler.Routes[GroupsUrl] = fixtures["light-groups"].Text;
                handler.Routes[ModeUrl] = fixtures["location-mode"].Text;
                handler.Routes[$"{ModeUrl}/settings"] = fixtures["location-mode-settings"].Text;
                handler.Routes[$"{ModeUrl}/sharing"] = fixtures["location-mode-sharing"].Text;
                handler.Routes[MonitoringUrl] = fixtures["account-monitoring-status"].Text;
                handler.Routes[VideoSearchUrl] = fixtures["video-search"].Text;
                handler.Routes[LocationEventsUrl] = fixtures["location-events"].Text;
                handler.Routes[LocationHistoryUrl] = fixtures["location-history"].Text;
                handler.Routes[AppLocationHistoryUrl] = fixtures["app-location-history"].Text;

                foreach (JsonProperty group in JsonDocument.Parse(fixtures["devices"].Text).RootElement.EnumerateObject().Where(g => g.Value.ValueKind == JsonValueKind.Array))
                {
                    foreach (JsonElement device in group.Value.EnumerateArray())
                    {
                        string id = device.GetProperty("id").ToString();
                        handler.Routes[$"https://api.ring.com/clients_api/doorbots/{id}/history"] = fixtures["doorbot-history"].Text;
                        string? settings = fixtures.Values.SelectMany(f => f.DeviceSettings).Where(s => s.DeviceId == id).Select(s => s.Text).FirstOrDefault();
                        if (settings is not null)
                        {
                            handler.Routes[$"https://api.ring.com/devices/v1/devices/{id}/settings"] = settings;
                        }
                    }
                }

                return handler;
            }
        }

        // ------------------------------------------------------------------------------------------------------------
        // Fixtures: placeholder-valued copies of the saved responses
        // ------------------------------------------------------------------------------------------------------------

        /// <summary>The device ids the fixtures are given (the saved responses use one device per kind with the same placeholder id).</summary>
        private static readonly Dictionary<string, string> DeviceIdByGroup = new(StringComparer.Ordinal)
        {
            ["doorbots"] = "1001",
            ["stickup_cams"] = "2002",
            ["chimes"] = "3003"
        };

        private sealed class FixtureSet
        {
            private readonly string _endpoint;

            private FixtureSet(string endpoint, string text, List<(string DeviceId, string Text)> deviceSettings)
            {
                _endpoint = endpoint;
                Text = text;
                DeviceSettings = deviceSettings;
            }

            /// <summary>The response body the stub serves (and the verifier compares against).</summary>
            public string Text { get; }

            /// <summary>For device-settings: one body per device it is served for.</summary>
            public List<(string DeviceId, string Text)> DeviceSettings { get; }

            public static Dictionary<string, FixtureSet> LoadAll()
            {
                string dir = Path.Combine(AppContext.BaseDirectory, "fixtures");
                var sets = new Dictionary<string, FixtureSet>(StringComparer.Ordinal);
                foreach (IGrouping<string, string> files in Directory.EnumerateFiles(dir, "*.json")
                    .GroupBy(f => Path.GetFileName(f)[..Path.GetFileName(f).IndexOf('_', StringComparison.Ordinal)], StringComparer.Ordinal))
                {
                    string endpoint = files.Key;
                    string first = files.OrderBy(f => f, StringComparer.Ordinal).First();
                    if (endpoint == "device-settings")
                    {
                        // fixturec is the doorbot's full settings body, fixturea the camera's; fixtureb is a second camera, served for an extra stickup cam.
                        var bodies = new List<(string, string)>();
                        foreach (string file in files.OrderBy(f => f, StringComparer.Ordinal))
                        {
                            string deviceId = Path.GetFileName(file).Contains("fixturec", StringComparison.Ordinal) ? "1001"
                                : Path.GetFileName(file).Contains("fixturea", StringComparison.Ordinal) ? "2002" : "2003";
                            bodies.Add((deviceId, Diversify(endpoint + deviceId, File.ReadAllText(file))));
                        }

                        sets[endpoint] = new FixtureSet(endpoint, bodies[0].Item2, bodies);
                        continue;
                    }

                    string text = Diversify(endpoint, File.ReadAllText(first));
                    if (endpoint == "devices")
                    {
                        text = ShapeDeviceList(text);
                    }
                    else if (endpoint == "locations")
                    {
                        JsonObject locations = JsonNode.Parse(text)!.AsObject();
                        locations["user_locations"]![0]!["location_id"] = LocationId;
                        text = locations.ToJsonString();
                    }

                    sets[endpoint] = new FixtureSet(endpoint, text, []);
                }

                return sets;
            }

            public IEnumerable<FixtureDocument> Documents(HashSet<string> unsynced)
            {
                if (_endpoint == "device-settings")
                {
                    foreach ((string deviceId, string text) in DeviceSettings)
                    {
                        yield return new FixtureDocument(_endpoint, $"{_endpoint}_{deviceId}", JsonDocument.Parse(text).RootElement, DeviceScope: deviceId);
                    }

                    yield break;
                }

                yield return new FixtureDocument(_endpoint, _endpoint, JsonDocument.Parse(Text).RootElement,
                    ScopeFromTopLevelId: _endpoint == "devices", Synced: !unsynced.Contains(_endpoint));
            }
        }

        /// <summary>
        /// Gives every device its own id, the shared location id and real-looking health units. The saved responses use one
        /// placeholder id for every device, so they could not be told apart in the database.
        /// </summary>
        private static string ShapeDeviceList(string json)
        {
            JsonObject root = JsonNode.Parse(json)!.AsObject();

            // A second stickup cam, for the third device-settings body.
            if (root["stickup_cams"] is JsonArray cams && cams.Count > 0)
            {
                cams.Add(JsonNode.Parse(cams[0]!.ToJsonString()));
            }

            int extra = 0;
            foreach ((string group, string id) in DeviceIdByGroup)
            {
                if (root[group] is not JsonArray devices)
                {
                    continue;
                }

                for (int i = 0; i < devices.Count; i++)
                {
                    JsonObject device = devices[i]!.AsObject();
                    device["id"] = long.Parse(id, CultureInfo.InvariantCulture) + i;
                    device["location_id"] = LocationId;
                    device["device_id"] = $"hw-{id}-{i}";
                    device["description"] = $"Device {id}-{i}";
                    device["kind"] = group switch { "doorbots" => "doorbell_graham_cracker", "stickup_cams" => "stickup_cam_elite", _ => "chime_pro_v2" };
                    if (device["health"] is JsonObject health)
                    {
                        // 32768 Hz ticks since the epoch for 2026-09; deci-kelvin for about 19 degrees C.
                        health["status_time"] = (32768L * 1_790_000_000L) + extra;
                        health["battery_temp"] = 2921 + extra;
                        health["last_update_time"] = 1_790_000_000L + extra;
                        if (health["batteries"] is JsonArray batteries)
                        {
                            foreach (JsonNode? battery in batteries)
                            {
                                battery!["battery_temp"] = 2905 + extra;
                            }
                        }
                    }

                    extra++;
                }
            }

            return root.ToJsonString();
        }

        /// <summary>
        /// Replaces the placeholder leaves of a response ("x", 1, 1.5, booleans) with distinct fake values, so a value that lands in
        /// the wrong column or the wrong row differs from its neighbours. Strings named like dates become ISO dates. Keys stay as is.
        /// </summary>
        private static string Diversify(string seed, string json)
        {
            JsonNode root = JsonNode.Parse(json)!;
            int counter = 0;
            string tag = seed.Replace('_', '-');
            JsonNode? Walk(JsonNode? node, string name, string path)
            {
                switch (node)
                {
                    case JsonObject obj:
                        foreach (string key in obj.Select(p => p.Key).ToList())
                        {
                            JsonNode? child = obj[key];
                            JsonNode? replaced = Walk(child, key, path + "." + key);
                            if (!ReferenceEquals(child, replaced))
                            {
                                obj[key] = replaced;
                            }
                        }

                        return obj;
                    case JsonArray array:
                        for (int i = 0; i < array.Count; i++)
                        {
                            JsonNode? child = array[i];
                            JsonNode? replaced = Walk(child, name, path + "[]");
                            if (!ReferenceEquals(child, replaced))
                            {
                                array[i] = replaced;
                            }
                        }

                        return array;
                    case JsonValue value:
                        int k = ++counter;
                        if (value.TryGetValue(out bool flag))
                        {
                            return JsonValue.Create(k % 2 == 0 ? flag : !flag);
                        }

                        if (value.TryGetValue(out string? text))
                        {
                            // Values the app parses: zone states arrive as enabled/disabled, health counters as numeric strings.
                            return name == "state" && path.Contains("zone_settings", StringComparison.Ordinal)
                                ? JsonValue.Create(k % 2 == 0 ? "enabled" : "disabled")
                                : text is not ("x" or "fake-text")
                                ? JsonValue.Create(text)
                                : name is "firmware_avg_bitrate" or "video_packets_total"
                                ? JsonValue.Create((100000 + k).ToString(CultureInfo.InvariantCulture))
                                : JsonValue.Create(LooksLikeDate(name)
                                ? new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(k * 7).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture)
                                : $"{tag}-{k}");
                        }

                        if (value.TryGetValue(out long whole))
                        {
                            // Ring sends these switches as 0 and 1.
                            return name is "enable_vod" or "vod_suspended" or "enable_white_leds"
                                ? JsonValue.Create((long)(k % 2))
                                : whole == 1 ? JsonValue.Create(10L + k) : JsonValue.Create(whole);
                        }

                        if (value.TryGetValue(out double real))
                        {
                            // Whole seconds that Ring sends as 41.0 and the app rounds on read.
                            return name is "duration" or "seconds_left"
                                ? JsonValue.Create(10.0 + k)
                                : Math.Abs(real - 1.5) < 1e-9 ? JsonValue.Create(0.05 + (k % 90 / 100.0)) : JsonValue.Create(real);
                        }

                        return value;
                    default:
                        return node;
                }
            }

            return (Walk(root, string.Empty, string.Empty) ?? root).ToJsonString();
        }

        private static bool LooksLikeDate(string name)
        {
            return name.EndsWith("_at", StringComparison.OrdinalIgnoreCase) || name.EndsWith("At", StringComparison.Ordinal)
                || name.Contains("timestamp", StringComparison.OrdinalIgnoreCase) || name.EndsWith("date", StringComparison.OrdinalIgnoreCase)
                || name is "created_at" or "updated_at" or "installationDate" or "createdAt";
        }

        // ------------------------------------------------------------------------------------------------------------
        // Test doubles
        // ------------------------------------------------------------------------------------------------------------

        /// <summary>Answers OAuth and the configured URLs exactly; anything else is a 404. Never reaches the network.</summary>
        private sealed class StubHandler : HttpMessageHandler
        {
            private readonly string _accessToken;
            private readonly List<string> _unrouted = [];

            public StubHandler(string accessToken)
            {
                _accessToken = accessToken;
            }

            public Dictionary<string, string> Routes { get; } = [];

            public IReadOnlyList<string> UnroutedRequests
            {
                get
                {
                    lock (_unrouted)
                    {
                        return [.. _unrouted];
                    }
                }
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string url = request.RequestUri?.GetLeftPart(UriPartial.Path) ?? string.Empty;
                if (url.StartsWith("https://oauth.ring.com/", StringComparison.Ordinal))
                {
                    string token = $"{{\"access_token\":\"{_accessToken}\",\"refresh_token\":\"refresh\",\"expires_in\":3600,\"token_type\":\"Bearer\"}}";
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(token), RequestMessage = request });
                }

                if (!Routes.TryGetValue(url, out string? body))
                {
                    lock (_unrouted)
                    {
                        _unrouted.Add(url);
                    }

                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{\"error\":\"Not found\"}", Encoding.UTF8), RequestMessage = request });
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body), RequestMessage = request });
            }
        }

        /// <summary>Keeps each log entry's level and text. Thread-safe: the sync's fire-and-forget metadata task logs on this instance.</summary>
        private sealed class RecordingLogger : ILogger
        {
            private readonly System.Collections.Concurrent.ConcurrentQueue<(LogLevel Level, string Message)> _entries = new();

            public IReadOnlyList<(LogLevel Level, string Message)> Entries => _entries.ToArray();

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                _entries.Enqueue((logLevel, formatter(state, exception)));
            }
        }
    }
}
