using System.Text.Json;

using VideoForensics.Providers.Ring.Entities;

using RingDeviceDetailsRow = VideoForensics.Data.Common.Entities.RingDeviceDetails;

namespace VideoForensics.Providers.Ring.Services
{
    /// <summary>The top-level ring_devices scalars: the provider-agnostic times for the Devices row and the Ring-only template row.</summary>
    public sealed record RingTopLevelFields(DateTime? DeactivatedAtUtc, DateTime? SharedAtUtc, RingDeviceDetailsRow Row);

    /// <summary>
    /// Reads the flexible top-level scalars of a ring_devices entry. A value whose shape does not fit its column is not stored and is
    /// noted; so is a null in a column whose type is a guess (every captured sample had null). Notes carry the key and type only.
    /// </summary>
    public static class RingDeviceTopLevelParser
    {
        public static RingTopLevelFields Parse(
            string? schemaId,
            bool? isSidewalkGateway,
            JsonElement? deactivatedAt,
            string? deviceResourceId,
            JsonElement? sharedAt,
            JsonElement? ringNetId,
            JsonElement? activeScheduleUuid,
            JsonElement? flickElimRecommendedMode,
            JsonElement? extPowerState,
            JsonElement? snoozeSettings,
            EncryptionGroup? encryptionGroup,
            List<string>? notes)
        {
            notes ??= [];
            // snooze_settings was null in every captured sample and its shape is unknown, so nothing is stored; the JSON type is logged.
            notes.Add($"snooze_settings not stored: no column, shape unknown ({snoozeSettings?.ValueKind.ToString() ?? "Null"})");
            return new RingTopLevelFields(
                Time(deactivatedAt, "deactivated_at", notes),
                Time(sharedAt, "shared_at", notes),
                new RingDeviceDetailsRow
                {
                    SchemaId = schemaId,
                    IsSidewalkGateway = isSidewalkGateway,
                    DeviceResourceId = deviceResourceId,
                    RingNetId = Text(ringNetId, "ring_net_id", notes),
                    ActiveScheduleUuid = Text(activeScheduleUuid, "active_schedule_uuid", notes),
                    FlickElimRecommendedMode = Text(flickElimRecommendedMode, "flick_elim_recommended_mode", notes),
                    ExtPowerState = Int(extPowerState, "ext_power_state", notes),
                    EncryptionGroupId = Text(encryptionGroup?.Id, "encryption_group.id", notes),
                    AccountGroupRevisionId = Text(encryptionGroup?.AccountGroupRevisionId, "encryption_group.account_group_revision_id", notes)
                });
        }

        private static string? Text(JsonElement? v, string key, List<string> notes)
        {
            if (v is not { } e || e.ValueKind == JsonValueKind.Null)
            {
                notes.Add($"{key} is null; its column type is a guess");
                return null;
            }

            if (!RingJsonValue.TryText(e, out string? text))
            {
                notes.Add($"{key} not stored: unexpected shape ({e.ValueKind})");
            }

            return text;
        }

        private static int? Int(JsonElement? v, string key, List<string> notes)
        {
            if (v is not { } e || e.ValueKind == JsonValueKind.Null)
            {
                notes.Add($"{key} is null; its column type is a guess");
                return null;
            }

            if (!RingJsonValue.TryInt(e, out int? number) || number == null)
            {
                notes.Add($"{key} not stored: unexpected shape ({e.ValueKind})");
            }

            return number;
        }

        private static DateTime? Time(JsonElement? v, string key, List<string> notes)
        {
            if (v is not { } e || e.ValueKind == JsonValueKind.Null)
            {
                notes.Add($"{key} is null; its column type is a guess");
                return null;
            }

            if (!RingJsonValue.TryUtcTime(e, out DateTime? time) || time == null)
            {
                notes.Add($"{key} not stored: unexpected shape ({e.ValueKind})");
            }

            return time;
        }
    }
}
