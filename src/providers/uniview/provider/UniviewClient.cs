using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace VideoForensics.Providers.Uniview;

/// <summary>
/// Client for the Sovmiku H5NVR-P-16A web API. See docs/NVR_API.md for the
/// reverse-engineered protocol this implements.
/// </summary>
public sealed class UniviewClient : IDisposable
{
    private const string Realm = "NVRDVR";
    private const string Qop = "auth";
    private const string Nc = "00000001";

    private readonly string _host;
    private readonly string _username;
    private readonly string _password;
    private readonly string _ffmpegPath;
    private readonly HttpClient _http;
    private readonly CookieContainer _cookies = new();

    private string? _nonce;
    private TimeSpan? _cachedClockOffset;
    private DateTime _clockOffsetCachedAtUtc;
    private static readonly TimeSpan ClockOffsetCacheDuration = TimeSpan.FromMinutes(15);

    public UniviewClient(string host, string username, string password, string ffmpegPath = "ffmpeg")
    {
        _host = host;
        _username = username;
        _password = password;
        _ffmpegPath = ffmpegPath;

        var handler = new HttpClientHandler { CookieContainer = _cookies };
        _http = new HttpClient(handler) { BaseAddress = new Uri($"http://{host}") };
        _cookies.Add(_http.BaseAddress!, new Cookie("WebLoginHandle", "10081124"));
    }

    /// <summary>The login nonce, reused as u32UserLoginHandle in legacy cgi-bin calls.</summary>
    public long UserLoginHandle { get; private set; }

    /// <summary>Information about a configured channel on the NVR.</summary>
    public record ChannelInfo(int Index, string Name, bool IsOnline);

    public async Task LoginAsync(CancellationToken ct = default)
    {
        const string path = "/LAPI/V1.0/System/Security/Login";

        using HttpResponseMessage challengeResponse = await _http.PutAsync(path, null, ct);
        string challengeBody = await challengeResponse.Content.ReadAsStringAsync(ct);
        string wwwAuth = challengeResponse.Headers.WwwAuthenticate.ToString();
        if (string.IsNullOrEmpty(wwwAuth))
        {
            throw new InvalidOperationException($"No WWW-Authenticate challenge from {path}. Body: {challengeBody}");
        }

        _nonce = ExtractDigestField(wwwAuth, "nonce")
            ?? throw new InvalidOperationException($"Could not parse nonce from challenge: {wwwAuth}");

        using HttpRequestMessage req = BuildDigestRequest(HttpMethod.Put, path);
        using HttpResponseMessage resp = await _http.SendAsync(req, ct);
        JsonNode? json = await ReadJsonAsync(resp, ct);
        int? statusCode = json?["Response"]?["StatusCode"]?.GetValue<int>();
        if (statusCode != 0)
        {
            throw new InvalidOperationException($"Login failed: {json}");
        }

        UserLoginHandle = long.Parse(_nonce);
    }

    public async Task KeepAliveAsync(CancellationToken ct = default)
    {
        const string path = "/LAPI/V1.0/System/Security/KeepAlive";
        _ = await SendAuthenticatedAsync(HttpMethod.Put, path, body: null, ct);
    }

    /// <summary>
    /// Lists all configured channels on the NVR using legacy cgi-bin cmd 20
    /// (WEB_VMP_MSG_QUERY_VIDEO_CHL_LIST). The response shape is UNVERIFIED
    /// against a live device — guessing at common key names from similar
    /// endpoints (e.g. astChlList, ChannelList, channel index/name/online-status
    /// fields). If those assumptions prove wrong against real hardware, the
    /// response parsing may need adjustment. Wraps per-entry parsing in
    /// try/catch so one malformed channel doesn't fail the entire call.
    /// </summary>
    public async Task<IReadOnlyList<ChannelInfo>> GetChannelListAsync(CancellationToken ct = default)
    {
        var payload = new JsonObject
        {
            ["cmd"] = 20,
            ["szUserName"] = _username,
            ["u32UserLoginHandle"] = UserLoginHandle,
        };

        JsonNode? json = await CallCgiAsync(payload, ct);
        var channels = new List<ChannelInfo>();

        // Try common key names for the channel list array
        JsonArray? channelArray = null;
        foreach (string? key in new[] { "astChlList", "ChannelList", "channels", "chlList" })
        {
            if (json?[key] is JsonArray arr)
            {
                channelArray = arr;
                break;
            }
        }

        if (channelArray == null)
        {
            return channels.AsReadOnly();
        }

        foreach (JsonNode? entry in channelArray)
        {
            try
            {
                // Try common field names for channel index
                int? index = null;
                foreach (string? indexKey in new[] { "u16ChlNo", "ChlNo", "channel", "index", "Index" })
                {
                    if (entry?[indexKey]?.GetValue<int?>() is int idx)
                    {
                        index = idx;
                        break;
                    }
                }

                if (index == null)
                {
                    continue;
                }

                // Try common field names for channel name
                string name = "Unknown";
                foreach (string? nameKey in new[] { "szChlName", "ChlName", "name", "Name" })
                {
                    if (entry?[nameKey]?.GetValue<string>() is string n && !string.IsNullOrEmpty(n))
                    {
                        name = n;
                        break;
                    }
                }

                // Try common field names for online status (assume 0=offline, non-0=online)
                bool isOnline = false;
                foreach (string? statusKey in new[] { "u8OnlineStatus", "OnlineStatus", "status", "Status", "online" })
                {
                    if (entry?[statusKey]?.GetValue<int?>() is int status)
                    {
                        isOnline = status != 0;
                        break;
                    }
                }

                channels.Add(new ChannelInfo(index.Value, name, isOnline));
            }
            catch
            {
                // Skip malformed entries; log only if needed per caller
            }
        }

        return channels.AsReadOnly();
    }

