using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.Providers.Ring;
using VideoForensics.Providers.Ring.Services;
using Session = VideoForensics.Providers.Ring.Session;

// Read-only battery probe: 1 restore + 3 x (GetRingDevices + GetDoorbotHealth). No snapshot calls. Never writes tokens/URLs/ids.
const long DoorId = 86401464;
var services = new ServiceCollection();
_ = services.AddLogging();
string keyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "VideoForensics", "keys");
_ = Directory.CreateDirectory(keyPath);
IDataProtectionBuilder dp = services.AddDataProtection().SetApplicationName("VideoForensics").PersistKeysToFileSystem(new DirectoryInfo(keyPath));
if (OperatingSystem.IsWindows()) { _ = dp.ProtectKeysWithDpapi(); }
_ = services.AddVideoForensicsDataLayer();
_ = services.AddVideoForensicsServerCore("Ring");
using ServiceProvider root = services.BuildServiceProvider();
using IServiceScope scope = root.CreateScope();

var raw = new ConcurrentQueue<(string Kind, int Status, string Body)>();
ApiRawLogger.OnRawResponse += c =>
{
    string u = c.Url ?? "";
    string? kind = u.Contains("ring_devices", StringComparison.Ordinal) ? "devices"
        : u.Contains("/health", StringComparison.Ordinal) ? "health" : null;
    if (kind != null) { raw.Enqueue((kind, c.StatusCode, c.Body ?? "")); }
};

CancellationToken ct = CancellationToken.None;
Guid accountId;
try
{
    var accounts = scope.ServiceProvider.GetRequiredService<IProviderAccountRepository>();
    IReadOnlyList<ProviderAccount> list = await accounts.ListActiveAsync(ct);
    ProviderAccount? acct = list.FirstOrDefault(a => string.Equals(a.ProviderName, "Ring", StringComparison.OrdinalIgnoreCase));
    if (acct is null) { Console.WriteLine("RESULT: no linked Ring account."); return 3; }
    accountId = acct.Id;
}
catch (Exception ex) { Console.WriteLine($"RESULT: account lookup failed: {ex.GetType().Name}"); return 4; }

int liveCalls = 0;
try
{
    var auth = scope.ServiceProvider.GetRequiredService<IProviderAuthService>();
    bool ok = await auth.RestoreFromSavedCredentialsAsync(accountId, ct);
    if (!ok) { Console.WriteLine("RESULT: restore returned false. Stopping."); return 6; }
}
catch (Exception ex) { Console.WriteLine($"RESULT: restore threw: {ex.GetType().Name}"); return 5; }
Console.WriteLine("Ring account restored: yes");
Session? session = root.GetRequiredService<ISessionProvider>().GetSession(accountId);
if (session is null) { Console.WriteLine("RESULT: no session."); return 7; }

string dir = @"C:\ProgramData\VideoForensics\Probes";
_ = Directory.CreateDirectory(dir);
string csvPath = Path.Combine(dir, $"battery-probe-{DateTime.UtcNow:yyyyMMddTHHmmssZ}.csv");
using var csv = new StreamWriter(csvPath, false, new UTF8Encoding(false));
csv.WriteLine("round,utc_iso,source,http_status,battery_life,health_battery_percentage,health_battery_percentage_category,health_battery_voltage,health_battery_voltage_category,health_battery_present,health_online,health_wifi_signal,health_exists,raw_health_json_keys,error");

static string V(JsonElement e, string n)
{
    if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(n, out JsonElement p)) { return ""; }
    return p.ValueKind == JsonValueKind.Null ? "null" : p.ToString();
}
static string Keys(JsonElement e) => e.ValueKind == JsonValueKind.Object ? string.Join("|", e.EnumerateObject().Select(x => x.Name)) : "";
string Row(int round, string src, int? status, JsonElement? top, JsonElement health, bool exists, string err)
{
    string bl = top.HasValue ? V(top.Value, "battery_life") : "";
    string online = V(health, "connection_status");
    if (online == "") { online = V(health, "connected"); }
    string wifi = V(health, "wifi_signal_strength");
    if (wifi == "") { wifi = V(health, "latest_signal_strength"); }
    if (wifi == "") { wifi = V(health, "rssi"); }
    string line = string.Join(",", round, DateTime.UtcNow.ToString("O"), src, status?.ToString() ?? "", bl,
        V(health, "battery_percentage"), V(health, "battery_percentage_category"), V(health, "battery_voltage"),
        V(health, "battery_voltage_category"), V(health, "battery_present"), online, wifi, exists, "\"" + Keys(health) + "\"", "\"" + err + "\"");
    csv.WriteLine(line); csv.Flush();
    Console.WriteLine(line);
    return line;
}

for (int round = 1; round <= 3; round++)
{
    if (round > 1) { await Task.Delay(TimeSpan.FromSeconds(60)); }
    while (raw.TryDequeue(out _)) { }
    try
    {
        liveCalls++;
        _ = await session.GetRingDevices();
        var hit = raw.Where(r => r.Kind == "devices").LastOrDefault();
        int? st = hit.Kind == null ? null : hit.Status;
        JsonElement? match = null; JsonElement health = default; bool exists = false;
        if (!string.IsNullOrEmpty(hit.Body))
        {
            using JsonDocument doc = JsonDocument.Parse(hit.Body);
            foreach (JsonProperty arr in doc.RootElement.EnumerateObject())
            {
                if (arr.Value.ValueKind != JsonValueKind.Array) { continue; }
                foreach (JsonElement d in arr.Value.EnumerateArray())
                {
                    if (d.ValueKind == JsonValueKind.Object && d.TryGetProperty("id", out JsonElement id) && id.ValueKind == JsonValueKind.Number && id.GetInt64() == DoorId)
                    {
                        match = d.Clone();
                        if (d.TryGetProperty("health", out JsonElement h) && h.ValueKind == JsonValueKind.Object) { health = h.Clone(); exists = true; }
                    }
                }
            }
        }
        _ = Row(round, "devices", st, match, health, exists, match.HasValue ? "" : "doorbot not matched");
    }
    catch (Exception ex) { _ = Row(round, "devices", null, null, default, false, ex.GetType().Name); }

    while (raw.TryDequeue(out _)) { }
    try
    {
        liveCalls++;
        _ = await session.GetDoorbotHealth(DoorId);
        var hit = raw.Where(r => r.Kind == "health").LastOrDefault();
        JsonElement health = default; JsonElement? top = null; bool exists = false;
        if (!string.IsNullOrEmpty(hit.Body))
        {
            using JsonDocument doc = JsonDocument.Parse(hit.Body);
            top = doc.RootElement.Clone();
            if (doc.RootElement.TryGetProperty("device_health", out JsonElement h) && h.ValueKind == JsonValueKind.Object) { health = h.Clone(); exists = true; }
        }
        _ = Row(round, "doorbot_health", hit.Kind == null ? null : hit.Status, null, health, exists, "");
    }
    catch (Exception ex)
    {
        int? st = ex is VideoForensics.Providers.Ring.Exceptions.ThrottledException ? 429
            : ex is VideoForensics.Providers.Ring.Exceptions.UnexpectedOutcomeException ue ? (int)ue.ReturnedStatusCode : null;
        _ = Row(round, "doorbot_health", st, null, default, false, ex.GetType().Name);
    }
}
Console.WriteLine($"live calls (excl restore): {liveCalls}");
Console.WriteLine($"csv: {csvPath}");
return 0;
