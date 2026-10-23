using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Hosting;
using VideoForensics.Providers.Ring;
using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Services;
using Session = VideoForensics.Providers.Ring.Session;

// Throwaway probe. Writes no tokens/URLs/identifiers to CSV or stdout.
const int Ticks = 15;
TimeSpan period = TimeSpan.FromSeconds(60);

var services = new ServiceCollection();
_ = services.AddLogging(); // no providers: nothing is logged (logs would carry account GUIDs)
string keyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "VideoForensics", "keys");
_ = Directory.CreateDirectory(keyPath);
IDataProtectionBuilder dp = services.AddDataProtection().SetApplicationName("VideoForensics").PersistKeysToFileSystem(new DirectoryInfo(keyPath));
if (OperatingSystem.IsWindows()) { _ = dp.ProtectKeysWithDpapi(); }
_ = services.AddVideoForensicsDataLayer();
_ = services.AddVideoForensicsServerCore("Ring");
using ServiceProvider root = services.BuildServiceProvider();
using IServiceScope scope = root.CreateScope();

// Raw status capture (kind + status only; URL never stored).
var raw = new ConcurrentQueue<(string Kind, int Status)>();
ApiRawLogger.OnRawResponse += c =>
{
    string u = c.Url ?? "";
    string? kind = u.Contains("snapshots/image", StringComparison.Ordinal) ? "image"
        : u.Contains("snapshots/update_all", StringComparison.Ordinal) ? "update"
        : u.Contains("ring_devices", StringComparison.Ordinal) ? "devices" : null;
    if (kind != null) { raw.Enqueue((kind, c.StatusCode)); }
};

CancellationToken ct = CancellationToken.None;
Guid accountId;
try
{
    IProviderAccountRepository accounts = scope.ServiceProvider.GetRequiredService<IProviderAccountRepository>();
    IReadOnlyList<ProviderAccount> list = await accounts.ListActiveAsync(ct);
    ProviderAccount? acct = list.FirstOrDefault(a => string.Equals(a.ProviderName, "Ring", StringComparison.OrdinalIgnoreCase));
    if (acct is null) { Console.WriteLine("RESULT: no linked Ring account. Stopping."); return 3; }
    accountId = acct.Id;
}
catch (Exception ex) { Console.WriteLine($"RESULT: account lookup failed: {ex.GetType().Name}: {Redact(ex.Message)}"); return 4; }

bool restored;
try
{
    IProviderAuthService auth = scope.ServiceProvider.GetRequiredService<IProviderAuthService>();
    restored = await auth.RestoreFromSavedCredentialsAsync(accountId, ct);
}
catch (Exception ex) { Console.WriteLine($"RESULT: restore threw: {ex.GetType().Name}: {Redact(ex.Message)}"); return 5; }
if (!restored) { Console.WriteLine("RESULT: RestoreFromSavedCredentialsAsync returned false for the Ring account. Stopping."); return 6; }
Console.WriteLine("Ring account restored: yes");

Session? session = root.GetRequiredService<ISessionProvider>().GetSession(accountId);
if (session is null) { Console.WriteLine("RESULT: restored but no session available. Stopping."); return 7; }

Devices devices0 = await session.GetRingDevices();
Doorbot? door = devices0.Doorbots?.FirstOrDefault(d => d.Id > 0);
if (door is null) { Console.WriteLine("RESULT: no doorbot found. Stopping."); return 8; }
int doorId = door.Id;
Console.WriteLine($"Device chosen (doorbot id): {doorId}");

string dir = @"C:\ProgramData\VideoForensics\Probes";
_ = Directory.CreateDirectory(dir);
string csvPath = Path.Combine(dir, $"snapshot-probe-{DateTime.UtcNow:yyyyMMddTHHmmssZ}.csv");
using var csv = new StreamWriter(csvPath, false, new UTF8Encoding(false));
csv.WriteLine("tick_index,utc_iso,variant,step,http_status,status_detail,bytes,retry_after_seconds,battery_level,snapshot_image_timestamp,sha256_prefix");

var rows = new List<Row>();
void Emit(Row r)
{
    rows.Add(r);
    csv.WriteLine(string.Join(",", r.Tick, r.Utc.ToString("O"), r.Variant, r.Step, r.Status?.ToString() ?? "", Q(r.Detail), r.Bytes?.ToString() ?? "",
        r.RetryAfter?.ToString(CultureInfo.InvariantCulture) ?? "", r.Battery?.ToString() ?? "", r.ImgTs ?? "", r.Hash ?? ""));
    csv.Flush();
}

DateTime t0 = DateTime.UtcNow;
int? startBattery = null, endBattery = null;
int count429 = 0;

