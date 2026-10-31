using System.Text.Json;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Ring.Entities;

namespace VideoForensics.Providers.Ring.Services
{
    /// <summary>
    /// Maps the shared users, linked chime doorbots and snapshot timestamp responses onto persistence rows. Each parent's
    /// rows are replaced as a whole. Null values stay null; a value of an unexpected JSON type is not stored.
    /// </summary>
    public static class RingSmallEndpointMapper
    {
        /// <summary>Maps the users shared onto a location. A user without an id is skipped, since it cannot be keyed.</summary>
        public static IReadOnlyList<RingLocationSharedUser> MapLocationSharedUsers(IEnumerable<SharedUser> users, Guid locationId)
        {
            ArgumentNullException.ThrowIfNull(users);

            DateTime now = DateTime.UtcNow;
            var rows = new List<RingLocationSharedUser>();
            foreach (SharedUser user in users)
            {
                if (user.Id is not long userId)
                {
                    continue;
                }

                var row = new RingLocationSharedUser
                {
                    Id = Guid.NewGuid(),
                    LocationId = locationId,
                    ProviderUserId = userId,
                    Verified = user.Verified,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email,
                    ObjectType = user.ObjectType,
                    UpdatedAtUtc = now,
                };

                int devicePosition = 0;
                foreach (SharedUserDevice device in user.Devices ?? [])
                {
                    var deviceRow = new RingLocationSharedUserDevice
                    {
                        Id = Guid.NewGuid(),
                        RingLocationSharedUserId = row.Id,
                        Position = devicePosition++,
                        ProviderDeviceId = device.Id,
                        DeviceType = device.DeviceType,
                        Role = device.Role,
                    };

                    int permissionPosition = 0;
                    foreach (string permission in StringElements(device.Permissions))
                    {
                        deviceRow.Permissions.Add(new RingLocationSharedUserDevicePermission
                        {
                            Id = Guid.NewGuid(),
                            RingLocationSharedUserDeviceId = deviceRow.Id,
                            Position = permissionPosition++,
                            Permission = permission,
                        });
                    }

                    row.Devices.Add(deviceRow);
                }

                rows.Add(row);
            }

            return rows;
        }

        /// <summary>
        /// Maps the raw linked_doorbots response of one chime. Items without an integer id are skipped. Returns null when the body
        /// has no linked_doorbots array: an undefined, malformed or empty body is a null result, not "no linked doorbots", so it
        /// must not be read as an empty list that would clear the chime's stored rows. An array that is present but empty maps to none.
        /// </summary>
        public static IReadOnlyList<RingChimeLinkedDoorbot>? MapChimeLinkedDoorbots(JsonElement response, Guid chimeDeviceId)
        {
            if (response.ValueKind != JsonValueKind.Object
                || !response.TryGetProperty("linked_doorbots", out JsonElement list)
                || list.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var rows = new List<RingChimeLinkedDoorbot>();
            DateTime now = DateTime.UtcNow;
            int position = 0;
            foreach (JsonElement item in list.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object
                    || !RingJsonValue.TryLong(Property(item, "id"), out long? id)
                    || id is not long linkedId)
                {
                    position++;
                    continue;
                }

                _ = RingJsonValue.TryText(Property(item, "device_id"), out string? deviceId);
                _ = RingJsonValue.TryText(Property(item, "kind"), out string? kind);
                _ = RingJsonValue.TryBool(Property(item, "call_alerts"), out bool? callAlerts);
                _ = RingJsonValue.TryBool(Property(item, "motion_alerts"), out bool? motionAlerts);

                rows.Add(new RingChimeLinkedDoorbot
                {
                    Id = Guid.NewGuid(),
                    ChimeDeviceId = chimeDeviceId,
                    Position = position++,
                    LinkedDoorbotId = linkedId,
                    DeviceId = deviceId,
                    Kind = kind,
                    CallAlerts = callAlerts,
                    MotionAlerts = motionAlerts,
                    UpdatedAtUtc = now,
                });
            }

            return rows;
        }

        /// <summary>Maps the latest snapshot timestamp of each doorbot. Entries without a doorbot id are skipped.</summary>
        public static IReadOnlyList<RingSnapshotTimestamp> MapSnapshotTimestamps(DoorbotTimestamps response, Guid ringAccountId)
        {
            ArgumentNullException.ThrowIfNull(response);

            DateTime now = DateTime.UtcNow;
            var rows = new List<RingSnapshotTimestamp>();
            int position = 0;
            foreach (DoorbotTimestamp entry in response.Timestamp ?? [])
            {
                if (entry.DoorbotId is not long doorbotId)
                {
                    position++;
                    continue;
                }

                rows.Add(new RingSnapshotTimestamp
                {
                    Id = Guid.NewGuid(),
                    RingAccountId = ringAccountId,
                    Position = position++,
                    DoorbotId = doorbotId,
                    TimestampEpoch = entry.TimestampEpoch,
                    UpdatedAtUtc = now,
                });
            }

            return rows;
        }

        // Permissions is typed object, so it arrives as a JsonElement. Only string elements are kept.
        private static IEnumerable<string> StringElements(object? permissions)
        {
            if (permissions is not JsonElement { ValueKind: JsonValueKind.Array } array)
            {
                yield break;
            }

            foreach (JsonElement element in array.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.String && element.GetString() is { } text)
                {
                    yield return text;
                }
            }
        }

        private static JsonElement Property(JsonElement obj, string name)
        {
            return obj.TryGetProperty(name, out JsonElement value) ? value : default;
        }
    }
}
