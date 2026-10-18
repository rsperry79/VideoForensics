namespace VideoForensics.Data.Common.Entities
{
    /// <summary>One element of notYetParticipatingInMode, kept as raw JSON until its shape is captured.</summary>
    public class RingLocationModeNotYetParticipating
    {
        public Guid Id { get; set; }
        public Guid RingLocationModeId { get; set; }
        public int Position { get; set; }
        public string ValueJson { get; set; } = string.Empty;
    }
}
