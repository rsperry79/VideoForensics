namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Per-user suppression of a notice - dismissing a notice only hides it for that operator, not everyone.</summary>
    public class NoticeDismissal
    {
        /// <summary>Unique identifier.</summary>
        public Guid Id { get; set; }

        /// <summary>FK to Notice being dismissed.</summary>
        public required Guid NoticeId { get; set; }

        /// <summary>FK to Operator who dismissed it - one dismissal per (NoticeId, OperatorId) pair (Unique index).</summary>
        public required Guid OperatorId { get; set; }

        /// <summary>When the operator dismissed it.</summary>
        public DateTime DismissedUtc { get; set; }
    }
}