    /// <summary>
    /// Returns true if the given channel (1-based) has any recording on any
    /// day of the given year/month. Cheap per-channel probe — see
    /// docs/NVR_API.md section 5.
    /// </summary>
    public async Task<bool> ChannelHasRecordingsAsync(int channel, int year, int month, CancellationToken ct = default)
    {
        int[] days = await GetDailyDistributionAsync(channel, year, month, ct);
        return Array.Exists(days, d => d != 0);
    }

    /// <summary>Per-day recording presence (1/0) for one channel/month.</summary>
    public async Task<int[]> GetDailyDistributionAsync(int channel, int year, int month, CancellationToken ct = default)
    {
        string path = $"/LAPI/V1.0/Channels/{channel}/Media/Video/Streams/0/Records/DailyDistribution?Year={year}&Month={month}";
        using HttpResponseMessage resp = await SendAuthenticatedAsync(HttpMethod.Get, path, body: null, ct);
        JsonNode? json = await ReadJsonAsync(resp, ct);
        int? statusCode = json?["Response"]?["StatusCode"]?.GetValue<int>();
        if (statusCode != 0)
        {
            throw new InvalidOperationException($"DailyDistribution failed for channel {channel}: {json}");
        }

        JsonArray dailyStatus = json!["Response"]!["Data"]!["DailyStatus"]!.AsArray();
        int[] result = new int[dailyStatus.Count];
        for (int i = 0; i < dailyStatus.Count; i++)
        {
            result[i] = dailyStatus[i]!.GetValue<int>();
        }

        return result;
    }

    /// <summary>
    /// Lists recorded segments for one channel between two UTC instants,
    /// using the legacy cgi-bin cmd 78 (WEB_VMP_MSG_VOD_QRY_V2) call.
    /// </summary>
    public async Task<IReadOnlyList<RecordSegment>> ListSegmentsAsync(
        int channel, DateTimeOffset begin, DateTimeOffset end, CancellationToken ct = default)
    {
        string resourceCode = ResourceCode(channel);
        var payload = new JsonObject
        {
            ["cmd"] = 78,
            ["astResourceCode"] = new JsonArray(resourceCode),
            ["focusCamCode"] = resourceCode,
            ["u32RecordType"] = 0,
            ["u32StoretypeCount"] = 0,
            ["u32Task_No"] = Random.Shared.Next(1, int.MaxValue),
            ["u32Begin"] = begin.ToUnixTimeSeconds(),
            ["u32End"] = end.ToUnixTimeSeconds(),
            ["u32StorStream"] = 0,
            ["szUserName"] = _username,
            ["u32UserLoginHandle"] = UserLoginHandle,
        };

        JsonNode? json = await CallCgiAsync(payload, ct);
        JsonArray recordList = json?["recordList"]?.AsArray() ?? [];

        var segments = new List<RecordSegment>(recordList.Count);
        foreach (JsonNode? entry in recordList)
        {
            segments.Add(new RecordSegment(
                Channel: channel,
                Begin: DateTimeOffset.FromUnixTimeSeconds(entry!["u32Begin"]!.GetValue<long>()),
                End: DateTimeOffset.FromUnixTimeSeconds(entry["u32End"]!.GetValue<long>()),
                RecordType: entry["u32RecordType"]!.GetValue<int>()));
        }

        return segments;
    }

    /// <summary>
    /// Generic LAPI REST GET - returns the "Data" object from the standard
    /// {Response:{StatusCode,Data}} envelope every LAPI endpoint uses. See
    /// docs/NVR_API.md section 11 for the settings endpoints this backs.
    /// </summary>
    public async Task<JsonNode?> GetLapiAsync(string path, CancellationToken ct = default)
    {
        using HttpResponseMessage resp = await SendAuthenticatedAsync(HttpMethod.Get, path, body: null, ct);
        JsonNode? json = await ReadJsonAsync(resp, ct);
        int? statusCode = json?["Response"]?["StatusCode"]?.GetValue<int>();
        return statusCode != 0 ? throw new InvalidOperationException($"LAPI GET {path} failed: {json}") : json!["Response"]!["Data"];
    }

    /// <summary>
    /// Generic LAPI REST PUT - the body is the same shape as the "Data"
    /// object the matching GET returns, sent as raw JSON (not urlencoded,
    /// not "json="-wrapped) terminated with "\r\n". Confirmed by capture
    /// against /LAPI/V1.0/System/TimeNTP - the real web UI's own $.ajax
    /// call sets no explicit contentType, so jQuery labels the request
    /// x-www-form-urlencoded while the body is actually raw JSON; this
    /// matches BuildDigestRequest's existing StringContent call, so no
    /// header changes were needed here. See docs/NVR_API.md section 11.
    /// </summary>
    public async Task SetLapiAsync(string path, JsonObject data, CancellationToken ct = default)
    {
        string body = data.ToJsonString() + "\r\n";
        using HttpResponseMessage resp = await SendAuthenticatedAsync(HttpMethod.Put, path, body, ct);
        JsonNode? json = await ReadJsonAsync(resp, ct);
        int? statusCode = json?["Response"]?["StatusCode"]?.GetValue<int>();
        if (statusCode != 0)
        {
            throw new InvalidOperationException($"LAPI PUT {path} failed: {json}");
        }
    }

    /// <summary>Whether the given channel's camera supports PTZ - check before calling PtzCtrlAsync (most fixed cameras will report false and reject any PTZ command with StatusCode 60006).</summary>
    public async Task<bool> SupportsPtzAsync(int channel, CancellationToken ct = default)
    {
        JsonNode? caps = await GetLapiAsync($"/LAPI/V1.0/Channels/{channel}/PTZ/Capabilities", ct);
        return caps?["IsSupportPTZ"]?.GetValue<int>() != 0;
    }

