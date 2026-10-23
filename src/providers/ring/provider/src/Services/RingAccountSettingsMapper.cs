using System.Text.Json;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Ring.Entities;

namespace VideoForensics.Providers.Ring.Services
{
    /// <summary>
    /// Maps the profile, ringtone, light group and location mode responses onto persistence entities.
    /// The profile authentication token is never mapped: it is a credential. Each call assigns new row
    /// ids, so a caller replaces a parent's child rows rather than merging into them.
    /// </summary>
    public static class RingAccountSettingsMapper
    {
        private const string FeaturesScope = "features";
        private const string SettingsScope = "settings";
        private const string PreferencesScope = "preferences";

        /// <summary>Maps the profile, including its feature flags and user preferences. The token is excluded.</summary>
        public static RingAccountProfile MapProfile(Profile profile, Guid ringAccountId)
        {
            ArgumentNullException.ThrowIfNull(profile);

            var result = new RingAccountProfile
            {
                Id = Guid.NewGuid(),
                RingAccountId = ringAccountId,
                ProviderUserId = profile.Id,
                Email = profile.Email,
                FirstName = profile.FirstName,
                LastName = profile.LastName,
                FirstNameExtra = profile.FirstNameExtra,
                LastNameExtra = profile.LastNameExtra,
                PhoneNumber = profile.PhoneNumber,
                TfaPhoneNumber = profile.TfaPhoneNumber,
                TfaEnabled = profile.TfaEnabled,
                CfesEnrolled = profile.CfesEnrolled,
                HardwareId = profile.HardwareId,
                ExplorerProgramTerms = profile.ExplorerProgramTerms,
                UserFlow = profile.UserFlow,
                AppBrand = profile.AppBrand,
                Country = profile.Country,
                Status = profile.Status,
                CreatedAt = profile.CreatedAt,
                AccountType = profile.AccountType,
                TsvState = profile.TsvState,
            };

            if (profile.Features is not null)
            {
                result.SessionFeatures = MapSessionFeatures(profile.Features, result.Id);
                foreach (KeyValuePair<string, object> flag in profile.Features.AdditionalFeatures ?? [])
                {
                    AddFlag(result.Flags, result.Id, FeaturesScope, flag.Key, ToJsonElement(flag.Value));
                }
            }

            if (profile.UserPreferences is not null)
            {
                AddScope(result.Flags, result.Id, SettingsScope, profile.UserPreferences.Settings);
                AddScope(result.Flags, result.Id, PreferencesScope, profile.UserPreferences.Preferences);
            }

            return result;
        }

        /// <summary>Maps the ringtones list to one row per audio, each with its device kind children.</summary>
        public static IReadOnlyList<RingAccountRingtone> MapRingtones(RingtonesResponse response, Guid ringAccountId)
        {
            ArgumentNullException.ThrowIfNull(response);

            var rows = new List<RingAccountRingtone>();
            foreach (Ringtone audio in response.Ringtones ?? [])
            {
                var row = new RingAccountRingtone
                {
                    Id = Guid.NewGuid(),
                    RingAccountId = ringAccountId,
                    RingtoneId = audio.Id,
                    Description = audio.Description,
                    Kind = audio.Kind,
                    Category = audio.Category,
                    Available = audio.Available,
                    Checksum = audio.Checksum,
                    SampleRateKhz = audio.SampleRateKhz,
                    Url = audio.Url,
                    UrlAmz = audio.UrlAmz,
                    UserId = audio.UserId,
                };

                int position = 0;
                foreach (string kind in audio.SupportedDeviceKinds ?? [])
                {
                    row.SupportedDeviceKinds.Add(new RingAccountRingtoneDeviceKind
                    {
                        Id = Guid.NewGuid(),
                        RingAccountRingtoneId = row.Id,
                        Position = position++,
                        DeviceKind = kind,
                    });
                }

                rows.Add(row);
            }

            return rows;
        }

