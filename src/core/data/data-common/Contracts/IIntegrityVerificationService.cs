namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Service for computing and verifying file integrity via SHA-256 hashing.</summary>
    public interface IIntegrityVerificationService
    {
        /// <summary>Computes the SHA-256 hash of a file.</summary>
        Task<string> ComputeHashAsync(string filePath, CancellationToken ct);

        /// <summary>Verifies a media item file against its stored hash, recording the result in IntegrityRecord.</summary>
        Task<bool> VerifyAsync(Guid mediaItemId, CancellationToken ct);

        /// <summary>Verifies all media items for a device, recording results in IntegrityRecord.</summary>
        Task<int> VerifyAllForDeviceAsync(Guid deviceId, CancellationToken ct);
    }
}