    /// <summary>
    /// Sends a PTZ command. This actually moves the physical camera (or
    /// operates its iris/focus/zoom/light/etc.) - it is not reversible by
    /// re-reading state, only by sending an opposing move. Move commands
    /// (PanLeft, TiltUp, ...) start continuous motion and do NOT
    /// self-stop - the caller must send the matching *Stop command (or
    /// PtzCommand.Stop) shortly after, the same way the real web UI does
    /// on mouseup. Confirmed by live capture + test against a PTZ-capable
    /// camera on this device: PUT body is {"PTZCmd":&lt;numeric code&gt;,"Para1":&lt;speed|0&gt;,"Para2":&lt;speed|0&gt;}
    /// - NOT the {Command:"..."} shape you'd guess from the LAPI REST
    /// convention elsewhere. Reverse-engineered from the web UI's own
    /// minified live-view JS (Static.PtzCmd enum + the Para1/Para2 zeroing
    /// rules per axis), since PTZCtrl's request shape isn't inferable from
    /// its GET-side response (there isn't one - this is a pure action
    /// endpoint). See docs/NVR_API.md section 11.
    /// </summary>
    public Task PtzCtrlAsync(int channel, PtzCommand command, int speed = 30, CancellationToken ct = default)
    {
        // Mirrors the web UI's ctrlPtz(): pure pan (Left/Right) zeroes
        // Para2, pure tilt (Up/Down) zeroes Para1, everything else
        // (diagonals, zoom, focus, iris, light, ...) uses speed for both.
        bool isPan = command is PtzCommand.PanLeft or PtzCommand.PanRight
            or PtzCommand.PanLeftStop or PtzCommand.PanRightStop;
        bool isTilt = command is PtzCommand.TiltUp or PtzCommand.TiltDown
            or PtzCommand.TiltUpStop or PtzCommand.TiltDownStop;
        int para1 = isTilt ? 0 : speed;
        int para2 = isPan ? 0 : speed;

        var payload = new JsonObject
        {
            ["PTZCmd"] = (int)command,
            ["Para1"] = para1,
            ["Para2"] = para2,
        };
        return SetLapiAsync($"/LAPI/V1.0/Channels/{channel}/PTZ/PTZCtrl", payload, ct);
    }

    /// <summary>Per-channel video encode settings (resolution/bitrate/fps/codec, main+sub streams).</summary>
    public Task<JsonNode?> GetChannelVideoAsync(int channel, CancellationToken ct = default)
    {
        return GetLapiAsync($"/LAPI/V1.0/Channels/{channel}/Media/Video", ct);
    }

    /// <summary>Set per-channel video encode settings - pass the full object back from GetChannelVideoAsync with your edits applied.</summary>
    public Task SetChannelVideoAsync(int channel, JsonObject data, CancellationToken ct = default)
    {
        return SetLapiAsync($"/LAPI/V1.0/Channels/{channel}/Media/Video", data, ct);
    }

    /// <summary>Per-channel on-screen-display text overlays (camera name, date/time, custom text).</summary>
    public Task<JsonNode?> GetChannelOsdAsync(int channel, CancellationToken ct = default)
    {
        return GetLapiAsync($"/LAPI/V1.0/Channels/{channel}/Media/OSDs", ct);
    }

    public Task SetChannelOsdAsync(int channel, JsonObject data, CancellationToken ct = default)
    {
        return SetLapiAsync($"/LAPI/V1.0/Channels/{channel}/Media/OSDs", data, ct);
    }

    /// <summary>Per-channel motion detection: enable, grid/sensitivity, week schedule, linkage actions.</summary>
    public Task<JsonNode?> GetMotionDetectionAsync(int channel, CancellationToken ct = default)
    {
        return GetLapiAsync($"/LAPI/V1.0/Channels/{channel}/Alarm/MotionDetection", ct);
    }

    public Task SetMotionDetectionAsync(int channel, JsonObject data, CancellationToken ct = default)
    {
        return SetLapiAsync($"/LAPI/V1.0/Channels/{channel}/Alarm/MotionDetection", data, ct);
    }

    /// <summary>Per-channel recording schedule: enabled, pre/post-record seconds, per-weekday time sections.</summary>
    public Task<JsonNode?> GetRecordScheduleAsync(int channel, CancellationToken ct = default)
    {
        return GetLapiAsync($"/LAPI/V1.0/Channels/{channel}/Storage/Private/Schedule/Record/", ct);
    }

    public Task SetRecordScheduleAsync(int channel, JsonObject data, CancellationToken ct = default)
    {
        return SetLapiAsync($"/LAPI/V1.0/Channels/{channel}/Storage/Private/Schedule/Record/", data, ct);
    }

    /// <summary>Device time zone/date-format/NTP settings.</summary>
    public Task<JsonNode?> GetTimeNtpAsync(CancellationToken ct = default)
    {
        return GetLapiAsync("/LAPI/V1.0/System/TimeNTP", ct);
    }

    public Task SetTimeNtpAsync(JsonObject data, CancellationToken ct = default)
    {
        return SetLapiAsync("/LAPI/V1.0/System/TimeNTP", data, ct);
    }