        /// <summary>Maps the default ringtone and owner ids returned beside the ringtones list.</summary>
        public static RingAccountRingtoneDefaults MapRingtoneDefaults(RingtonesResponse response, Guid ringAccountId)
        {
            ArgumentNullException.ThrowIfNull(response);

            return new RingAccountRingtoneDefaults
            {
                Id = Guid.NewGuid(),
                RingAccountId = ringAccountId,
                DefaultDingId = response.DefaultDingId,
                DefaultDingUserId = response.DefaultDingUserId,
                DefaultMotionId = response.DefaultMotionId,
                DefaultMotionUserId = response.DefaultMotionUserId,
                DefaultAlarmUserId = response.DefaultAlarmUserId,
                DefaultChirpUserId = response.DefaultChirpUserId,
                DefaultAppstoreUserId = response.DefaultAppstoreUserId,
            };
        }

        /// <summary>
        /// Maps a location's Smart Lighting groups. <paramref name="devicesByGroupId"/> supplies the device ids
        /// of each group; no captured payload has shown where Ring sends them, so callers pass none until it does.
        /// </summary>
        public static LightGroupSet MapLightGroups(
            GroupsResponse response,
            string providerLocationId,
            IReadOnlyDictionary<string, IReadOnlyList<string>>? devicesByGroupId = null)
        {
            ArgumentNullException.ThrowIfNull(response);

            var set = new LightGroupSet
            {
                Id = Guid.NewGuid(),
                ProviderLocationId = providerLocationId,
                IsOwner = response.IsOwner,
            };

            foreach (Group group in response.DeviceGroups ?? [])
            {
                var lightGroup = new LightGroup
                {
                    Id = Guid.NewGuid(),
                    LightGroupSetId = set.Id,
                    ProviderDeviceGroupId = group.DeviceGroupId,
                    Name = group.Name,
                };

                if (group.DeviceGroupId is not null
                    && devicesByGroupId is not null
                    && devicesByGroupId.TryGetValue(group.DeviceGroupId, out IReadOnlyList<string>? deviceIds))
                {
                    foreach (string deviceId in deviceIds)
                    {
                        lightGroup.Devices.Add(new LightGroupDevice
                        {
                            Id = Guid.NewGuid(),
                            LightGroupId = lightGroup.Id,
                            ProviderDeviceId = deviceId,
                        });
                    }
                }

                set.Groups.Add(lightGroup);
            }

            return set;
        }

        /// <summary>Maps the current location mode, including the raw notYetParticipatingInMode elements.</summary>
        public static RingLocationMode MapLocationMode(LocationMode mode, string providerLocationId)
        {
            ArgumentNullException.ThrowIfNull(mode);

            var result = new RingLocationMode
            {
                Id = Guid.NewGuid(),
                ProviderLocationId = providerLocationId,
                Mode = mode.Mode,
                LastUpdateTimeMs = mode.LastUpdateTimeMs,
                SecurityStatusMode = mode.SecurityStatus?.Mode,
                SecurityStatusLastUpdateMs = mode.SecurityStatus?.LastUpdateTimeMs,
                ReadOnly = mode.ReadOnly,
                ModePanelType = mode.ModePanelType,
            };

            int position = 0;
            foreach (JsonElement element in mode.NotYetParticipatingInMode ?? [])
            {
                result.NotYetParticipating.Add(new RingLocationModeNotYetParticipating
                {
                    Id = Guid.NewGuid(),
                    RingLocationModeId = result.Id,
                    Position = position++,
                    ValueJson = element.GetRawText(),
                });
            }

            return result;
        }

        /// <summary>Maps the location mode settings: the options block plus one entry per mode and device.</summary>
        public static RingLocationModeSettings MapLocationModeSettings(LocationModeSettingsResponse response, string providerLocationId)
        {
            ArgumentNullException.ThrowIfNull(response);

            var result = new RingLocationModeSettings
            {
                Id = Guid.NewGuid(),
                ProviderLocationId = providerLocationId,
                LastUpdateTimeMs = response.LastUpdateTimeMs,
                DefaultCameraOnlyMode = response.Options?.DefaultCameraOnlyMode,
            };

            int groupPosition = 0;
            foreach (string group in response.Options?.SupportedActionGroups ?? [])
            {
                result.SupportedActionGroups.Add(new RingLocationModeSupportedActionGroup
                {
                    Id = Guid.NewGuid(),
                    RingLocationModeSettingsId = result.Id,
                    Position = groupPosition++,
                    GroupName = group,
                });
            }

            AddModeEntries(result, "disarmed", response.Disarmed);
            AddModeEntries(result, "home", response.Home);
            AddModeEntries(result, "away", response.Away);
            return result;
        }

