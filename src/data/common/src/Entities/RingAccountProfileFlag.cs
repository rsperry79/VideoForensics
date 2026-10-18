namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// One profile value that has no dedicated column: a features entry other than the typed ones, or a
    /// user_preferences entry. Nested objects are flattened into dotted names. Scalars use the typed
    /// value column matching ValueKind; arrays keep their elements in <see cref="RingAccountProfileFlagItem"/>.
    /// </summary>
    public class RingAccountProfileFlag
    {
        public Guid Id { get; set; }
        public Guid RingAccountProfileId { get; set; }
        /// <summary>features, settings or preferences.</summary>
        public string Scope { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        /// <summary>bool, int, decimal, string, list, object or null.</summary>
        public string ValueKind { get; set; } = string.Empty;
        public bool? BoolValue { get; set; }
        public long? IntValue { get; set; }
        public string? StringValue { get; set; }
        public List<RingAccountProfileFlagItem> Items { get; set; } = [];
    }
}