    /// <summary>
    /// Returns the delta to add to the device's raw recording-index timestamps
    /// (segment.Begin/End from ListSegmentsAsync) to correct them to this
    /// machine's real UTC clock. The recording index is always raw UTC
    /// regardless of the device's configured TimeZone (see docs/NVR_API.md
    /// section 6), but the device's own clock can still drift from true UTC
    /// if its NTP sync is stale or disabled. Computed by comparing the
    /// device's NTP-synced clock (/LAPI/V1.0/System/TimeNTP's DeviceTime)
    /// against DateTimeOffset.UtcNow at the moment of the call. Cached for
    /// 15 minutes since drift changes slowly and this avoids an extra round
    /// trip per download/query. Apply this offset only when persisting or
    /// displaying a timestamp - never to the raw values used to query or
    /// download from the device itself.
    /// </summary>
    public async Task<TimeSpan> GetClockOffsetAsync(CancellationToken ct = default)
    {
        if (_cachedClockOffset is { } cached && DateTime.UtcNow - _clockOffsetCachedAtUtc < ClockOffsetCacheDuration)
        {
            return cached;
        }

        DateTimeOffset requestedAt = DateTimeOffset.UtcNow;
        JsonNode? data = await GetTimeNtpAsync(ct);
        long deviceTimeSeconds = data?["DeviceTime"]?.GetValue<long>()
            ?? throw new InvalidOperationException($"TimeNTP response missing DeviceTime: {data}");
        var deviceTime = DateTimeOffset.FromUnixTimeSeconds(deviceTimeSeconds);

        TimeSpan offset = requestedAt - deviceTime;
        _cachedClockOffset = offset;
        _clockOffsetCachedAtUtc = DateTime.UtcNow;
        return offset;
    }

    /// <summary>Daylight saving time rule (begin/end month-week-day-hour, bias in minutes).</summary>
    public Task<JsonNode?> GetDstAsync(CancellationToken ct = default)
    {
        return GetLapiAsync("/LAPI/V1.0/System/Time/DST", ct);
    }

    public Task SetDstAsync(JsonObject data, CancellationToken ct = default)
    {
        return SetLapiAsync("/LAPI/V1.0/System/Time/DST", data, ct);
    }

    /// <summary>SSH (labeled "SSH" in the UI, endpoint is still named for the older Telnet feature it replaced) enable/disable.</summary>
    public Task<JsonNode?> GetSshAsync(CancellationToken ct = default)
    {
        return GetLapiAsync("/LAPI/V1.0/Network/SSH", ct);
    }

    public Task SetSshAsync(bool enabled, CancellationToken ct = default)
    {
        return SetLapiAsync("/LAPI/V1.0/Network/SSH", new JsonObject { ["Enabled"] = enabled ? 1 : 0 }, ct);
    }

    /// <summary>HTTP/HTTPS/RTSP (and their redirect) listen ports.</summary>
    public Task<JsonNode?> GetNetworkPortsAsync(CancellationToken ct = default)
    {
        return GetLapiAsync("/LAPI/V1.0/Network/Ports", ct);
    }

    public Task SetNetworkPortsAsync(JsonObject data, CancellationToken ct = default)
    {
        return SetLapiAsync("/LAPI/V1.0/Network/Ports", data, ct);
    }

    /// <summary>DDNS provider list/config (DynDNS, No-IP, Uniview's own star4live).</summary>
    public Task<JsonNode?> GetDdnsAsync(CancellationToken ct = default)
    {
        return GetLapiAsync("/LAPI/V1.0/Network/DDNS", ct);
    }

    public Task SetDdnsAsync(JsonObject data, CancellationToken ct = default)
    {
        return SetLapiAsync("/LAPI/V1.0/Network/DDNS", data, ct);
    }

    /// <summary>Device identity/model/firmware/serial - see also DeviceInfo's cousin, the legacy cmd 26 in GetChannelStatusAsync.</summary>
    public Task<JsonNode?> GetDeviceInfoAsync(CancellationToken ct = default)
    {
        return GetLapiAsync("/LAPI/V1.0/System/DeviceInfo", ct);
    }

    /// <summary>
    /// The NVR's own host network (NIC) config - IP/DHCP/subnet/gateway/DNS/MTU.
    /// This is the one settings area still on the legacy cgi-bin JSON-RPC
    /// endpoint (cmd 29), not LAPI REST - confirmed by live capture against
    /// the "TCP/IP" settings page. Read-only here: the corresponding "set"
    /// cmd number was deliberately not captured (would have required
    /// actually changing this device's own IP/network config to observe the
    /// request, which risks losing connectivity to it - see docs/NVR_API.md
    /// section 11).
    /// </summary>
    public Task<JsonNode?> GetTcpIpAsync(CancellationToken ct = default)
    {
        var payload = new JsonObject
        {
            ["cmd"] = 29,
            ["bFour"] = false,
            ["szUserName"] = _username,
            ["u32UserLoginHandle"] = UserLoginHandle,
        };
        return CallCgiAsync(payload, ct);
    }

    /// <summary>
    /// IP address filtering (off/blocklist/allowlist) - legacy cgi-bin cmd
    /// 146. Read-only here; the "set" cmd wasn't captured (changing this
    /// live risks locking out the very connection used to test it).
    /// </summary>
    public Task<JsonNode?> GetIpControlAsync(CancellationToken ct = default)
    {
        var payload = new JsonObject
        {
            ["cmd"] = 146,
            ["szUserName"] = _username,
            ["u32UserLoginHandle"] = UserLoginHandle,
        };
        return CallCgiAsync(payload, ct);
    }

    /// <summary>ONVIF authentication requirement (on/off) - legacy cgi-bin cmd 920. Read-only here; "set" cmd not captured.</summary>
    public Task<JsonNode?> GetOnvifAuthAsync(CancellationToken ct = default)
    {
        var payload = new JsonObject
        {
            ["cmd"] = 920,
            ["szUserName"] = _username,
            ["u32UserLoginHandle"] = UserLoginHandle,
        };
        return CallCgiAsync(payload, ct);
    }

