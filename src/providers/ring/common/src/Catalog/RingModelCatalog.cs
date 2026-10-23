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
    public const int Version = 2;

    private static readonly RingModelInfo[] Entries =
    [
        // Doorbells. Only cocoa_doorbell_v3, cocoa_doorbell_v5, cocoa_doorbell_v6 and doorbell_tahoe have a sourced product name.
        new("doorbot", RingDeviceFamily.Doorbell, null, null, false),
        new("doorbell", RingDeviceFamily.Doorbell, null, null, false),
        new("doorbell_v3", RingDeviceFamily.Doorbell, null, null, false),
        new("doorbell_v4", RingDeviceFamily.Doorbell, null, null, false),
        new("doorbell_v5", RingDeviceFamily.Doorbell, null, null, false),
        new("doorbell_portal", RingDeviceFamily.Doorbell, null, null, false),
        new("doorbell_scallop", RingDeviceFamily.Doorbell, null, null, false),
        new("doorbell_scallop_lite", RingDeviceFamily.Doorbell, null, null, false),
        new("cocoa_doorbell", RingDeviceFamily.Doorbell, null, null, false),
        new("cocoa_doorbell_v3", RingDeviceFamily.Doorbell, "Doorbell (2nd Gen)", "2nd Gen", false),
        new("cocoa_doorbell_v5", RingDeviceFamily.Doorbell, "Wired Doorbell 4K Pro", null, false),
        new("cocoa_doorbell_v6", RingDeviceFamily.Doorbell, "Battery Doorbell 4K Pro", null, false),
        new("doorbell_tahoe", RingDeviceFamily.Doorbell, "Wired Video Doorbell Retinal 2K", null, false),
        new("doorbell_oyster", RingDeviceFamily.Doorbell, "Video Doorbell 4", null, false),

        // Cameras.
        new("stickup_cam", RingDeviceFamily.Camera, null, null, false),
        new("stickup_cam_v3", RingDeviceFamily.Camera, null, null, false),
        new("stickup_cam_v4", RingDeviceFamily.Camera, null, null, false),
        new("stickup_cam_elite", RingDeviceFamily.Camera, null, null, false),
        new("stickup_cam_lunar", RingDeviceFamily.Camera, null, null, false),
        new("stickup_cam_mini", RingDeviceFamily.Camera, null, null, false),
        new("stickup_cam_mini_v3", RingDeviceFamily.Camera, "Indoor Cam Plus", null, false),
        new("cocoa_camera", RingDeviceFamily.Camera, null, null, false),
        new("cocoa_camera_v2", RingDeviceFamily.Camera, null, null, false),
        new("cocoa_camera_v3", RingDeviceFamily.Camera, null, null, false),
        new("cocoa_spotlight_v2", RingDeviceFamily.Camera, "Spotlight Cam 4K Pro", null, false),
        new("cocoa_spotlight", RingDeviceFamily.Camera, "Spotlight Cam Plus", null, false),
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
