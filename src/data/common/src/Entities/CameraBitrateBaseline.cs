namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Time-bucketed baseline statistics for camera bitrate and RTP quality metrics, used to detect interference.</summary>
    public class CameraBitrateBaseline
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }
        public int HourOfDay { get; set; }
        public bool IsWeekend { get; set; }
        public int SampleCount { get; set; }
        public long MedianBitrateBps { get; set; }
        public double StdDevBitrateBps { get; set; }
        public double MedianFractionLost { get; set; }
        public double MedianJitterTicks { get; set; }
        public DateTime LastRecomputedAtUtc { get; set; }
    }
}