    /// <summary>Configured holiday date ranges (used by schedules that distinguish holiday/weekday/weekend) - legacy cgi-bin cmd 656. Read-only here; "set"/"add" cmd not captured.</summary>
    public Task<JsonNode?> GetHolidaysAsync(CancellationToken ct = default)
    {
        var payload = new JsonObject
        {
            ["cmd"] = 656,
            ["szUserName"] = _username,
            ["u32UserLoginHandle"] = UserLoginHandle,
        };
        return CallCgiAsync(payload, ct);
    }

    /// <summary>User accounts (username + role: 0=Administrator, 1=Operator, 3=Local Preview User on this device) - legacy cgi-bin cmd 200. Read-only here; add/modify/delete cmd numbers not captured (deliberately not tested against real accounts on this device).</summary>
    public Task<JsonNode?> GetUsersAsync(CancellationToken ct = default)
    {
        var payload = new JsonObject
        {
            ["cmd"] = 200,
            ["szUserName"] = _username,
            ["u32UserLoginHandle"] = UserLoginHandle,
        };
        return CallCgiAsync(payload, ct);
    }

    /// <summary>Exception/alert types this device supports (IP conflict, disk full, etc.) - the "Alert Type" dropdown values on the Setup &gt; Alert page.</summary>
    public Task<JsonNode?> GetAlarmExceptionCapabilitiesAsync(CancellationToken ct = default)
    {
        return GetLapiAsync("/LAPI/V1.0/Alarm/Exceptions/Capabilities", ct);
    }

    /// <summary>
    /// Linkage actions (buzzer/email/push/alarm-output) configured for one exception type.
    /// <paramref name="exceptionType"/> must be one of the values from
    /// GetAlarmExceptionCapabilitiesAsync's SupportType list (e.g. 204 = IP
    /// Conflict on this device) - calling without a valid type returns
    /// StatusCode 60006 (Invalid Arguments).
    /// </summary>
    public Task<JsonNode?> GetAlarmExceptionLinkageActionsAsync(int exceptionType, CancellationToken ct = default)
    {
        return GetLapiAsync($"/LAPI/V1.0/Alarm/Exceptions/LinkageActions?ExceptionType={exceptionType}", ct);
    }

    public Task SetAlarmExceptionLinkageActionsAsync(int exceptionType, JsonObject data, CancellationToken ct = default)
    {
        return SetLapiAsync($"/LAPI/V1.0/Alarm/Exceptions/LinkageActions?ExceptionType={exceptionType}", data, ct);
    }

    /// <summary>
    /// Captures a single JPEG frame from the channel's live feed via
    /// ffmpeg + RTSP. There is no device-side "take a snapshot" API - the
    /// web UI's own snapshot button only grabs a frame from the browser's
    /// already-decoded video locally, and neither the LAPI map nor a
    /// handful of guessed conventional snapshot-URL paths turned up a real
    /// endpoint (confirmed by comparing against a deliberately bogus path
    /// - both returned the identical generic error). RTSP live view is
    /// confirmed working on this device (see docs/NVR_API.md section 6)
    /// even though RTSP historical/time-ranged playback is not - this
    /// pulls whatever is live right now, not a specific past moment. See
    /// docs/NVR_API.md section 11.
    /// </summary>
    public Task CaptureLiveSnapshotAsync(int channel, string destinationJpegPath, CancellationToken ct = default)
    {
        string rtspUrl = $"rtsp://{Uri.EscapeDataString(_username)}:{Uri.EscapeDataString(_password)}@{_host}:554/unicast/c{channel}/s0/live";
        return RunFfmpegAsync(
        [
            "-y",
            "-rtsp_transport", "tcp",
            // Bounds the RTSP connect/read attempt (microseconds) - channels
            // on this device flap online/offline fairly often (see the
            // camera-naming/online-status caveats in docs section 10-11),
            // and without this ffmpeg would otherwise hang indefinitely
            // trying to connect to an offline channel. "-stimeout" is the
            // older option name; newer ffmpeg builds (9.x+) renamed it to
            // "-timeout" and reject the old spelling outright rather than
            // just warning, so this uses the current name.
            "-timeout", "10000000",
            "-i", rtspUrl,
            "-frames:v", "1",
            destinationJpegPath,
        ], "live snapshot", ct);
    }

    /// <summary>
    /// Extracts a single JPEG frame from an already-downloaded video file
    /// (e.g. DownloadSegmentAsync's output), at <paramref name="at"/> if
    /// given, otherwise the first frame.
    /// </summary>
    public Task CaptureFrameFromFileAsync(string videoPath, string destinationJpegPath, TimeSpan? at = null, CancellationToken ct = default)
    {
        var args = new List<string> { "-y" };
        if (at is { } offset)
        {
            args.Add("-ss");
            args.Add(offset.ToString(@"hh\:mm\:ss\.fff"));
        }

        args.Add("-i");
        args.Add(videoPath);
        args.Add("-frames:v");
        args.Add("1");
        args.Add(destinationJpegPath);
        return RunFfmpegAsync(args, "frame capture", ct);
    }