for (int i = 0; i < Ticks; i++)
{
    DateTime due = t0 + i * period;
    TimeSpan wait = due - DateTime.UtcNow;
    if (wait > TimeSpan.Zero) { await Task.Delay(wait); }
    string variant = i % 2 == 0 ? "latest" : "fresh";

    // Battery from device health (GetRingDevices).
    int? battery = null;
    DrainRaw();
    try
    {
        Devices d = await session.GetRingDevices();
        Doorbot? me = d.Doorbots?.FirstOrDefault(x => x.Id == doorId);
        battery = me?.BatteryLife;
        Emit(new Row(i, DateTime.UtcNow, variant, "health", TakeStatus("devices") ?? 200, "ok", null, null, battery, null, null));
    }
    catch (Exception ex) { count429 += Handle(i, variant, "health", ex, battery); }
    if (i == 0) { startBattery = battery; }
    endBattery = battery ?? endBattery;

    if (variant == "fresh")
    {
        DrainRaw();
        bool ok = false;
        try
        {
            await session.UpdateSnapshot(doorId);
            Emit(new Row(i, DateTime.UtcNow, variant, "trigger", TakeStatus("update") ?? 204, "ok", null, null, battery, null, null));
            ok = true;
        }
        catch (Exception ex) { count429 += Handle(i, variant, "trigger", ex, battery); }
        if (!ok) { continue; }
        await Task.Delay(TimeSpan.FromSeconds(5));
    }

    DrainRaw();
    try
    {
        DateTime callUtc = DateTime.UtcNow;
        using Stream s = await session.GetLatestSnapshot(doorId);
        using var ms = new MemoryStream();
        await s.CopyToAsync(ms);
        byte[] b = ms.ToArray();
        int? st = TakeStatus("image");
        string? ts = ExifTs(b);
        string hash = Convert.ToHexString(SHA256.HashData(b))[..12];
        if (st == 429) { count429++; }
        Emit(new Row(i, callUtc, variant, "read", st, st is >= 200 and < 300 ? "ok" : "non-success", b.Length, null, battery, ts, hash));
    }
    catch (Exception ex) { count429 += Handle(i, variant, "read", ex, battery); }
}

Console.WriteLine("---- SUMMARY ----");
foreach (string v in new[] { "latest", "fresh" })
{
    var reads = rows.Where(r => r.Variant == v && r.Step == "read").OrderBy(r => r.Utc).ToList();
    var gaps = reads.Zip(reads.Skip(1), (a, b) => (b.Utc - a.Utc).TotalSeconds).ToList();
    Console.WriteLine($"{v}: ticks={rows.Where(r => r.Variant == v).Select(r => r.Tick).Distinct().Count()} reads={reads.Count} triggers={rows.Count(r => r.Variant == v && r.Step == "trigger")} read_gap_s min={(gaps.Count > 0 ? gaps.Min().ToString("F1") : "n/a")} max={(gaps.Count > 0 ? gaps.Max().ToString("F1") : "n/a")}");
}
Console.WriteLine($"429 count: {count429}");
Console.WriteLine($"distinct http statuses: {string.Join(",", rows.Where(r => r.Status.HasValue).Select(r => r.Status!.Value).Distinct().OrderBy(x => x))}");
Console.WriteLine($"battery start={startBattery?.ToString() ?? "n/a"} end={endBattery?.ToString() ?? "n/a"}");
Console.WriteLine($"image timestamps exposed: {rows.Any(r => !string.IsNullOrEmpty(r.ImgTs))}");
Console.WriteLine($"distinct image hashes: {rows.Where(r => r.Hash != null).Select(r => r.Hash).Distinct().Count()} of {rows.Count(r => r.Hash != null)} reads");
Console.WriteLine($"exceptions: {rows.Count(r => r.Detail.StartsWith("EX:"))}");
Console.WriteLine($"csv: {csvPath}");
return 0;

void DrainRaw() { while (raw.TryDequeue(out _)) { } }
int? TakeStatus(string kind)
{
    int? last = null;
    while (raw.TryDequeue(out var x)) { if (x.Kind == kind) { last = x.Status; } }
    return last;
}

int Handle(int tick, string variant, string step, Exception ex, int? battery)
{
    int? status = null; double? retry = null; bool is429 = false;
    if (ex is VideoForensics.Providers.Ring.Exceptions.ThrottledException te)
    {
        status = 429; is429 = true; retry = te.RetryAfter?.TotalSeconds;
    }
    else if (ex is VideoForensics.Providers.Ring.Exceptions.UnexpectedOutcomeException ue)
    {
        status = (int)ue.ReturnedStatusCode; is429 = status == 429;
    }
    Emit(new Row(tick, DateTime.UtcNow, variant, step, status, "EX:" + ex.GetType().Name + ": " + Redact(ex.Message), null, retry, battery, null, null));
    if (is429)
    {
        double secs = Math.Min(retry ?? 0, 300);
        if (secs > 0) { Task.Delay(TimeSpan.FromSeconds(secs)).GetAwaiter().GetResult(); }
        return 1;
    }
    return 0;
}

static string Redact(string m) => Regex.Replace(m.Replace('\r', ' ').Replace('\n', ' '), @"https?://\S+", "<url>");
static string Q(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
static string? ExifTs(byte[] b)
{
    int n = Math.Min(b.Length, 65536);
    string head = Encoding.Latin1.GetString(b, 0, n);
    Match m = Regex.Match(head, @"\d{4}:\d{2}:\d{2} \d{2}:\d{2}:\d{2}");
    return m.Success ? m.Value : null;
}

internal sealed record Row(int Tick, DateTime Utc, string Variant, string Step, int? Status, string Detail, int? Bytes, double? RetryAfter, int? Battery, string? ImgTs, string? Hash);
