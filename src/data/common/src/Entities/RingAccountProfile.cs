namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// Ring account profile (clients_api/profile). Every profile field is stored except the
    /// authentication token, which is a credential and is deliberately never persisted.
    /// </summary>
    public class RingAccountProfile
    {
        public Guid Id { get; set; }
        public Guid RingAccountId { get; set; }
        public int ProviderUserId { get; set; }
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? FirstNameExtra { get; set; }
        public string? LastNameExtra { get; set; }
        public string? PhoneNumber { get; set; }
        public string? TfaPhoneNumber { get; set; }
        public bool? TfaEnabled { get; set; }
        public bool? CfesEnrolled { get; set; }
        public string? HardwareId { get; set; }
        public string? ExplorerProgramTerms { get; set; }
        public string? UserFlow { get; set; }
        public string? AppBrand { get; set; }
        public string? Country { get; set; }
        public string? Status { get; set; }
        /// <summary>The created_at timestamp exactly as Ring sends it.</summary>
        public string? CreatedAt { get; set; }
        public string? AccountType { get; set; }
        public string? TsvState { get; set; }
        public RingAccountSessionFeatures? SessionFeatures { get; set; }
        public List<RingAccountProfileFlag> Flags { get; set; } = [];
    }
}