    /// <summary>
    /// Downloads a recorded segment. Confirmed by packet capture: cmd 82
    /// starts a session and the server hands back its own address/port plus
    /// a session id; the actual media then arrives over a plain TCP
    /// connection using RTSP-style '$' interleaved framing around ordinary
    /// unencrypted RTP/HEVC (the RSA key exchange in the request goes
    /// unused for this transport). See docs/NVR_API.md section 6.
    /// </summary>
    public async Task DownloadSegmentAsync(RecordSegment segment, string destinationPath, CancellationToken ct = default)
    {
        // Empirically, cmd 82 sometimes accepts a session (code:0, valid
        // sessionId) but the server never sends any RTP data - a transient
        // server-side hiccup, not a protocol error (retrying with a fresh
        // cmd 82 call reliably succeeds). See docs/NVR_API.md section 6.
        const int maxAttempts = 3;
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                await DownloadSegmentAttemptAsync(segment, destinationPath, ct);
                return;
            }
            catch (DownloadCapacityException)
            {
                throw; // persistent device-side exhaustion - retrying won't help
            }
            catch (InvalidOperationException) when (attempt < maxAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }
        }
    }

    private async Task DownloadSegmentAttemptAsync(RecordSegment segment, string destinationPath, CancellationToken ct)
    {
        string resourceCode = ResourceCode(segment.Channel);
        using var rsa = RSA.Create(1024);
        RSAParameters rsaParams = rsa.ExportParameters(false);
        string localIp = GetLocalAddressFor(_host);

        var startPayload = new JsonObject
        {
            ["cmd"] = 82,
            ["stResourceCode"] = resourceCode,
            ["u8RecvSendFlag"] = 2,
            ["u16Port"] = 33598,
            ["stIPAddress"] = localIp,
            ["szRecordFileName"] = "",
            ["u32RecordType"] = segment.RecordType,
            ["u32Begin"] = segment.Begin.ToUnixTimeSeconds(),
            ["u32End"] = segment.End.ToUnixTimeSeconds(),
            ["bHasEncodeType"] = true,
            ["u8EncodeType"] = 4,
            ["RSAPubKey"] = 65537,
            ["RSAPubKeyN"] = Convert.ToHexStringLower(rsaParams.Modulus!).ToUpperInvariant(),
            ["szUserName"] = _username,
            ["u32UserLoginHandle"] = UserLoginHandle,
        };

        JsonNode startResponse = await CallCgiAsync(startPayload, ct)
            ?? throw new InvalidOperationException("cmd 82 (start download) returned no response.");
        int? startCode = startResponse["code"]?.GetValue<int>();
        if (startCode == 60031)
        {
            throw new DownloadCapacityException(
                "cmd 82 rejected with code 60031 - the device has hit its concurrent download " +
                "task limit, most likely from earlier sessions that were killed abruptly and never " +
                "sent cmd 84 to release their slot. Wait a while for stale sessions to expire " +
                "server-side, or reboot the NVR to clear it immediately. See docs/NVR_API.md section 6.");
        }

        if (startCode != 0)
        {
            throw new InvalidOperationException($"cmd 82 (start download) failed: {startResponse}");
        }

        string sessionId = startResponse["szSessionID"]!.GetValue<string>();
        string dataHost = startResponse["stIPAddress"]!.GetValue<string>();
        int dataPort = startResponse["u16Port"]!.GetValue<int>();
        uint taskNo = startResponse["u32Task_No"]!.GetValue<uint>();

        // Observed on the wire: cmd 83 fires once immediately after cmd 82,
        // before any media flows — it appears to be a required "start
        // playing" signal, not just a periodic heartbeat.
        _ = await SendPlayStatusAsync(taskNo, segment.Begin, ct);

        using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Task heartbeatTask = RunHeartbeatAsync(taskNo, segment.Begin, heartbeatCts.Token);

        bool closeSent = false;
        async Task SendCloseOnceAsync()
        {
            if (closeSent)
            {
                return;
            }

            closeSent = true;
            var closePayload = new JsonObject
            {
                ["cmd"] = 84,
                ["u32Task_No"] = taskNo,
                ["szUserName"] = _username,
                ["u32UserLoginHandle"] = UserLoginHandle,
            };
            _ = await CallCgiAsync(closePayload, ct);
        }

        string elementaryStreamPath = destinationPath + ".h265";
        string audioStreamPath = destinationPath + ".g711";
        try
        {
            TimeSpan targetDuration = segment.End - segment.Begin;
            var idleTimeout = TimeSpan.FromSeconds(20); // data arrives in a fast burst, not real-time
            // cmd 84 fires from inside ReceiveAsync, while the TCP data
            // connection is still open - see the ordering note there.
            await RtpDownloadSession.ReceiveAsync(dataHost, dataPort, sessionId, elementaryStreamPath, audioStreamPath, targetDuration, idleTimeout, SendCloseOnceAsync, ct);
        }
        finally
        {
            heartbeatCts.Cancel();
            try
            { await heartbeatTask; }
            catch (OperationCanceledException) { }

            // Fallback in case ReceiveAsync threw before reaching its own
            // onStopping call (e.g. a network error) - SendCloseOnceAsync
            // is idempotent so this is a no-op if cmd 84 already fired.
            await SendCloseOnceAsync();
        }

        if (new FileInfo(elementaryStreamPath).Length == 0)
        {
            throw new InvalidOperationException(
                "Received no video data for this segment - the server accepted the download " +
                "session but never sent any RTP packets. Empirically this happens when the same " +
                "resourceCode+time-range combination has been downloaded very recently; retrying " +
                "with a different segment (or waiting) usually succeeds. See docs/NVR_API.md section 6.");
        }

        TimeSpan clockOffset = await GetClockOffsetAsync(ct);
        DateTimeOffset correctedBegin = segment.Begin + clockOffset;

        bool hasAudio = new FileInfo(audioStreamPath).Length > 0;
        await MuxToMp4Async(elementaryStreamPath, hasAudio ? audioStreamPath : null, destinationPath, correctedBegin, ct);
        File.Delete(elementaryStreamPath);
        File.Delete(audioStreamPath);

        // segment.Begin uses the device's own raw recording-index digits
        // (always UTC regardless of the device's TimeZone setting - see
        // docs/NVR_API.md section 6), corrected here by the device's
        // NTP-clock-vs-our-clock delta (GetClockOffsetAsync) so the file's
        // OS timestamps and embedded creation_time reflect the actual
        // real-world recording time rather than raw device-clock drift.
        var recordedAt = DateTime.SpecifyKind(correctedBegin.DateTime, DateTimeKind.Local);
        File.SetCreationTime(destinationPath, recordedAt);
        File.SetLastWriteTime(destinationPath, recordedAt);
    }

    private async Task RunHeartbeatAsync(uint taskNo, DateTimeOffset playTime, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(15), ct);
                _ = await SendPlayStatusAsync(taskNo, playTime, ct);
            }
        }
        catch (OperationCanceledException) { }
    }

    private Task<JsonNode?> SendPlayStatusAsync(uint taskNo, DateTimeOffset playTime, CancellationToken ct)
    {
        var payload = new JsonObject
        {
            ["cmd"] = 83,
            ["u32Task_No"] = taskNo,
            ["u32PlayTime"] = playTime.ToUnixTimeSeconds(),
            ["u32PlayStatus"] = 54,
            ["szUserName"] = _username,
            ["u32UserLoginHandle"] = UserLoginHandle,
        };
        return CallCgiAsync(payload, ct);
    }

    private async Task MuxToMp4Async(string elementaryStreamPath, string? audioStreamPath, string destinationPath, DateTimeOffset recordedAt, CancellationToken ct)
    {
        var psi = new System.Diagnostics.ProcessStartInfo(_ffmpegPath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("-y");
        // The raw elementary stream and audio inputs carry no usable PTS on
        // their own (see the -shortest note below) - genpts derives clean,
        // monotonic presentation timestamps from decode order instead of
        // leaving them unset, which otherwise triggers "Timestamps are
        // unset in a packet" warnings and can confuse players' seeking.
        psi.ArgumentList.Add("-fflags");
        psi.ArgumentList.Add("+genpts");
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add("hevc");
        // Annex-B elementary streams carry no real timing; the RTP stream
        // runs at a fixed 25fps (confirmed via RTP timestamp deltas of
        // 3600 ticks at the 90kHz clock), so force it explicitly rather
        // than let ffmpeg guess and mis-report the duration.
        psi.ArgumentList.Add("-r");
        psi.ArgumentList.Add("25");
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(elementaryStreamPath);

        if (audioStreamPath is not null)
        {
            // Raw, headerless G.711 mu-law samples extracted from RTP
            // payload type 0 packets (see docs/NVR_API.md section 6) -
            // 8kHz mono, same as the device's live RTSP audio. Re-encoded
            // to AAC since raw mu-law isn't a standard MP4 audio codec.
            psi.ArgumentList.Add("-f");
            psi.ArgumentList.Add("mulaw");
            psi.ArgumentList.Add("-ar");
            psi.ArgumentList.Add("8000");
            psi.ArgumentList.Add("-ac");
            psi.ArgumentList.Add("1");
            psi.ArgumentList.Add("-i");
            psi.ArgumentList.Add(audioStreamPath);
            psi.ArgumentList.Add("-c:v");
            psi.ArgumentList.Add("copy");
            psi.ArgumentList.Add("-c:a");
            psi.ArgumentList.Add("aac");
            // No -shortest: the raw HEVC elementary stream carries no PTS
            // (it's Annex-B with no container), so -shortest's duration
            // tracking sees it as already exhausted and truncates the
            // output to a fraction of a second, dropping audio entirely.
            // Both streams already cover the same requested time range, so
            // there's nothing for -shortest to trim in practice.
        }
        else
        {
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add("copy");
        }

        // Embed the actual recording time (device wall-clock convention,
        // same digits as the filename - see docs/NVR_API.md section 6)
        // rather than leaving ffmpeg to default to "now". Players that read
        // this atom (e.g. QuickTime) treat it as UTC; since it's really the
        // device's naive-local time, displayed values may be shifted by
        // whatever timezone the player assumes - same caveat as the
        // filename/OS-timestamp convention, not something ffmpeg can fix.
        psi.ArgumentList.Add("-metadata");
        psi.ArgumentList.Add($"creation_time={recordedAt:yyyy-MM-ddTHH:mm:ssZ}");
        // moov atom at the front, not the end - without this, downloaded
        // files won't start playing or seek until fully loaded in many
        // players/scrubbers, which otherwise reads as a "corrupt" download.
        psi.ArgumentList.Add("-movflags");
        psi.ArgumentList.Add("+faststart");
        psi.ArgumentList.Add(destinationPath);

        using Process process = System.Diagnostics.Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start ffmpeg.");
        Task<string> stderrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"ffmpeg remux failed (exit {process.ExitCode}): {await stderrTask}");
        }
    }

    private async Task RunFfmpegAsync(List<string> args, string label, CancellationToken ct)
    {
        var psi = new System.Diagnostics.ProcessStartInfo(_ffmpegPath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        using Process process = System.Diagnostics.Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start ffmpeg.");
        Task<string> stderrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"ffmpeg {label} failed (exit {process.ExitCode}): {await stderrTask}");
        }
    }

    private static string GetLocalAddressFor(string remoteHost)
    {
        using var socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Dgram, System.Net.Sockets.ProtocolType.Udp);
        socket.Connect(remoteHost, 80);
        return ((IPEndPoint)socket.LocalEndPoint!).Address.ToString();
    }

    private async Task<JsonNode?> CallCgiAsync(JsonObject payload, CancellationToken ct)
    {
        string body = "json=" + payload.ToJsonString();
        using HttpResponseMessage resp = await SendAuthenticatedAsync(HttpMethod.Post, "/cgi-bin/main-cgi", body, ct);
        return await ReadJsonAsync(resp, ct);
    }

    private async Task<HttpResponseMessage> SendAuthenticatedAsync(
        HttpMethod method, string pathAndQuery, string? body, CancellationToken ct)
    {
        HttpResponseMessage response = await SendOnceAsync(method, pathAndQuery, body, ct);

        // A stale nonce comes back as HTTP 200 with Response.StatusCode==401
        // in the JSON body, plus a fresh WWW-Authenticate header. Refresh and retry once.
        if (response.Headers.WwwAuthenticate.Count > 0)
        {
            JsonNode? probe = await ReadJsonAsync(response, ct, leaveOpen: true);
            int? statusCode = probe?["Response"]?["StatusCode"]?.GetValue<int>();
            if (statusCode == 401)
            {
                string wwwAuth = response.Headers.WwwAuthenticate.ToString();
                _nonce = ExtractDigestField(wwwAuth, "nonce")
                    ?? throw new InvalidOperationException($"Could not parse renewed nonce: {wwwAuth}");
                response.Dispose();
                response = await SendOnceAsync(method, pathAndQuery, body, ct);
            }
        }

        return response;
    }

    private async Task<HttpResponseMessage> SendOnceAsync(HttpMethod method, string pathAndQuery, string? body, CancellationToken ct)
    {
        using HttpRequestMessage req = BuildDigestRequest(method, pathAndQuery, body);
        return await _http.SendAsync(req, ct);
    }

    private HttpRequestMessage BuildDigestRequest(HttpMethod method, string pathAndQuery, string? body = null)
    {
        if (_nonce is null)
        {
            throw new InvalidOperationException("Not logged in yet.");
        }

        string path = pathAndQuery.Split('?')[0];
        string cnonce = Random.Shared.Next(0, int.MaxValue).ToString();

        string ha1 = Md5Hex($"{_username}:{Realm}:{_password}");
        string ha2 = Md5Hex($"{method.Method}:{path}");
        string response = Md5Hex($"{ha1}:{_nonce}:{Nc}:{cnonce}:{Qop}:{ha2}");

        string authHeader = $"Digest username=\"{_username}\",realm=\"{Realm}\",qop={Qop}, " +
                          $"nonce=\"{_nonce}\",algorithm=MD5,cnonce=\"{cnonce}\",nc={Nc}," +
                          $"uri=\"{path}\",response=\"{response}\"";

        var req = new HttpRequestMessage(method, pathAndQuery);
        _ = req.Headers.TryAddWithoutValidation("Authorization", authHeader);
        if (body is not null)
        {
            req.Content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");
        }

        return req;
    }

    private static async Task<JsonNode?> ReadJsonAsync(HttpResponseMessage resp, CancellationToken ct, bool leaveOpen = false)
    {
        string text = await resp.Content.ReadAsStringAsync(ct);
        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return null;
        }
        finally
        {
            if (!leaveOpen)
            {
                resp.Dispose();
            }
        }
    }

    private static string? ExtractDigestField(string header, string field)
    {
        Match match = Regex.Match(header, $"{field}=\"?([^\",]+)\"?");
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>16-digit channel resource code — see docs/NVR_API.md section 4.</summary>
    public static string ResourceCode(int channel)
    {
        return $"1100{channel * 100:D4}0100{channel:D4}";
    }

    private static string Md5Hex(string input)
    {
        byte[] bytes = System.Security.Cryptography.MD5.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }

    public void Dispose()
    {
        _http.Dispose();
    }
}

