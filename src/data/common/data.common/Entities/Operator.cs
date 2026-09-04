namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// A local person operating this VideoForensics installation - distinct from the existing
    /// Ring-scoped <see cref="User"/> entity (which represents the Ring account holder, keyed by
    /// ProviderUserKey). Exists for legal/chain-of-custody attribution: a paired identity is really
    /// (Operator, device credential, Role), not just a device, so a shared laptop can host multiple
    /// separately-attributed people (plan §5.11).
    /// </summary>
    public class Operator
    {
        public Guid Id { get; set; }
        public required string DisplayName { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public bool Active { get; set; } = true;
    }
}
