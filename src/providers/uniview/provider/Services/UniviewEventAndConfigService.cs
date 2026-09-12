using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using VideoForensics.Providers.Common.Contracts;

namespace VideoForensics.Providers.Uniview.Services;

/// <summary>
/// Uniview implementation of event and configuration retrieval.
///
/// Events are derived from motion-detected recording segments. The Uniview API
/// does not expose a separate events endpoint — the only "events" available are
/// recording segments marked with RecordType == 1 (motion detection).
/// Recording-schedule queries are chunked by month to avoid the per-query result
/// cap documented in docs/NVR_API.md section 9.
/// </summary>
public class UniviewEventAndConfigService : IEventAndConfigService
{
    private readonly ILogger<UniviewEventAndConfigService> _logger;
    private readonly IUniviewSessionProvider _sessionProvider;

    public UniviewEventAndConfigService(
        ILogger<UniviewEventAndConfigService> logger,
        IUniviewSessionProvider sessionProvider)
    {
        _logger = logger;
        _sessionProvider = sessionProvider;
    }

    public async Task<IReadOnlyList<DeviceEvent>> GetEventsAsync(
        string deviceId,
        DateTime startDate,
        DateTime endDate,
        string? eventType = null,
        CancellationToken cancellationToken = default)
    {
        var client = _sessionProvider.GetClient();
        if (client is null)
        {
            _logger.LogError("Not authenticated: Session is null");
            return new List<DeviceEvent>().AsReadOnly();
        }

        // Parse deviceId to channel number (assuming format like "1", "2", etc.)
        if (!int.TryParse(deviceId, out var channel))
            throw new ArgumentException($"Invalid deviceId format: {deviceId}", nameof(deviceId));

        // If eventType is specified and not motion-related, return empty list since motion
        // is the only event type this device exposes
        if (eventType is not null && !eventType.Equals("motion", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Event type '{EventType}' not supported by Uniview (only 'motion' available)", eventType);
            return [];
        }

        var clockOffset = await client.GetClockOffsetAsync(cancellationToken);
        var events = new List<DeviceEvent>();

        // Query segment listings month by month to avoid the per-query result cap
        // documented in NVR_API.md section 9 ("never one big multi-year range")
        for (var current = new DateTimeOffset(startDate);
             current < new DateTimeOffset(endDate);
             current = current.AddMonths(1))
        {
            var monthStart = current;
            var monthEnd = current.AddMonths(1);

            // Clamp the month-end to the requested endDate
            if (monthEnd > new DateTimeOffset(endDate))
                monthEnd = new DateTimeOffset(endDate);

            try
            {
                var segments = await client.ListSegmentsAsync(channel, monthStart, monthEnd, cancellationToken);

                foreach (var segment in segments)
                {
                    // Only include motion-detection segments (RecordType == 1)
                    if (!segment.IsMotionDetection)
                        continue;

                    var eventId = $"{deviceId}-{segment.Begin.ToUnixTimeSeconds()}";
                    var correctedBegin = segment.Begin + clockOffset;
                    var correctedEnd = segment.End + clockOffset;
                    var metadata = new Dictionary<string, string>
                    {
                        ["EndTime"] = correctedEnd.UtcDateTime.ToString("O"),
                    };

                    var deviceEvent = new DeviceEvent(
                        Id: eventId,
                        DeviceId: deviceId,
                        EventType: "motion",
                        Timestamp: correctedBegin.UtcDateTime,
                        SnapshotUrl: null, // No per-event snapshot API available
                        Metadata: metadata);

                    events.Add(deviceEvent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to query segments for channel {Channel} in month {Month}",
                    channel, monthStart.ToString("yyyy-MM"));
                throw;
            }
        }

        return events.AsReadOnly();
    }

    public async Task<DeviceConfig?> GetDeviceConfigAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        var client = _sessionProvider.GetClient();
        if (client is null)
        {
            _logger.LogError("Not authenticated: Session is null");
            return null;
        }

        // Parse deviceId to channel number
        if (!int.TryParse(deviceId, out var channel))
            throw new ArgumentException($"Invalid deviceId format: {deviceId}", nameof(deviceId));

        try
        {
            // Fetch motion detection and recording schedule configs
            var motionDetectionJson = await client.GetMotionDetectionAsync(channel, cancellationToken);
            var recordScheduleJson = await client.GetRecordScheduleAsync(channel, cancellationToken);

            // Extract motion detection settings (defensively, field names are unverified against live device)
            var motionDetectionEnabled = false;
            var motionSensitivity = 0;

            if (motionDetectionJson is JsonObject motionObj)
            {
                try
                {
                    // Try to extract Enabled flag from Rule.Enabled or Rule object
                    if (motionObj["Rule"] is JsonObject ruleObj)
                    {
                        if (ruleObj["Enabled"] is JsonValue enabledVal)
                        {
                            motionDetectionEnabled = enabledVal.GetValue<int>() != 0;
                        }
                    }

                    // Try to extract sensitivity from Areas.GridArea.Sensitivity
                    if (motionObj["Areas"] is JsonObject areasObj &&
                        areasObj["GridArea"] is JsonObject gridObj &&
                        gridObj["Sensitivity"] is JsonValue sensVal)
                    {
                        motionSensitivity = sensVal.GetValue<int>();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse motion detection config for channel {Channel} - " +
                        "field names may differ from documented defaults; using safe defaults",
                        channel);
                    motionDetectionEnabled = false;
                    motionSensitivity = 0;
                }
            }

            // Extract recording mode (best-effort, unverified against live device)
            var recordingMode = "unknown";
            if (recordScheduleJson is JsonObject schedObj)
            {
                try
                {
                    // Check for "Enabled" field to infer recording mode
                    if (schedObj["Enabled"] is JsonValue enabledVal)
                    {
                        recordingMode = enabledVal.GetValue<int>() != 0 ? "always" : "off";
                    }

                    // Could also check RecordRule for more granular mode info, but
                    // the simple enabled/off dichotomy suffices for now
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse record schedule config for channel {Channel} - " +
                        "field names may differ from documented defaults; using 'unknown'",
                        channel);
                    recordingMode = "unknown";
                }
            }

            // Store raw JSON blobs in CustomSettings to preserve all information
            var customSettings = new Dictionary<string, object>();
            if (motionDetectionJson is not null)
            {
                customSettings["motionDetectionRaw"] = motionDetectionJson.ToJsonString();
            }
            if (recordScheduleJson is not null)
            {
                customSettings["recordScheduleRaw"] = recordScheduleJson.ToJsonString();
            }

            var config = new DeviceConfig(
                DeviceId: deviceId,
                MotionDetectionEnabled: motionDetectionEnabled,
                MotionSensitivity: motionSensitivity,
                RecordingMode: recordingMode,
                CustomSettings: customSettings.Count > 0 ? customSettings : null);

            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get device config for channel {Channel}", channel);
            throw;
        }
    }

