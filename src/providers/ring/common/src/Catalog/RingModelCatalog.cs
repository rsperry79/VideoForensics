using System;
using System.Collections.Generic;
using System.Linq;

using VideoForensics.Providers.Ring.Interfaces;

namespace VideoForensics.Providers.Ring.Catalog;

/// <summary>
/// Static Ring kind catalog. Kinds and their names come from the ring-client-api device type enum and from product-name
/// mappings in python-ring-doorbell. Rows without a product name are kept with a null ModelName rather than guessed.
/// </summary>
public sealed class RingModelCatalog : IRingModelCatalog
{
    /// <summary>
    /// Data version of this catalog. Bump it whenever an entry changes so databases re-seed on the next startup
    /// (see <c>ReferenceDataSeeder</c>); a higher version is the only thing that triggers a reload.
    /// </summary>
    public const int Version = 4;

    private static readonly RingModelInfo[] Entries =
    [
        // Doorbells. Only cocoa_doorbell_v3, cocoa_doorbell_v5, cocoa_doorbell_v6 and doorbell_tahoe have a sourced product name.
        new("doorbot", RingDeviceFamily.Doorbell, null, null, false),
        new("doorbell", RingDeviceFamily.Doorbell, null, null, false),
        new("doorbell_v3", RingDeviceFamily.Doorbell, null, null, false),
        new("doorbell_v4", RingDeviceFamily.Doorbell, null, null, false),
        // Video Doorbell 4 (Ring support page: 1080p HD). The kind is the one Front Door reports; confirmed by the device owner.
        new("doorbell_graham_cracker", RingDeviceFamily.Doorbell, "Video Doorbell 4", null, false, "1920x1080"),
        new("doorbell_v5", RingDeviceFamily.Doorbell, null, null, false),
        new("doorbell_portal", RingDeviceFamily.Doorbell, null, null, false),
        new("doorbell_scallop", RingDeviceFamily.Doorbell, null, null, false),
        new("doorbell_scallop_lite", RingDeviceFamily.Doorbell, null, null, false),
        new("cocoa_doorbell", RingDeviceFamily.Doorbell, null, null, false),
        new("cocoa_doorbell_v3", RingDeviceFamily.Doorbell, "Doorbell (2nd Gen)", "2nd Gen", false),
        new("cocoa_doorbell_v5", RingDeviceFamily.Doorbell, "Wired Doorbell 4K Pro", null, false),
        new("cocoa_doorbell_v6", RingDeviceFamily.Doorbell, "Battery Doorbell 4K Pro", null, false),
        new("doorbell_tahoe", RingDeviceFamily.Doorbell, "Wired Video Doorbell Retinal 2K", null, false),
        new("doorbell_oyster", RingDeviceFamily.Doorbell, "Video Doorbell 4", null, false, "1920x1080"),

        // Cameras.
        new("stickup_cam", RingDeviceFamily.Camera, null, null, false),
        new("stickup_cam_v3", RingDeviceFamily.Camera, null, null, false),
        new("stickup_cam_v4", RingDeviceFamily.Camera, null, null, false),
        new("stickup_cam_elite", RingDeviceFamily.Camera, null, null, false, "1920x1080"),
        new("stickup_cam_lunar", RingDeviceFamily.Camera, null, null, false),
        new("stickup_cam_mini", RingDeviceFamily.Camera, null, null, false),
        new("stickup_cam_mini_v3", RingDeviceFamily.Camera, "Indoor Cam Plus", null, false, "2560x1440"),
        new("cocoa_camera", RingDeviceFamily.Camera, null, null, false),
        new("cocoa_camera_v2", RingDeviceFamily.Camera, null, null, false),
        new("cocoa_camera_v3", RingDeviceFamily.Camera, null, null, false),
        new("cocoa_spotlight_v2", RingDeviceFamily.Camera, "Spotlight Cam 4K Pro", null, false),
        new("cocoa_spotlight", RingDeviceFamily.Camera, "Spotlight Cam Plus", null, false, "1920x1080"),
        new("cocoa_floodlight_v2", RingDeviceFamily.Camera, null, null, false),
        new("floodlight_v1", RingDeviceFamily.Camera, null, null, false),
        new("floodlight_v2", RingDeviceFamily.Camera, null, null, false),
        new("spotlightw_v2", RingDeviceFamily.Camera, null, null, false),
        new("hp_cam_v1", RingDeviceFamily.Camera, null, null, false),
        new("hp_cam_v2", RingDeviceFamily.Camera, null, null, false),
        new("hexa_camera_2_v1", RingDeviceFamily.Camera, "Elite 140 4K", null, false),
        new("hexa_camera_6_v1", RingDeviceFamily.Camera, "Elite 360 4K", null, false),

        // Chimes. The Chime Pro is the Wi-Fi bridge; its generation is not sourced for either kind.
        new("chime", RingDeviceFamily.Chime, null, null, false),
        new("chime_v2", RingDeviceFamily.Chime, null, null, false),
        new("chime_v4", RingDeviceFamily.Chime, "Chime (2nd Gen)", "2nd Gen", false),
        new("chime_pro", RingDeviceFamily.Chime, null, null, true),
        new("chime_pro_v2", RingDeviceFamily.Chime, null, null, true),

        // CCTV Database Ring list (cctv-database.com/brand/ring). Kinds are the CSV RingKind strings; not yet verified against API kinds.
        new("Doorbell", RingDeviceFamily.Doorbell, "Video Doorbell (2nd Gen)", null, false, "1920x1080"),
        new("Doorbell2", RingDeviceFamily.Doorbell, "Video Doorbell (2nd Gen)", null, false, "1920x1080"),
        new("Doorbell3", RingDeviceFamily.Doorbell, "Video Doorbell 3", null, false, "1920x1080"),
        new("Doorbell3Plus", RingDeviceFamily.Doorbell, "Video Doorbell 3 Plus", null, false, "1920x1080"),
        new("Doorbell4", RingDeviceFamily.Doorbell, "Video Doorbell 4", null, false, "1920x1080"),
        new("DoorbellPro", RingDeviceFamily.Doorbell, "Video Doorbell Pro", null, false, "1920x1080"),
        new("DoorbellPro2", RingDeviceFamily.Doorbell, "Video Doorbell Pro 2", null, false, "2048x1536"),
        new("DoorbellElite", RingDeviceFamily.Doorbell, "Doorbell Elite", null, false, "1920x1080"),
        new("DoorbellWired", RingDeviceFamily.Doorbell, "Video Doorbell Wired", null, false, "1920x1080"),
        new("BatteryDoorbell", RingDeviceFamily.Doorbell, "Battery Doorbell (2nd Gen)", null, false, "2560x1440"),
        new("BatteryDoorbellPlus", RingDeviceFamily.Doorbell, "Battery Doorbell Plus (2nd Gen)", null, false, "2560x1440"),
        new("BatteryDoorbellPro", RingDeviceFamily.Doorbell, "Battery Doorbell Pro", null, false, "2048x1536"),
        new("PeepholeCam", RingDeviceFamily.Camera, "Peephole Cam", null, false, "1920x1080"),
        new("StickUpCamBattery", RingDeviceFamily.Camera, "Stick Up Cam Battery (3rd Gen)", null, false, "1920x1080"),
        new("StickUpCamPlugIn", RingDeviceFamily.Camera, "Stick Up Cam Plug-In", null, false, "1920x1080"),
        new("StickUpCamElite", RingDeviceFamily.Camera, "Stick Up Cam Elite", null, false, "1920x1080"),
        new("StickUpCamPro", RingDeviceFamily.Camera, "Stick Up Cam Pro", null, false, "1920x1080"),
        new("SpotlightCam", RingDeviceFamily.Camera, "Spotlight Cam (2nd Gen)", null, false, "2560x1440"),
        new("SpotlightCamBattery", RingDeviceFamily.Camera, "Spotlight Cam Plus (Battery)", null, false, "1920x1080"),
        new("SpotlightCamPlugIn", RingDeviceFamily.Camera, "Spotlight Cam Plus (Plug-In)", null, false, "1920x1080"),
        new("SpotlightCamSolar", RingDeviceFamily.Camera, "Spotlight Cam Plus (Solar)", null, false, "1920x1080"),
        new("SpotlightCamPro", RingDeviceFamily.Camera, "Spotlight Cam Pro", null, false, "1920x1080"),
        new("FloodlightCam", RingDeviceFamily.Camera, "Floodlight Cam (2nd Gen)", null, false, "2560x1440"),
        new("FloodlightCamPlus", RingDeviceFamily.Camera, "Floodlight Cam Wired Plus", null, false, "1920x1080"),
        new("FloodlightCamPro", RingDeviceFamily.Camera, "Floodlight Cam Pro (2nd Gen)", null, false, "3840x2160"),
        new("IndoorCam", RingDeviceFamily.Camera, "Indoor Cam (2nd Gen)", null, false, "1920x1080"),
        new("IndoorCamPlus", RingDeviceFamily.Camera, "Indoor Cam Plus", null, false, "2560x1440"),
        new("PanTiltIndoorCam", RingDeviceFamily.Camera, "Pan-Tilt Indoor Cam", null, false, "1920x1080"),
        new("OutdoorCam", RingDeviceFamily.Camera, "Outdoor Cam (2nd Gen)", null, false, "2560x1440"),
        new("OutdoorCamPlus", RingDeviceFamily.Camera, "Outdoor Cam Plus", null, false, "2560x1440"),
        new("OutdoorCamPro", RingDeviceFamily.Camera, "Outdoor Cam Pro 4K", null, false, "3840x2160"),
        new("DomeCam", RingDeviceFamily.Camera, "Dome Cam 4K", null, false, "3840x2160"),
    ];

    private static readonly Dictionary<string, RingModelInfo> ByKind =
        Entries.ToDictionary(e => e.Kind, StringComparer.Ordinal);

    /// <inheritdoc />
    public IReadOnlyList<RingModelInfo> All => Entries;

    /// <inheritdoc />
    public RingModelInfo? Find(string? kind)
    {
        return string.IsNullOrWhiteSpace(kind) ? null : ByKind.TryGetValue(kind, out RingModelInfo? info) ? info : null;
    }
}
