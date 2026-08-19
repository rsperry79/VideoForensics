using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using KoenZomers.Ring.Api;

namespace Ring.Api.Tester
{
    /// <summary>
    /// What a single endpoint call needs from the fan-out (nothing, one location, one doorbot, or
    /// one chime). Locations/doorbots/chimes are discovered by running the "devices"/"locations"
    /// endpoints first.
    /// </summary>
    internal enum EndpointScope
    {
        None,
        PerLocation,
        PerDoorbot,
        PerChime
    }

    /// <summary>
    /// What to put a setting back to after a destructive call tested a new value, and what the
    /// original value actually was (or a safe fallback, if the original couldn't be read back).
    /// </summary>
    internal sealed record RestorePlan(string OriginalValueDescription, bool WasCaptured, Func<Session, Task> Restore);

    /// <summary>
    /// Describes one Ring API call this tool knows how to make: identity for --endpoints selection
    /// and --list, plus the delegate that actually invokes the KoenZomers.Ring.Api Session method.
    /// <see cref="Destructive"/> calls mutate account/device state and only ever run when the
    /// caller passes --destructive; <see cref="Physical"/> ones additionally trigger a real-world
    /// effect on the hardware (light, siren, chime speaker, camera shutter) and are further
    /// excluded by --no-physical. <see cref="PrepareRestore"/>, when set, is called after a
    /// successful destructive call to put the setting back - see README.md's "Backup and restore"
    /// section for how this fits into a run.
    /// </summary>
    internal sealed class EndpointDescriptor
    {
        public required string Key { get; init; }
        public required string DisplayName { get; init; }
        public required string Description { get; init; }
        public required string SessionMethod { get; init; }
        public required string HttpMethod { get; init; }
        public required string ApiPath { get; init; }
        public EndpointScope Scope { get; init; } = EndpointScope.None;
        public bool Destructive { get; init; }
        public bool Physical { get; init; }
        public required Func<Session, EndpointTarget, Task> Invoke { get; init; }
        public Func<EndpointTarget, RestorePlan?>? PrepareRestore { get; init; }
        /// <summary>Why a destructive endpoint has no PrepareRestore - shown in index.json instead of leaving it unexplained.</summary>
        public string? NoRestoreReason { get; init; }
    }

    /// <summary>
    /// The location/doorbot/chime (if any) a single call was made against, discovered from the
    /// devices/locations endpoints or narrowed via --location-id/--doorbot-id/--chime-id.
    /// </summary>
    internal sealed record EndpointTarget(Guid? LocationId, long? DoorbotId, long? ChimeId = null)
    {
        public static readonly EndpointTarget None = new(null, null);
    }

    internal static class EndpointRegistry
    {
        // Original-value snapshots for restore. Doorbot settings are populated once by Runner from
        // the raw "devices" response before any destructive call runs; location mode is captured
        // per-location by set-location-mode's own Invoke, immediately before it mutates that
        // location (see there for why). Declared before All below since All's PrepareRestore
        // lambdas reference these by name - the nullable analyzer treats a static member as not-yet-
        // assigned when it's referenced before its own declaration in source order.
        public static Dictionary<long, DoorbotSettingsSnapshot> OriginalDoorbotSettings { get; set; } = new();
        public static Dictionary<Guid, string> OriginalLocationModeByLocation { get; } = new();

