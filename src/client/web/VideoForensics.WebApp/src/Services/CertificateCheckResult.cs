namespace VideoForensics.Client.WebApp.Services
{
    /// <summary>Outcome of an issuance check. The message never contains key material or tokens.</summary>
    public sealed record CertificateCheckResult(bool Success, bool Issued, string Message);
}