        /// <summary>Maps the location mode sharing state.</summary>
        public static RingLocationModeSharing MapLocationModeSharing(LocationModeSharingResponse response, string providerLocationId)
        {
            ArgumentNullException.ThrowIfNull(response);

            return new RingLocationModeSharing
            {
                Id = Guid.NewGuid(),
                ProviderLocationId = providerLocationId,
                SharedUsersEnabled = response.SharedUsersEnabled,
                LastUpdateTimeMs = response.LastUpdateTimeMs,
            };
        }

        private static RingAccountSessionFeatures MapSessionFeatures(SessionFeatures f, Guid profileId)
        {
            return new()
            {
                Id = Guid.NewGuid(),
                RingAccountProfileId = profileId,
                RemoteLoggingFormatStoring = f.RemoteLoggingFormatStoring,
                RemoteLoggingLevel = f.RemoteLoggingLevel,
                SubscriptionsEnabled = f.SubscriptionsEnabled,
                StickupCamSetupEnabled = f.StickupcamSetupEnabled,
                VodEnabled = f.VodEnabled,
                NwEnabled = f.NwEnabled,
                NwUserActivated = f.NwUserActivated,
                RingPlusEnabled = f.RingPlusEnabled,
                LpdEnabled = f.LdpEnabled,
                ReactiveSnoozingEnabled = f.ReactiveSnoozingEnabled,
                ProActiveSnoozingEnabled = f.ProActiveSnoozingEnabled,
                OwnerProActiveSnoozingEnabled = f.OwnerProActiveSnoozingEnabled,
                LiveviewSettingsEnabled = f.LiveviewSettingsEnabled,
                DeleteAllSettingsEnabled = f.DeleteAllSettingsEnabled,
                PowerCableEnabled = f.PowerCableEnabled,
                DeviceHealthAlertsEnabled = f.DeviceHealthAlertsEnabled,
                ChimeProEnabled = f.ChimeProEnabled,
                MultipleCallsEnabled = f.MultipleCallsEnabled,
                UjetEnabled = f.UjetEnabled,
                MultipleDeleteEnabled = f.MultipleDeleteEnabled,
                DeleteAllEnabled = f.DeleteAllEnabled,
                LdpMotionAnnouncementEnabled = f.LdpMotionAnnouncementEnabled,
                StarredEventsEnabled = f.StarredEventsEnabled,
                ChimeDndEnabled = f.ChimeDndEnabled,
                VideoSearchEnabled = f.VideoSearchEnabled,
                FloodlightCamEnabled = f.FlooglightCamEnabled,
                NwLargerAreaEnabled = f.NwLargerAreaEnabled,
                RingCamBatteryEnabled = f.RingCamBatteryEnabled,
                EliteCamEnabled = f.EliteCamEnabled,
                DoorbellV2Enabled = f.DoorbellV2Enabled,
                SpotlightBatteryDashboardControlsEnabled = f.SpotlightBatteryDashboardControlsEnabled,
                BypassAccountVerification = f.ByPassAccountVerification,
                LegacyCvrRetentionEnabled = f.LegacyCvrRetentionEnabled,
                NewDashboardEnabled = f.NewDashboardEnabled,
                RingCamEnabled = f.RingCamEnabled,
            };
        }

        private static void AddScope(List<RingAccountProfileFlag> flags, Guid profileId, string scope, IDictionary<string, JsonElement>? values)
        {
            foreach (KeyValuePair<string, JsonElement> entry in values ?? new Dictionary<string, JsonElement>())
            {
                AddFlag(flags, profileId, scope, entry.Key, entry.Value);
            }
        }