    public async Task<bool> UpdateDeviceConfigAsync(string deviceId, DeviceConfig config, CancellationToken cancellationToken = default)
    {
        var client = _sessionProvider.GetClient();
        if (client is null)
        {
            _logger.LogError("Not authenticated: Session is null");
            return false;
        }

        // Parse deviceId to channel number
        if (!int.TryParse(deviceId, out var channel))
            throw new ArgumentException($"Invalid deviceId format: {deviceId}", nameof(deviceId));

        try
        {
            // Build motion detection JSON: prefer round-tripping the raw JSON from
            // CustomSettings if available (safer for fields we don't fully understand),
            // otherwise construct from the typed config fields.
            JsonObject motionDetectionData;

            if (config.CustomSettings?.TryGetValue("motionDetectionRaw", out var rawMotionObj) == true &&
                rawMotionObj is string rawMotionString)
            {
                try
                {
                    // Parse the raw JSON and merge in the typed field updates
                    motionDetectionData = JsonNode.Parse(rawMotionString) as JsonObject
                        ?? new JsonObject();

                    // Update the typed fields within the existing structure
                    if (motionDetectionData["Rule"] is JsonObject ruleObj)
                    {
                        ruleObj["Enabled"] = config.MotionDetectionEnabled ? 1 : 0;
                    }
                    else
                    {
                        motionDetectionData["Rule"] = new JsonObject
                        {
                            ["Enabled"] = config.MotionDetectionEnabled ? 1 : 0,
                        };
                    }

                    // Also update sensitivity if we can find the right field
                    if (motionDetectionData["Areas"] is JsonObject areasObj &&
                        areasObj["GridArea"] is JsonObject gridObj)
                    {
                        gridObj["Sensitivity"] = config.MotionSensitivity;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to round-trip motion detection raw JSON for channel {Channel}; " +
                        "falling back to reconstructed config", channel);
                    motionDetectionData = BuildMotionDetectionJson(config);
                }
            }
            else
            {
                // No raw JSON available, construct from typed fields
                motionDetectionData = BuildMotionDetectionJson(config);
            }

            // Send the motion detection update
            await client.SetMotionDetectionAsync(channel, motionDetectionData, cancellationToken);

            _logger.LogInformation("Updated motion detection config for channel {Channel}: " +
                "enabled={Enabled}, sensitivity={Sensitivity}",
                channel, config.MotionDetectionEnabled, config.MotionSensitivity);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update device config for channel {Channel}", channel);
            return false;
        }
    }

    /// <summary>
    /// Constructs a motion detection JSON object from typed config fields,
    /// using best-effort field names (unverified against a live device).
    /// </summary>
    private static JsonObject BuildMotionDetectionJson(DeviceConfig config)
    {
        return new JsonObject
        {
            ["Rule"] = new JsonObject
            {
                ["Enabled"] = config.MotionDetectionEnabled ? 1 : 0,
            },
            ["Areas"] = new JsonObject
            {
                ["GridArea"] = new JsonObject
                {
                    ["Sensitivity"] = config.MotionSensitivity,
                },
            },
        };
    }
}