        public static readonly IReadOnlyList<EndpointDescriptor> All = new List<EndpointDescriptor>
        {
            new()
            {
                Key = "devices",
                DisplayName = "List devices",
                Description = "All doorbots, chimes and stickup cams registered on the account.",
                SessionMethod = "Session.GetRingDevices()",
                HttpMethod = "GET",
                ApiPath = "https://api.ring.com/clients_api/ring_devices?api_version=11",
                Scope = EndpointScope.None,
                Invoke = (session, _) => session.GetRingDevices()
            },
            new()
            {
                Key = "locations",
                DisplayName = "List locations",
                Description = "Locations (with friendly names) visible to the account.",
                SessionMethod = "Session.GetLocations()",
                HttpMethod = "GET",
                ApiPath = "https://api.ring.com/devices/v1/locations",
                Scope = EndpointScope.None,
                Invoke = (session, _) => session.GetLocations()
            },
            new()
            {
                Key = "doorbot-history",
                DisplayName = "Doorbot event history",
                Description = "Recent ding/motion history across all doorbots (count via --history-limit).",
                SessionMethod = "Session.GetDoorbotsHistory(limit)",
                HttpMethod = "GET",
                ApiPath = "https://api.ring.com/clients_api/doorbots/history?limit={limit}",
                Scope = EndpointScope.None,
                Invoke = (session, _) => session.GetDoorbotsHistory(limit: CurrentHistoryLimit)
            },
            new()
            {
                Key = "location-mode",
                DisplayName = "Location mode",
                Description = "Current home/away/disarmed mode for a location (camera-only locations without an Alarm hub).",
                SessionMethod = "Session.GetLocationMode(locationId)",
                HttpMethod = "GET",
                ApiPath = "https://api.ring.com/rs/mode/location/{locationId}",
                Scope = EndpointScope.PerLocation,
                Invoke = (session, target) => session.GetLocationMode(target.LocationId!.Value)
            },
            new()
            {
                Key = "shared-users",
                DisplayName = "Shared users",
                Description = "Users who currently have access to a location.",
                SessionMethod = "Session.GetSharedUsers(locationId)",
                HttpMethod = "GET",
                ApiPath = "https://api.ring.com/clients_api/locations/{locationId}/users",
                Scope = EndpointScope.PerLocation,
                Invoke = (session, target) => session.GetSharedUsers(target.LocationId!.Value)
            },
            new()
            {
                Key = "invitations",
                DisplayName = "Pending invitations",
                Description = "Pending share invitations for a location.",
                SessionMethod = "Session.GetInvitations(locationId)",
                HttpMethod = "GET",
                ApiPath = "https://api.ring.com/clients_api/locations/{locationId}/invitations",
                Scope = EndpointScope.PerLocation,
                Invoke = (session, target) => session.GetInvitations(target.LocationId!.Value)
            },
            new()
            {
                Key = "snapshot-timestamps",
                DisplayName = "Snapshot timestamps",
                Description = "When the last snapshot was taken for a doorbot. Read-only - does not request a new snapshot (that's update-snapshot, a --destructive endpoint since it triggers the physical camera).",
                SessionMethod = "Session.GetDoorbotSnapshotTimestamp(doorbotId)",
                HttpMethod = "POST",
                ApiPath = "https://api.ring.com/clients_api/snapshots/timestamps",
                Scope = EndpointScope.PerDoorbot,
                Invoke = (session, target) => session.GetDoorbotSnapshotTimestamp((int)target.DoorbotId!.Value)
            },
            new()
            {
                Key = "doorbot-health",
                DisplayName = "Doorbot health",
                Description = "Connectivity/battery telemetry for a single doorbot, fetched standalone rather than from the devices listing.",
                SessionMethod = "Session.GetDoorbotHealth(doorbotId)",
                HttpMethod = "GET",
                ApiPath = "https://api.ring.com/clients_api/doorbots/{doorbotId}/health",
                Scope = EndpointScope.PerDoorbot,
                Invoke = (session, target) => session.GetDoorbotHealth(target.DoorbotId!.Value)
            },
            new()
            {
                Key = "chime-health",
                DisplayName = "Chime health",
                Description = "Connectivity telemetry for a single chime, fetched standalone rather than from the devices listing.",
                SessionMethod = "Session.GetChimeHealth(chimeId)",
                HttpMethod = "GET",
                ApiPath = "https://api.ring.com/clients_api/chimes/{chimeId}/health",
                Scope = EndpointScope.PerChime,
                Invoke = (session, target) => session.GetChimeHealth(target.ChimeId!.Value)
            },
            new()
            {
                Key = "device-settings",
                DisplayName = "Device settings",
                Description = "Raw per-device settings object (motion zones, detection toggles, etc.) for a doorbot.",
                SessionMethod = "Session.GetDeviceSettings(doorbotId)",
                HttpMethod = "GET",
                ApiPath = "https://api.ring.com/devices/v1/devices/{doorbotId}/settings",
                Scope = EndpointScope.PerDoorbot,
                Invoke = (session, target) => session.GetDeviceSettings(target.DoorbotId!.Value)
            },
            new()
            {
                Key = "video-search",
                DisplayName = "Video search",
                Description = "Date-range-filterable event search for a doorbot (called here with no date bounds - an alternative to doorbot-history).",
                SessionMethod = "Session.VideoSearch(doorbotId)",
                HttpMethod = "GET",
                ApiPath = "https://api.ring.com/clients_api/video_search/history?doorbot_id={doorbotId}",
                Scope = EndpointScope.PerDoorbot,
                Invoke = (session, target) => session.VideoSearch(target.DoorbotId!.Value)
            },
            new()
            {
                Key = "location-events",
                DisplayName = "Location events",
                Description = "Unified event feed across every device at a location, as an alternative to doorbot-history.",
                SessionMethod = "Session.GetLocationEvents(locationId)",
                HttpMethod = "GET",
                ApiPath = "https://api.ring.com/clients_api/locations/{locationId}/events",
                Scope = EndpointScope.PerLocation,
                Invoke = (session, target) => session.GetLocationEvents(target.LocationId!.Value)
            },
            new()
            {
                Key = "location-mode-settings",
                DisplayName = "Location mode settings",
                Description = "Location Mode configuration (e.g. per-mode alert settings) for a location.",
                SessionMethod = "Session.GetLocationModeSettings(locationId)",
                HttpMethod = "GET",
                ApiPath = "https://api.ring.com/rs/mode/location/{locationId}/settings",
                Scope = EndpointScope.PerLocation,
                Invoke = (session, target) => session.GetLocationModeSettings(target.LocationId!.Value)
            },
            new()
            {
                Key = "location-mode-sharing",
                DisplayName = "Location mode sharing",
                Description = "Location Mode sharing configuration for a location.",
                SessionMethod = "Session.GetLocationModeSharing(locationId)",
                HttpMethod = "GET",
                ApiPath = "https://api.ring.com/rs/mode/location/{locationId}/sharing",
                Scope = EndpointScope.PerLocation,
                Invoke = (session, target) => session.GetLocationModeSharing(target.LocationId!.Value)
            },
            new()
            {
                Key = "account-monitoring-status",
                DisplayName = "Alarm monitoring status",
                Description = "Alarm monitoring account status for a location.",
                SessionMethod = "Session.GetAccountMonitoringStatus(locationId)",
                HttpMethod = "GET",
                ApiPath = "https://api.ring.com/rs/monitoring/accounts/{locationId}",
                Scope = EndpointScope.PerLocation,
                Invoke = (session, target) => session.GetAccountMonitoringStatus(target.LocationId!.Value)
            },
            new()
            {
                Key = "location-history",
                DisplayName = "Location history",
                Description = "Raw location-wide history entries, distinct from doorbot-history/location-events.",
                SessionMethod = "Session.GetLocationHistory(locationId)",
                HttpMethod = "GET",
                ApiPath = "https://api.ring.com/evm/v2/history/locations/{locationId}",
                Scope = EndpointScope.PerLocation,
                Invoke = (session, target) => session.GetLocationHistory(target.LocationId!.Value)
            },
            new()
            {
                Key = "profile",
                DisplayName = "Account profile",
                Description = "Profile of the currently authenticated account.",
                SessionMethod = "Session.GetProfile()",
                HttpMethod = "GET",
                ApiPath = "https://api.ring.com/clients_api/profile",
                Scope = EndpointScope.None,
                Invoke = (session, _) => session.GetProfile()
            },
            new()
            {
                Key = "ringtones",
                DisplayName = "Chime ringtones",
                Description = "Ringtones available to assign to a chime via update-chime.",
                SessionMethod = "Session.GetRingtones()",
                HttpMethod = "GET",
                ApiPath = "https://api.ring.com/clients_api/ringtones",
                Scope = EndpointScope.None,
                Invoke = (session, _) => session.GetRingtones()
            },
            new()
            {
                Key = "amazon-key-locks",
                DisplayName = "Amazon Key lock associations",
                Description = "Amazon Key integration's lock associations for this account.",
                SessionMethod = "Session.FetchAmazonKeyLocks()",
                HttpMethod = "GET",
                ApiPath = "https://api.ring.com/integrations/amazonkey/v2/devices/lock_associations",
                Scope = EndpointScope.None,
                Invoke = (session, _) => session.FetchAmazonKeyLocks()
            },
            new()
            {
                Key = "active-dings",
                DisplayName = "Active dings",
                Description = "Dings (doorbell presses/motion events) currently active/in-progress across the account.",
                SessionMethod = "Session.GetActiveDings()",
                HttpMethod = "GET",
                ApiPath = "https://api.ring.com/clients_api/dings/active",
                Scope = EndpointScope.None,
                Invoke = (session, _) => session.GetActiveDings()
            },
            new()
            {
                Key = "location-details",
                DisplayName = "Location details",
                Description = "Single-location detail lookup under clients_api, distinct from the locations endpoint's devices/v1 listing.",
                SessionMethod = "Session.GetLocation(locationId)",
                HttpMethod = "GET",
                ApiPath = "https://api.ring.com/clients_api/locations/{locationId}",
                Scope = EndpointScope.PerLocation,
                Invoke = (session, target) => session.GetLocation(target.LocationId!.Value)
            },
            new()
            {
                Key = "linked-chime-doorbots",
                DisplayName = "Linked chime doorbots",
                Description = "Doorbots linked to (chiming through) a chime.",
                SessionMethod = "Session.GetLinkedChimeDoorbots(chimeId)",
                HttpMethod = "GET",
                ApiPath = "https://api.ring.com/clients_api/chimes/{chimeId}/linked_doorbots",
                Scope = EndpointScope.PerChime,
                Invoke = (session, target) => session.GetLinkedChimeDoorbots(target.ChimeId!.Value)
            },

            // ---- Destructive: mutate account/device state. Only run with --destructive. Each
            // one either restores what it changed (PrepareRestore) or documents why it can't
            // (NoRestoreReason) - see README.md's "Backup and restore" section. ----

            new()
            {
                Key = "update-snapshot",
                DisplayName = "Request fresh snapshot",
                Description = "Requests the camera capture a brand new snapshot right now. Triggers the physical camera shutter.",
                SessionMethod = "Session.UpdateSnapshot(doorbotId)",
                HttpMethod = "PUT",
                ApiPath = "https://api.ring.com/clients_api/snapshots/update_all",
                Scope = EndpointScope.PerDoorbot,
                Destructive = true,
                Physical = true,
                NoRestoreReason = "not applicable: requests a new image capture, doesn't change a persisted setting.",
                Invoke = (session, target) => session.UpdateSnapshot((int)target.DoorbotId!.Value)
            },
            new()
            {
                Key = "set-light",
                DisplayName = "Toggle floodlight/spotlight",
                Description = "Turns the camera's light on to test it, then restores it to off. Only applicable to devices with a light. Visibly triggers the hardware.",
                SessionMethod = "Session.SetLight(doorbotId, on)",
                HttpMethod = "PUT",
                ApiPath = "https://api.ring.com/clients_api/doorbots/{doorbotId}/floodlight_light_{on|off}",
                Scope = EndpointScope.PerDoorbot,
                Destructive = true,
                Physical = true,
                Invoke = (session, target) => session.SetLight(target.DoorbotId!.Value, true),
                // This client has no getter for current light on/off state, so the original value
                // can't be read back - off is the safe assumption for a light that isn't a
                // deliberately-left-on porch light during the day.
                PrepareRestore = target => new RestorePlan(
                    "unknown (no read-path for current light state via this client) - restoring to off as a safe default",
                    WasCaptured: false,
                    Restore: session => session.SetLight(target.DoorbotId!.Value, false))
            },
            new()
            {
                Key = "set-siren",
                DisplayName = "Trigger siren",
                Description = "Sounds the camera's siren briefly (--siren-duration-seconds, default 2) to test it, then restores it to off. Audibly triggers the hardware.",
                SessionMethod = "Session.SetSiren(doorbotId, on, durationSeconds)",
                HttpMethod = "PUT",
                ApiPath = "https://api.ring.com/clients_api/doorbots/{doorbotId}/siren_{on|off}",
                Scope = EndpointScope.PerDoorbot,
                Destructive = true,
                Physical = true,
                Invoke = (session, target) => session.SetSiren(target.DoorbotId!.Value, true, SirenDurationSeconds),
                PrepareRestore = target => new RestorePlan(
                    "unknown (no read-path for current siren state via this client) - restoring to off as a safe default",
                    WasCaptured: false,
                    Restore: session => session.SetSiren(target.DoorbotId!.Value, false))
            },
            new()
            {
                Key = "test-chime-sound",
                DisplayName = "Play chime test sound",
                Description = "Plays a test sound ('ding') on a Ring chime. Audibly triggers the hardware.",
                SessionMethod = "Session.TestChimeSound(chimeId)",
                HttpMethod = "POST",
                ApiPath = "https://api.ring.com/clients_api/chimes/{chimeId}/play_sound",
                Scope = EndpointScope.PerChime,
                Destructive = true,
                Physical = true,
                NoRestoreReason = "not applicable: plays a momentary sound, doesn't change a persisted setting.",
                Invoke = (session, target) => session.TestChimeSound((int)target.ChimeId!.Value)
            },
            new()
            {
                Key = "set-volume",
                DisplayName = "Set doorbell/camera volume",
                Description = "Sets the ringtone/notification volume to test it, then restores the value captured from the devices listing beforehand. Requires --volume-level (0-11); skipped if not provided.",
                SessionMethod = "Session.SetVolume(doorbotId, volume)",
                HttpMethod = "PUT",
                ApiPath = "https://api.ring.com/clients_api/doorbots/{doorbotId}",
                Scope = EndpointScope.PerDoorbot,
                Destructive = true,
                Invoke = (session, target) => VolumeLevel.HasValue
                    ? session.SetVolume(target.DoorbotId!.Value, VolumeLevel.Value)
                    : throw new InvalidOperationException("set-volume requires --volume-level <0-11>"),
                PrepareRestore = target => OriginalDoorbotSettings.TryGetValue(target.DoorbotId!.Value, out var snap) && snap is { Volume: { } volume }
                    ? new RestorePlan($"doorbell_volume={volume}", WasCaptured: true, Restore: session => session.SetVolume(target.DoorbotId!.Value, volume))
                    : null
            },
            new()
            {
                Key = "set-motion-detection",
                DisplayName = "Toggle motion detection",
                Description = "Disables motion detection to test it, then restores the value captured from the devices listing beforehand (falls back to enabled if that wasn't present).",
                SessionMethod = "Session.SetMotionDetection(doorbotId, enabled)",
                HttpMethod = "PATCH",
                ApiPath = "https://api.ring.com/devices/v1/devices/{doorbotId}/settings",
                Scope = EndpointScope.PerDoorbot,
                Destructive = true,
                Invoke = (session, target) => session.SetMotionDetection(target.DoorbotId!.Value, false),
                PrepareRestore = target =>
                {
                    var captured = OriginalDoorbotSettings.TryGetValue(target.DoorbotId!.Value, out var snap) && snap.MotionDetectionEnabled.HasValue;
                    var original = captured ? snap!.MotionDetectionEnabled!.Value : true;
                    var description = captured
                        ? $"motion_detection_enabled={original}"
                        : "unknown (not present in the devices listing for this doorbot) - restoring to enabled as a safe default";
                    return new RestorePlan(description, captured, session => session.SetMotionDetection(target.DoorbotId!.Value, original));
                }
            },
            new()
            {
                Key = "set-chime-type",
                DisplayName = "Set existing chime type",
                Description = "Sets the wired chime type (0=Mechanical, 1=Digital, 2=Not Present) to test it, then restores the values captured from the devices listing beforehand. Requires --chime-type-value; skipped if not provided.",
                SessionMethod = "Session.SetChimeType(doorbotId, type)",
                HttpMethod = "PUT",
                ApiPath = "https://api.ring.com/clients_api/doorbots/{doorbotId}",
                Scope = EndpointScope.PerDoorbot,
                Destructive = true,
                Invoke = (session, target) => ChimeTypeValue.HasValue
                    ? session.SetChimeType(target.DoorbotId!.Value, ChimeTypeValue.Value)
                    : throw new InvalidOperationException("set-chime-type requires --chime-type-value <0|1|2>"),
                PrepareRestore = target => OriginalDoorbotSettings.TryGetValue(target.DoorbotId!.Value, out var snap) && snap is { ChimeType: { } chimeType }
                    ? new RestorePlan(
                        $"chime_settings.type={chimeType}, enable={snap.ChimeEnabled}, duration={snap.ChimeDuration}",
                        WasCaptured: true,
                        Restore: session => session.SetChimeType(target.DoorbotId!.Value, chimeType, snap.ChimeEnabled, snap.ChimeDuration))
                    : null
            },
            new()
            {
                Key = "set-do-not-disturb",
                DisplayName = "Snooze chime notifications",
                Description = "Snoozes a chime's notifications for --dnd-seconds (default 60). Self-reverts after that duration.",
                SessionMethod = "Session.SetDoNotDisturb(chimeId, seconds)",
                HttpMethod = "PUT",
                ApiPath = "https://api.ring.com/clients_api/chimes/{chimeId}/do_not_disturb",
                Scope = EndpointScope.PerChime,
                Destructive = true,
                NoRestoreReason = "not applicable: self-reverts after --dnd-seconds elapses, nothing to restore immediately.",
                Invoke = (session, target) => session.SetDoNotDisturb(target.ChimeId!.Value, DndSeconds)
            },
            new()
            {
                Key = "set-night-mode",
                DisplayName = "Toggle forced night mode",
                Description = "Forces night mode on to test it, then restores the value captured from the devices listing beforehand.",
                SessionMethod = "Session.SetNightMode(doorbotId, enabled)",
                HttpMethod = "PUT",
                ApiPath = "https://api.ring.com/clients_api/doorbots/{doorbotId}",
                Scope = EndpointScope.PerDoorbot,
                Destructive = true,
                Invoke = (session, target) => session.SetNightMode(target.DoorbotId!.Value, true),
                PrepareRestore = target =>
                {
                    var captured = OriginalDoorbotSettings.TryGetValue(target.DoorbotId!.Value, out var snap) && snap.NightModeEnabled.HasValue;
                    var original = captured ? snap!.NightModeEnabled!.Value : false;
                    var description = captured
                        ? $"night_mode={original}"
                        : "unknown (not present in the devices listing for this doorbot) - restoring to disabled as a safe default";
                    return new RestorePlan(description, captured, session => session.SetNightMode(target.DoorbotId!.Value, original));
                }
            },
            new()
            {
                Key = "set-location-mode",
                DisplayName = "Set location mode (arm/disarm)",
                Description = "Reads the location's current mode, sets it to --location-mode-value to test it, then restores the mode that was read beforehand. Requires --location-mode-value; skipped if not provided. Briefly changes real security/arming state.",
                SessionMethod = "Session.GetLocationMode(locationId) + Session.SetLocationMode(locationId, mode)",
                HttpMethod = "GET, then POST",
                ApiPath = "https://api.ring.com/rs/mode/location/{locationId}",
                Scope = EndpointScope.PerLocation,
                Destructive = true,
                Invoke = async (session, target) =>
                {
                    if (string.IsNullOrWhiteSpace(LocationModeValue))
                    {
                        throw new InvalidOperationException("set-location-mode requires --location-mode-value <home|away|disarmed>");
                    }
                    // Capture the true current mode immediately before mutating it, so
                    // PrepareRestore can put back exactly what was there - not a guess.
                    var current = await session.GetLocationMode(target.LocationId!.Value);
                    if (!string.IsNullOrWhiteSpace(current?.Mode))
                    {
                        OriginalLocationModeByLocation[target.LocationId!.Value] = current!.Mode;
                    }
                    await session.SetLocationMode(target.LocationId!.Value, LocationModeValue!);
                },
                PrepareRestore = target => OriginalLocationModeByLocation.TryGetValue(target.LocationId!.Value, out var mode) && !string.IsNullOrWhiteSpace(mode)
                    ? new RestorePlan($"mode={mode}", WasCaptured: true, Restore: session => session.SetLocationMode(target.LocationId!.Value, mode))
                    : null
            },
            new()
            {
                Key = "set-motion-zones",
                DisplayName = "Set motion zones",
                Description = "Overwrites a doorbot's motion zone configuration. Requires a full zone payload this CLI cannot safely generate - always skipped. Included for completeness/discoverability only; use the client library directly if you need this.",
                SessionMethod = "Session.SetMotionZones(doorbotId, zones)",
                HttpMethod = "PATCH",
                ApiPath = "https://api.ring.com/clients_api/doorbots/{doorbotId}/motion_zones",
                Scope = EndpointScope.PerDoorbot,
                Destructive = true,
                NoRestoreReason = "not applicable: this endpoint always refuses to run (see its own error), so nothing is ever changed to restore.",
                Invoke = (_, _) => throw new InvalidOperationException(
                    "set-motion-zones is not executable through ApiTester: it would overwrite the doorbot's entire motion zone configuration with an empty/placeholder payload, which is a real data loss risk. Use KoenZomers.Ring.Api directly with a deliberately constructed zone payload instead.")
            },
            new()
            {
                Key = "share-recording",
                DisplayName = "Create shared recording link",
                Description = "Creates a public share link for a recording. Requires --ding-id; skipped if not provided.",
                SessionMethod = "Session.ShareRecording(dingId)",
                HttpMethod = "GET",
                ApiPath = "https://api.ring.com/clients_api/dings/{dingId}/share/share",
                Scope = EndpointScope.None,
                Destructive = true,
                NoRestoreReason = "not applicable: additive (creates a new share link) rather than overwriting an existing setting, and this client exposes no unshare/revoke call to undo it with.",
                Invoke = (session, _) => !string.IsNullOrWhiteSpace(DingId)
                    ? session.ShareRecording(DingId!)
                    : throw new InvalidOperationException("share-recording requires --ding-id <id>")
            },
            new()
            {
                Key = "subscribe-ding-events",
                DisplayName = "Subscribe to ding push events",
                Description = "Subscribes this account to ding push notifications for a doorbot. Idempotent toggle.",
                SessionMethod = "Session.SubscribeToDingEvents(doorbotId)",
                HttpMethod = "POST",
                ApiPath = "https://api.ring.com/clients_api/doorbots/{doorbotId}/subscribe",
                Scope = EndpointScope.PerDoorbot,
                Destructive = true,
                NoRestoreReason = "not applicable: idempotent toggle - rerun unsubscribe-ding-events if you need to revert.",
                Invoke = (session, target) => session.SubscribeToDingEvents(target.DoorbotId!.Value)
            },
            new()
            {
                Key = "unsubscribe-ding-events",
                DisplayName = "Unsubscribe from ding push events",
                Description = "Unsubscribes this account from ding push notifications for a doorbot. Idempotent toggle.",
                SessionMethod = "Session.UnsubscribeFromDingEvents(doorbotId)",
                HttpMethod = "POST",
                ApiPath = "https://api.ring.com/clients_api/doorbots/{doorbotId}/unsubscribe",
                Scope = EndpointScope.PerDoorbot,
                Destructive = true,
                NoRestoreReason = "not applicable: idempotent toggle - rerun subscribe-ding-events if you need to revert.",
                Invoke = (session, target) => session.UnsubscribeFromDingEvents(target.DoorbotId!.Value)
            },
            new()
            {
                Key = "subscribe-motion-events",
                DisplayName = "Subscribe to motion push events",
                Description = "Subscribes this account to motion push notifications for a doorbot. Idempotent toggle.",
                SessionMethod = "Session.SubscribeToMotionEvents(doorbotId)",
                HttpMethod = "POST",
                ApiPath = "https://api.ring.com/clients_api/doorbots/{doorbotId}/motions_subscribe",
                Scope = EndpointScope.PerDoorbot,
                Destructive = true,
                NoRestoreReason = "not applicable: idempotent toggle - rerun unsubscribe-motion-events if you need to revert.",
                Invoke = (session, target) => session.SubscribeToMotionEvents(target.DoorbotId!.Value)
            },
            new()
            {
                Key = "unsubscribe-motion-events",
                DisplayName = "Unsubscribe from motion push events",
                Description = "Unsubscribes this account from motion push notifications for a doorbot. Idempotent toggle.",
                SessionMethod = "Session.UnsubscribeFromMotionEvents(doorbotId)",
                HttpMethod = "POST",
                ApiPath = "https://api.ring.com/clients_api/doorbots/{doorbotId}/motions_unsubscribe",
                Scope = EndpointScope.PerDoorbot,
                Destructive = true,
                NoRestoreReason = "not applicable: idempotent toggle - rerun subscribe-motion-events if you need to revert.",
                Invoke = (session, target) => session.UnsubscribeFromMotionEvents(target.DoorbotId!.Value)
            },
            new()
            {
                Key = "enable-location-modes",
                DisplayName = "Enable Location Modes",
                Description = "Enables Location Modes for a location that doesn't have them set up yet. Requires --destructive.",
                SessionMethod = "Session.EnableLocationModes(locationId)",
                HttpMethod = "POST",
                ApiPath = "https://api.ring.com/rs/mode/location/{locationId}/settings/setup",
                Scope = EndpointScope.PerLocation,
                Destructive = true,
                NoRestoreReason = "not applicable: one-time account setup action, not a toggle to revert - use disable-location-modes deliberately if you need to undo it.",
                Invoke = (session, target) => session.EnableLocationModes(target.LocationId!.Value)
            },
            new()
            {
                Key = "disable-location-modes",
                DisplayName = "Disable Location Modes",
                Description = "Would disable Location Modes for a location, discarding its per-mode configuration. Always refuses to run - no safe way to auto-restore what was disabled. Included for completeness/discoverability only.",
                SessionMethod = "Session.DisableLocationModes(locationId)",
                HttpMethod = "DELETE",
                ApiPath = "https://api.ring.com/rs/mode/location/{locationId}/settings",
                Scope = EndpointScope.PerLocation,
                Destructive = true,
                NoRestoreReason = "not applicable: this endpoint always refuses to run (see its own error), so nothing is ever changed to restore.",
                Invoke = (_, _) => throw new InvalidOperationException(
                    "disable-location-modes is not executable through ApiTester: it would discard the location's entire Location Mode configuration with no way for this tool to safely restore it afterward. Use KoenZomers.Ring.Api directly if you deliberately want this.")
            },
            new()
            {
                Key = "set-location-mode-settings",
                DisplayName = "Set Location Mode settings",
                Description = "Would overwrite a location's Location Mode settings. Requires a full settings payload this CLI cannot safely generate - always refuses to run. Included for completeness/discoverability only.",
                SessionMethod = "Session.SetLocationModeSettings(locationId, settingsJson)",
                HttpMethod = "POST",
                ApiPath = "https://api.ring.com/rs/mode/location/{locationId}/settings",
                Scope = EndpointScope.PerLocation,
                Destructive = true,
                NoRestoreReason = "not applicable: this endpoint always refuses to run (see its own error), so nothing is ever changed to restore.",
                Invoke = (_, _) => throw new InvalidOperationException(
                    "set-location-mode-settings is not executable through ApiTester: it would overwrite the location's entire mode settings with an empty/placeholder payload. Use KoenZomers.Ring.Api directly with a deliberately constructed payload instead.")
            },
            new()
            {
                Key = "set-location-mode-sharing",
                DisplayName = "Set Location Mode sharing",
                Description = "Would overwrite a location's Location Mode sharing configuration. Requires a full payload this CLI cannot safely generate - always refuses to run. Included for completeness/discoverability only.",
                SessionMethod = "Session.SetLocationModeSharing(locationId, sharingJson)",
                HttpMethod = "POST",
                ApiPath = "https://api.ring.com/rs/mode/location/{locationId}/sharing",
                Scope = EndpointScope.PerLocation,
                Destructive = true,
                NoRestoreReason = "not applicable: this endpoint always refuses to run (see its own error), so nothing is ever changed to restore.",
                Invoke = (_, _) => throw new InvalidOperationException(
                    "set-location-mode-sharing is not executable through ApiTester: it would overwrite the location's entire mode sharing configuration with an empty/placeholder payload. Use KoenZomers.Ring.Api directly with a deliberately constructed payload instead.")
            },
            new()
            {
                Key = "update-chime",
                DisplayName = "Update chime settings",
                Description = "Would overwrite a chime's settings. Requires a full settings payload this CLI cannot safely generate - always refuses to run. Included for completeness/discoverability only.",
                SessionMethod = "Session.UpdateChime(chimeId, settingsJson)",
                HttpMethod = "PUT",
                ApiPath = "https://api.ring.com/clients_api/chimes/{chimeId}",
                Scope = EndpointScope.PerChime,
                Destructive = true,
                NoRestoreReason = "not applicable: this endpoint always refuses to run (see its own error), so nothing is ever changed to restore.",
                Invoke = (_, _) => throw new InvalidOperationException(
                    "update-chime is not executable through ApiTester: it would overwrite the chime's settings with an empty/placeholder payload. Use KoenZomers.Ring.Api directly with a deliberately constructed payload instead.")
            },
            new()
            {
                Key = "trigger-alarm",
                DisplayName = "Trigger monitored-asset alarm",
                Description = "Sounds a REAL panic/user alarm for a monitored asset. Requires --asset-uuid (discover via account-monitoring-status); skipped if not provided. Audibly/visibly triggers real security response.",
                SessionMethod = "Session.TriggerAlarm(locationId, assetUuid)",
                HttpMethod = "POST",
                ApiPath = "https://api.ring.com/rs/monitoring/accounts/{locationId}/assets/{assetUuid}/userAlarm",
                Scope = EndpointScope.PerLocation,
                Destructive = true,
                Physical = true,
                NoRestoreReason = "not applicable: triggers a momentary alarm event, doesn't change a persisted setting.",
                Invoke = (session, target) => !string.IsNullOrWhiteSpace(AssetUuid)
                    ? session.TriggerAlarm(target.LocationId!.Value, AssetUuid!)
                    : throw new InvalidOperationException("trigger-alarm requires --asset-uuid <uuid>")
            },
            new()
            {
                Key = "register-push-receiver",
                DisplayName = "Register push notification receiver",
                Description = "Registers a push notification token for this account. Requires --push-token; skipped if not provided.",
                SessionMethod = "Session.RegisterPushReceiver(deviceToken)",
                HttpMethod = "PATCH",
                ApiPath = "https://api.ring.com/clients_api/device",
                Scope = EndpointScope.None,
                Destructive = true,
                NoRestoreReason = "not applicable: this client exposes no unregister call, and overwriting with a fresh token on every run is the normal usage pattern rather than a one-off change to revert.",
                Invoke = (session, _) => !string.IsNullOrWhiteSpace(PushToken)
                    ? session.RegisterPushReceiver(PushToken!)
                    : throw new InvalidOperationException("register-push-receiver requires --push-token <token>")
            },
            new()
            {
                Key = "unlock-intercom",
                DisplayName = "Unlock Intercom",
                Description = "Unlocks a Ring Intercom device. Triggers a REAL, physical door unlock. Requires --doorbot-id (the Intercom's device id).",
                SessionMethod = "Session.Unlock(deviceId)",
                HttpMethod = "PUT",
                ApiPath = "https://api.ring.com/commands/v1/devices/{deviceId}/device_rpc",
                Scope = EndpointScope.PerDoorbot,
                Destructive = true,
                Physical = true,
                NoRestoreReason = "not applicable: Ring Intercom auto re-locks after its own timeout; this client exposes no immediate re-lock command to call.",
                Invoke = (session, target) => session.Unlock(target.DoorbotId!.Value)
            },
        };

        // Ambient parameters threaded in by Program from CLI options before Runner.RunAsync runs -
        // the Invoke delegate signature only carries a Session and a location/doorbot/chime target,
        // so anything else an endpoint needs (a limit, a value to set, an id to act on) is read from
        // here instead of being wedged into EndpointTarget.
        public static int CurrentHistoryLimit { get; set; } = 5;
        public static int SirenDurationSeconds { get; set; } = 2;
        public static int? VolumeLevel { get; set; }
        public static int? ChimeTypeValue { get; set; }
        public static int DndSeconds { get; set; } = 60;
        public static string? LocationModeValue { get; set; }
        public static string? DingId { get; set; }
        public static string? AssetUuid { get; set; }
        public static string? PushToken { get; set; }

        public static EndpointDescriptor? Find(string key) =>
            All.FirstOrDefault(e => string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase));
    }
}
