namespace VideoForensics.Data.Common.Entities
{
    /// <summary>A single telemetry sample captured from an active live-view session's RTP/RTCP stream.</summary>
    public class LiveViewTelemetrySample
    {
        public Guid Id { get; set; }
        public Guid SessionId { get; set; }
        public DateTime CapturedAtUtc { get; set; }
        public byte? FractionLost { get; set; }
        public int? CumulativePacketsLost { get; set; }
        public uint? JitterTicks { get; set; }
        public long? BitrateBps { get; set; }
        public double? InterferenceScore { get; set; }
    }
}