public sealed record RecordSegment(int Channel, DateTimeOffset Begin, DateTimeOffset End, int RecordType)
{
    /// <summary>5 = Manual, 1 = Motion Detection (observed values — see docs/NVR_API.md).</summary>
    public bool IsMotionDetection => RecordType == 1;
}

/// <summary>
/// Numeric PTZCtrl command codes, decoded from the web UI's own minified
/// live-view JS (Static.PtzCmd enum in script/static_*.js). Move commands
/// start continuous motion - always follow one with its *Stop
/// counterpart. Not every camera model supports every command (e.g. no
/// wiper/heater); check PTZ/Capabilities first. See docs/NVR_API.md
/// section 11.
/// </summary>
public enum PtzCommand
{
    IrisCloseStop = 257,
    IrisClose = 258,
    IrisOpenStop = 259,
    IrisOpen = 260,
    FocusNearStop = 513,
    FocusNear = 514,
    FocusFarStop = 515,
    FocusFar = 516,
    ZoomTeleStop = 769,
    ZoomTele = 770,
    ZoomWideStop = 771,
    ZoomWide = 772,
    TiltUpStop = 1025,
    TiltUp = 1026,
    TiltDownStop = 1027,
    TiltDown = 1028,
    PanRightStop = 1281,
    PanRight = 1282,
    PanLeftStop = 1283,
    PanLeft = 1284,
    LeftUpStop = 1793,
    LeftUp = 1794,
    LeftDownStop = 1795,
    LeftDown = 1796,
    RightUpStop = 2049,
    RightUp = 2050,
    RightDownStop = 2051,
    RightDown = 2052,
    BrushOn = 2561,
    BrushOff = 2562,
    LightOn = 2817,
    LightOff = 2818,
    HeatOn = 3073,
    HeatOff = 3074,
    InfraredOn = 3329,
    InfraredOff = 3330,
    ScanCruise = 3585,
    SnowOn = 4609,
    SnowOff = 4610,
}

/// <summary>
/// The device has hit its concurrent download task limit (cmd 82 code
/// 60031). Persistent until stale sessions expire or the device is
/// rebooted - retrying immediately will not help.
/// </summary>
public sealed class DownloadCapacityException(string message) : InvalidOperationException(message);
