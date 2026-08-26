using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Core.Services
{
    /// <summary>Service for computing and verifying file integrity via SHA-256 hashing.</summary>
    internal class IntegrityVerificationService : IIntegrityVerificationService
    {
        private readonly IMediaItemRepository _mediaItemRepository;
        private readonly ILogger<IntegrityVerificationService> _logger;

        public IntegrityVerificationService(IMediaItemRepository mediaItemRepository, ILogger<IntegrityVerificationService> logger)
        {
            _mediaItemRepository = mediaItemRepository;
            _logger = logger;
        }

        public async Task<string> ComputeHashAsync(string filePath, CancellationToken ct)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    _logger.LogError("File not found for hashing: {FilePath}", filePath);
                    throw new FileNotFoundException($"File not found: {filePath}");
                }

                using var fileStream = File.OpenRead(filePath);
                var hash = await SHA256.HashDataAsync(fileStream, ct);
                var hashHex = Convert.ToHexString(hash).ToLowerInvariant();

                _logger.LogInformation("Computed SHA-256 for {FilePath}: {Hash}", filePath, hashHex);
                return hashHex;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error computing hash for file {FilePath}", filePath);
                throw;
            }
        }

        public async Task<bool> VerifyAsync(Guid mediaItemId, CancellationToken ct)
        {
            var mediaItem = await _mediaItemRepository.GetAsync(mediaItemId, ct);
            if (mediaItem == null)
            {
                _logger.LogWarning("MediaItem {MediaItemId} not found during integrity verification", mediaItemId);
                return false;
            }

            if (!File.Exists(mediaItem.FilePath))
            {
                _logger.LogError("File not found during verification: {FilePath}", mediaItem.FilePath);
                return false;
            }

            try
            {
                var currentHash = await ComputeHashAsync(mediaItem.FilePath, ct);
                var passed = currentHash.Equals(mediaItem.Sha256Hash, StringComparison.OrdinalIgnoreCase);

                _logger.LogInformation(
                    "Verification {Result} for {FilePath}: stored={StoredHash}, current={CurrentHash}",
                    passed ? "passed" : "FAILED",
                    mediaItem.FilePath,
                    mediaItem.Sha256Hash,
                    currentHash);

                return passed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during integrity verification of {FilePath}", mediaItem.FilePath);
                return false;
            }
        }

        public async Task<int> VerifyAllForDeviceAsync(Guid deviceId, CancellationToken ct)
        {
            var mediaItems = await _mediaItemRepository.GetByDeviceIdAsync(deviceId, ct);
            int verifiedCount = 0;

            foreach (var mediaItem in mediaItems)
            {
                await VerifyAsync(mediaItem.Id, ct);
                verifiedCount++;
            }

            _logger.LogInformation("Verified {VerifiedCount} media items for device {DeviceId}", verifiedCount, deviceId);
            return verifiedCount;
        }
    }
}
