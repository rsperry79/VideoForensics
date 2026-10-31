namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// Ring-only scalar settings from the settings.video_settings object (image controls, clip lengths, encryption and the ignore zones). 1:1 with the device, replaced on each sync.
    /// Every column is null when Ring did not send the key. Typed columns are named after the Ring key path.
    /// </summary>
    public class RingDeviceVideoSettings
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }

        /// <summary>settings.video_settings.abc_enabled.</summary>
        public bool? AbcEnabled { get; set; }

        /// <summary>settings.video_settings.ae_level.</summary>
        public int? AeLevel { get; set; }

        /// <summary>settings.video_settings.ae_mode.</summary>
        public int? AeMode { get; set; }

        /// <summary>settings.video_settings.aspect_ratio.</summary>
        public string? AspectRatio { get; set; }

        /// <summary>settings.video_settings.auto_clip_length_enabled.</summary>
        public bool? AutoClipLengthEnabled { get; set; }

        /// <summary>settings.video_settings.brightness.</summary>
        public int? Brightness { get; set; }

        /// <summary>settings.video_settings.clip_length_max.</summary>
        public int? ClipLengthMax { get; set; }

        /// <summary>settings.video_settings.clip_length_min.</summary>
        public int? ClipLengthMin { get; set; }

        /// <summary>settings.video_settings.contrast.</summary>
        public int? Contrast { get; set; }

        /// <summary>settings.video_settings.dynamic_resolution_enabled.</summary>
        public bool? DynamicResolutionEnabled { get; set; }

        /// <summary>settings.video_settings.exposure_control.</summary>
        public int? ExposureControl { get; set; }

        /// <summary>settings.video_settings.hdr_enable.</summary>
        public bool? HdrEnable { get; set; }

        /// <summary>settings.video_settings.ignore_zones.zone1.name.</summary>
        public string? IgnoreZonesZone1Name { get; set; }

        /// <summary>settings.video_settings.ignore_zones.zone1.state.</summary>
        public int? IgnoreZonesZone1State { get; set; }

        /// <summary>settings.video_settings.ignore_zones.zone1.vertex1.x.</summary>
        public int? IgnoreZonesZone1Vertex1X { get; set; }

        /// <summary>settings.video_settings.ignore_zones.zone1.vertex1.y.</summary>
        public int? IgnoreZonesZone1Vertex1Y { get; set; }

        /// <summary>settings.video_settings.ignore_zones.zone1.vertex2.x.</summary>
        public int? IgnoreZonesZone1Vertex2X { get; set; }

        /// <summary>settings.video_settings.ignore_zones.zone1.vertex2.y.</summary>
        public int? IgnoreZonesZone1Vertex2Y { get; set; }

        /// <summary>settings.video_settings.ignore_zones.zone2.name.</summary>
        public string? IgnoreZonesZone2Name { get; set; }

        /// <summary>settings.video_settings.ignore_zones.zone2.state.</summary>
        public int? IgnoreZonesZone2State { get; set; }

        /// <summary>settings.video_settings.ignore_zones.zone2.vertex1.x.</summary>
        public int? IgnoreZonesZone2Vertex1X { get; set; }

        /// <summary>settings.video_settings.ignore_zones.zone2.vertex1.y.</summary>
        public int? IgnoreZonesZone2Vertex1Y { get; set; }

        /// <summary>settings.video_settings.ignore_zones.zone2.vertex2.x.</summary>
        public int? IgnoreZonesZone2Vertex2X { get; set; }

        /// <summary>settings.video_settings.ignore_zones.zone2.vertex2.y.</summary>
        public int? IgnoreZonesZone2Vertex2Y { get; set; }

        /// <summary>settings.video_settings.ignore_zones.zone3.name.</summary>
        public string? IgnoreZonesZone3Name { get; set; }

        /// <summary>settings.video_settings.ignore_zones.zone3.state.</summary>
        public int? IgnoreZonesZone3State { get; set; }

        /// <summary>settings.video_settings.ignore_zones.zone3.vertex1.x.</summary>
        public int? IgnoreZonesZone3Vertex1X { get; set; }

        /// <summary>settings.video_settings.ignore_zones.zone3.vertex1.y.</summary>
        public int? IgnoreZonesZone3Vertex1Y { get; set; }

        /// <summary>settings.video_settings.ignore_zones.zone3.vertex2.x.</summary>
        public int? IgnoreZonesZone3Vertex2X { get; set; }

        /// <summary>settings.video_settings.ignore_zones.zone3.vertex2.y.</summary>
        public int? IgnoreZonesZone3Vertex2Y { get; set; }

        /// <summary>settings.video_settings.ignore_zones.zone4.name.</summary>
        public string? IgnoreZonesZone4Name { get; set; }

        /// <summary>settings.video_settings.ignore_zones.zone4.state.</summary>
        public int? IgnoreZonesZone4State { get; set; }

        /// <summary>settings.video_settings.ignore_zones.zone4.vertex1.x.</summary>
        public int? IgnoreZonesZone4Vertex1X { get; set; }

        /// <summary>settings.video_settings.ignore_zones.zone4.vertex1.y.</summary>
        public int? IgnoreZonesZone4Vertex1Y { get; set; }

        /// <summary>settings.video_settings.ignore_zones.zone4.vertex2.x.</summary>
        public int? IgnoreZonesZone4Vertex2X { get; set; }

        /// <summary>settings.video_settings.ignore_zones.zone4.vertex2.y.</summary>
        public int? IgnoreZonesZone4Vertex2Y { get; set; }

        /// <summary>settings.video_settings.night_color_enable.</summary>
        public bool? NightColorEnable { get; set; }

        /// <summary>settings.video_settings.retinal_tuning.enabled.</summary>
        public bool? RetinalTuningEnabled { get; set; }

        /// <summary>settings.video_settings.retinal_tuning.user_specified_enabled.</summary>
        public bool? RetinalTuningUserSpecifiedEnabled { get; set; }

        /// <summary>settings.video_settings.saturation.</summary>
        public int? Saturation { get; set; }

        /// <summary>UTC time this row was written by a sync (not a provider value).</summary>
        public DateTime UpdatedAtUtc { get; set; }
    }
}
