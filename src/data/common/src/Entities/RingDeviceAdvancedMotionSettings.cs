namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// Ring-only scalar settings from the settings.advanced_motion_settings object (zones 1 to 3, each with a name, a state and eight vertices). 1:1 with the device, replaced on each sync.
    /// Every column is null when Ring did not send the key. Typed columns are named after the Ring key path.
    /// </summary>
    public class RingDeviceAdvancedMotionSettings
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_1.name.</summary>
        public string? Zone1Name { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_1.state.</summary>
        public int? Zone1State { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_1.vertex1.x.</summary>
        public int? Zone1Vertex1X { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_1.vertex1.y.</summary>
        public double? Zone1Vertex1Y { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_1.vertex2.x.</summary>
        public double? Zone1Vertex2X { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_1.vertex2.y.</summary>
        public double? Zone1Vertex2Y { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_1.vertex3.x.</summary>
        public double? Zone1Vertex3X { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_1.vertex3.y.</summary>
        public double? Zone1Vertex3Y { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_1.vertex4.x.</summary>
        public int? Zone1Vertex4X { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_1.vertex4.y.</summary>
        public double? Zone1Vertex4Y { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_1.vertex5.x.</summary>
        public int? Zone1Vertex5X { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_1.vertex5.y.</summary>
        public double? Zone1Vertex5Y { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_1.vertex6.x.</summary>
        public double? Zone1Vertex6X { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_1.vertex6.y.</summary>
        public double? Zone1Vertex6Y { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_1.vertex7.x.</summary>
        public double? Zone1Vertex7X { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_1.vertex7.y.</summary>
        public double? Zone1Vertex7Y { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_1.vertex8.x.</summary>
        public int? Zone1Vertex8X { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_1.vertex8.y.</summary>
        public double? Zone1Vertex8Y { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_2.name.</summary>
        public string? Zone2Name { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_2.state.</summary>
        public int? Zone2State { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_2.vertex1.x.</summary>
        public int? Zone2Vertex1X { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_2.vertex1.y.</summary>
        public int? Zone2Vertex1Y { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_2.vertex2.x.</summary>
        public int? Zone2Vertex2X { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_2.vertex2.y.</summary>
        public int? Zone2Vertex2Y { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_2.vertex3.x.</summary>
        public int? Zone2Vertex3X { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_2.vertex3.y.</summary>
        public int? Zone2Vertex3Y { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_2.vertex4.x.</summary>
        public int? Zone2Vertex4X { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_2.vertex4.y.</summary>
        public int? Zone2Vertex4Y { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_2.vertex5.x.</summary>
        public int? Zone2Vertex5X { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_2.vertex5.y.</summary>
        public int? Zone2Vertex5Y { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_2.vertex6.x.</summary>
        public int? Zone2Vertex6X { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_2.vertex6.y.</summary>
        public int? Zone2Vertex6Y { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_2.vertex7.x.</summary>
        public int? Zone2Vertex7X { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_2.vertex7.y.</summary>
        public int? Zone2Vertex7Y { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_2.vertex8.x.</summary>
        public int? Zone2Vertex8X { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_2.vertex8.y.</summary>
        public int? Zone2Vertex8Y { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_3.name.</summary>
        public string? Zone3Name { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_3.state.</summary>
        public int? Zone3State { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_3.vertex1.x.</summary>
        public int? Zone3Vertex1X { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_3.vertex1.y.</summary>
        public int? Zone3Vertex1Y { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_3.vertex2.x.</summary>
        public int? Zone3Vertex2X { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_3.vertex2.y.</summary>
        public int? Zone3Vertex2Y { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_3.vertex3.x.</summary>
        public int? Zone3Vertex3X { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_3.vertex3.y.</summary>
        public int? Zone3Vertex3Y { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_3.vertex4.x.</summary>
        public int? Zone3Vertex4X { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_3.vertex4.y.</summary>
        public int? Zone3Vertex4Y { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_3.vertex5.x.</summary>
        public int? Zone3Vertex5X { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_3.vertex5.y.</summary>
        public int? Zone3Vertex5Y { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_3.vertex6.x.</summary>
        public int? Zone3Vertex6X { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_3.vertex6.y.</summary>
        public int? Zone3Vertex6Y { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_3.vertex7.x.</summary>
        public int? Zone3Vertex7X { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_3.vertex7.y.</summary>
        public int? Zone3Vertex7Y { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_3.vertex8.x.</summary>
        public int? Zone3Vertex8X { get; set; }

        /// <summary>settings.advanced_motion_settings.zone_3.vertex8.y.</summary>
        public int? Zone3Vertex8Y { get; set; }

        /// <summary>UTC time this row was written by a sync (not a provider value).</summary>
        public DateTime UpdatedAtUtc { get; set; }
    }
}
