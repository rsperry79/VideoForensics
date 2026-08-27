namespace VideoForensics.Data.Common.Entities
{
    /// <summary>An encrypted credential (password or token) for a provider account.</summary>
    public class Credential
    {
        public Guid Id { get; set; }
        public Guid ProviderAccountId { get; set; }
        public required string CredentialType { get; set; }
        public required string EncryptedValue { get; set; }
        public required string EncryptionProvider { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime? RotatedUtc { get; set; }
    }
}
