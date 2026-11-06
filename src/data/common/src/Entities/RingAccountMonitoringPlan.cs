namespace VideoForensics.Data.Common.Entities
{
    /// <summary>One compatible monitoring plan of a location's account monitoring status.</summary>
    public class RingAccountMonitoringPlan
    {
        public Guid Id { get; set; }
        public Guid RingAccountMonitoringStatusId { get; set; }
        public int Position { get; set; }
        public string Plan { get; set; } = string.Empty;
    }
}