        /// <summary>
        /// Adds one flag row per scalar and per array, and recurses into objects so each leaf has its own
        /// dotted name. An empty object is kept as an "object" row so the key is not lost.
        /// </summary>
        private static void AddFlag(List<RingAccountProfileFlag> flags, Guid profileId, string scope, string name, JsonElement value)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.True:
                case JsonValueKind.False:
                    flags.Add(NewFlag(profileId, scope, name, "bool", boolValue: value.GetBoolean()));
                    break;
                case JsonValueKind.Number:
                    flags.Add(value.TryGetInt64(out long integer)
                        ? NewFlag(profileId, scope, name, "int", intValue: integer)
                        : NewFlag(profileId, scope, name, "decimal", stringValue: value.GetRawText()));
                    break;
                case JsonValueKind.String:
                    flags.Add(NewFlag(profileId, scope, name, "string", stringValue: value.GetString()));
                    break;
                case JsonValueKind.Array:
                    RingAccountProfileFlag list = NewFlag(profileId, scope, name, "list");
                    int position = 0;
                    foreach (JsonElement element in value.EnumerateArray())
                    {
                        list.Items.Add(new RingAccountProfileFlagItem
                        {
                            Id = Guid.NewGuid(),
                            RingAccountProfileFlagId = list.Id,
                            Position = position++,
                            Value = element.ValueKind == JsonValueKind.String ? element.GetString() ?? string.Empty : element.GetRawText(),
                        });
                    }

                    flags.Add(list);
                    break;
                case JsonValueKind.Object:
                    if (!value.EnumerateObject().Any())
                    {
                        flags.Add(NewFlag(profileId, scope, name, "object"));
                        break;
                    }

                    foreach (JsonProperty child in value.EnumerateObject())
                    {
                        AddFlag(flags, profileId, scope, $"{name}.{child.Name}", child.Value);
                    }

                    break;
                default:
                    flags.Add(NewFlag(profileId, scope, name, "null"));
                    break;
            }
        }

        private static RingAccountProfileFlag NewFlag(
            Guid profileId,
            string scope,
            string name,
            string valueKind,
            bool? boolValue = null,
            long? intValue = null,
            string? stringValue = null)
        {
            return new()
            {
                Id = Guid.NewGuid(),
                RingAccountProfileId = profileId,
                Scope = scope,
                Name = name,
                ValueKind = valueKind,
                BoolValue = boolValue,
                IntValue = intValue,
                StringValue = stringValue,
            };
        }

        /// <summary>The untyped feature values arrive as JsonElement, but a value set in code may be any object.</summary>
        private static JsonElement ToJsonElement(object? value)
        {
            return value is JsonElement element ? element : JsonSerializer.SerializeToElement(value);
        }

        private static void AddModeEntries(RingLocationModeSettings settings, string mode, IEnumerable<LocationModeSettingsEntry>? entries)
        {
            int position = 0;
            foreach (LocationModeSettingsEntry entry in entries ?? [])
            {
                var row = new RingLocationModeSettingsEntry
                {
                    Id = Guid.NewGuid(),
                    RingLocationModeSettingsId = settings.Id,
                    Mode = mode,
                    Position = position++,
                    DeviceId = entry.DeviceId,
                    DeviceIdType = entry.DeviceIdType,
                };

                int actionPosition = 0;
                foreach (string action in entry.Actions ?? [])
                {
                    row.Actions.Add(NewAction(row.Id, "enabled", null, actionPosition++, action));
                }

                foreach (KeyValuePair<string, List<string>> group in entry.SupportedActions ?? [])
                {
                    int groupActionPosition = 0;
                    foreach (string action in group.Value ?? [])
                    {
                        row.Actions.Add(NewAction(row.Id, "supported", group.Key, groupActionPosition++, action));
                    }
                }

                settings.Entries.Add(row);
            }
        }

        private static RingLocationModeSettingsEntryAction NewAction(Guid entryId, string kind, string? actionGroup, int position, string actionName)
        {
            return new()
            {
                Id = Guid.NewGuid(),
                RingLocationModeSettingsEntryId = entryId,
                Kind = kind,
                ActionGroup = actionGroup,
                Position = position,
                ActionName = actionName,
            };
        }
    }
}
