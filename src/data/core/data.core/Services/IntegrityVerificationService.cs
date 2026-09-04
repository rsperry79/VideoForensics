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
        private readonly IIntegrityRecordRepository _integrityRecordRepository;
        private readonly ILogger<IntegrityVerificationService> _logger;

        public IntegrityVerificationService(
            IMediaItemRepository mediaItemRepository,
            IIntegrityRecordRepository integrityRecordRepository,
            ILogger<IntegrityVerificationService> logger)
        {
            _mediaItemRepository = mediaItemRepository;
            _integrityRecordRepository = integrityRecordRepository;
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
                await RecordResultAsync(mediaItemId, mediaItem.Sha256Hash, passed: false, failureReason: "File not found", ct);
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

                await RecordResultAsync(
                    mediaItemId,
                    currentHash,
                    passed,
                    failureReason: passed ? null : "SHA-256 hash mismatch",
                    ct);

                return passed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during integrity verification of {FilePath}", mediaItem.FilePath);
                await RecordResultAsync(mediaItemId, mediaItem.Sha256Hash, passed: false, failureReason: ex.Message, ct);
                return false;
            }
        }

        private async Task RecordResultAsync(Guid mediaItemId, string sha256Hash, bool passed, string? failureReason, CancellationToken ct)
        {
            try
            {
                await _integrityRecordRepository.AddAsync(
                    new IntegrityRecord
                    {
                        Id = Guid.NewGuid(),
                        MediaItemId = mediaItemId,
                        Sha256Hash = sha256Hash,
                        VerifiedAtUtc = DateTime.UtcNow,
                        Passed = passed,
                        FailureReason = failureReason,
                        VerifiedBy = nameof(IntegrityVerificationService)
                    },
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording integrity result for media item {MediaItemId}", mediaItemId);
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
