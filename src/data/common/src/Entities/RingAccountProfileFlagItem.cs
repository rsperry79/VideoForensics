namespace VideoForensics.Data.Common.Entities
{
    /// <summary>One element of an array-valued <see cref="RingAccountProfileFlag"/>.</summary>
    public class RingAccountProfileFlagItem
    {
        public Guid Id { get; set; }
        public Guid RingAccountProfileFlagId { get; set; }
        public int Position { get; set; }
        /// <summary>The element as text; non-string elements are kept as raw JSON.</summary>
        public string Value { get; set; } = string.Empty;
    }
}
