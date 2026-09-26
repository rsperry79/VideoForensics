using Microsoft.Extensions.Logging;

using Moq;

using System.Security.Cryptography;
using System.Text;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Core.Services;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Core;

using Xunit;

namespace VideoForensics.Client.Core.Tests
{
    public class MediaStillCaptureOrchestratorTests
    {
        private readonly Mock<ILogger<MediaStillCaptureOrchestrator>> _loggerMock = new();
        private readonly Mock<IMediaItemRepository> _mediaItemRepositoryMock = new();
        private readonly Mock<IMediaStillRepository> _mediaStillRepositoryMock = new();
        private readonly Mock<IMediaStorageProvider> _storageProviderMock = new();
        private readonly Mock<IMediaFrameExtractor> _frameExtractorMock = new();
        private readonly Mock<IActionLogRepository> _actionLogRepositoryMock = new();
        private readonly Mock<ICaseRepository> _caseRepositoryMock = new();
        private readonly MediaStillCaptureOrchestrator _orchestrator;

        private static readonly Guid SourceId = Guid.NewGuid();
        private static readonly byte[] SourceBytes = Encoding.UTF8.GetBytes("source-video-bytes");

        public MediaStillCaptureOrchestratorTests()
        {
            _orchestrator = new MediaStillCaptureOrchestrator(
                _loggerMock.Object,
                _mediaItemRepositoryMock.Object,
                _mediaStillRepositoryMock.Object,
                _storageProviderMock.Object,
                _frameExtractorMock.Object,
                _actionLogRepositoryMock.Object,
                _caseRepositoryMock.Object);

            var source = new MediaItem
            {
                Id = SourceId,
                DeviceId = Guid.NewGuid(),
                FileName = "video.mp4",
                FilePath = "/media/video.mp4",
                MediaFormat = "video/mp4",
                FileSizeBytes = SourceBytes.Length,
                Sha256Hash = "stored-hash-not-trusted",
                RecordedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                DownloadedAtUtc = DateTime.UtcNow
            };

            _ = _mediaItemRepositoryMock
                .Setup(r => r.GetAsync(SourceId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(source);

            _ = _storageProviderMock
                .Setup(s => s.ExistsAsync("/media/video.mp4", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            _ = _storageProviderMock
                .Setup(s => s.OpenReadStreamAsync("/media/video.mp4", It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new MemoryStream(SourceBytes));

            _ = _mediaStillRepositoryMock
                .Setup(r => r.CreateAsync(It.IsAny<MediaItem>(), It.IsAny<MediaStillCapture>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((MediaItem _, MediaStillCapture capture, CancellationToken _) => capture);
        }

        private static string ExpectedSourceHash =>
            Convert.ToHexString(SHA256.HashData(SourceBytes)).ToLowerInvariant();

        [Fact]
        public async Task CaptureStillAsync_NegativeOffset_ReturnsInvalidInput_WithoutTouchingRepositories()
        {
            MediaStillCaptureResult result = await _orchestrator.CaptureStillAsync(
                SourceId, -1, null, null, null, "operator-1", CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(MediaStillCaptureError.InvalidInput, result.Error);
            _mediaItemRepositoryMock.Verify(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CaptureStillAsync_CaseIdWithoutReason_ReturnsInvalidInput()
        {
            MediaStillCaptureResult result = await _orchestrator.CaptureStillAsync(
                SourceId, 1000, null, Guid.NewGuid(), null, "operator-1", CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(MediaStillCaptureError.InvalidInput, result.Error);
        }

        [Fact]
        public async Task CaptureStillAsync_UnknownMedia_ReturnsMediaNotFound()
        {
            _ = _mediaItemRepositoryMock
                .Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((MediaItem?)null);

            MediaStillCaptureResult result = await _orchestrator.CaptureStillAsync(
                Guid.NewGuid(), 1000, null, null, null, "operator-1", CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(MediaStillCaptureError.MediaNotFound, result.Error);
        }

        [Fact]
        public async Task CaptureStillAsync_ServerExtractionSucceeds_PersistsStillViaServerMethod()
        {
            var pngBytes = new byte[] { 1, 2, 3, 4 };
            _ = _frameExtractorMock
                .Setup(e => e.ExtractFrameAsync("/media/video.mp4", 2500, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FrameExtractionResult { Success = true, PngBytes = pngBytes });

            MediaStillCaptureResult result = await _orchestrator.CaptureStillAsync(
                SourceId, 2500, null, null, null, "operator-1", CancellationToken.None);

            Assert.True(result.Success);
            Assert.NotNull(result.Still);
            Assert.Equal("Server", result.Still!.CaptureMethod);
            Assert.Equal(SourceId, result.Still.SourceMediaItemId);
            Assert.Equal(2500, result.Still.FrameOffsetMs);
            Assert.Equal(ExpectedSourceHash, result.Still.SourceSha256AtCapture);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(pngBytes)).ToLowerInvariant(), result.Still.Sha256Hash);
            Assert.False(result.Still.PinnedToCase);

            _mediaStillRepositoryMock.Verify(r => r.CreateAsync(
                It.Is<MediaItem>(m => m.Sha256Hash == result.Still.Sha256Hash && m.MediaFormat == "image/png"),
                It.Is<MediaStillCapture>(c => c.CaptureMethod == MediaStillCaptureMethod.Server && c.SourceMediaItemId == SourceId),
                It.IsAny<CancellationToken>()), Times.Once);

            _actionLogRepositoryMock.Verify(r => r.AppendAsync(
                "operator-1", ActorType.Human, "CaptureStill", nameof(MediaItem), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Once);

            _caseRepositoryMock.Verify(r => r.AddItemAsync(It.IsAny<Guid>(), It.IsAny<CaseItemKind>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CaptureStillAsync_ServerExtractionFails_FallsBackToClientBytes()
        {
            _ = _frameExtractorMock
                .Setup(e => e.ExtractFrameAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FrameExtractionResult { Success = false, ErrorMessage = "ffmpeg not found" });

            var clientBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 9, 9, 9 };
            string base64 = Convert.ToBase64String(clientBytes);

            MediaStillCaptureResult result = await _orchestrator.CaptureStillAsync(
                SourceId, 500, base64, null, null, "operator-2", CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("Client", result.Still!.CaptureMethod);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(clientBytes)).ToLowerInvariant(), result.Still.Sha256Hash);

            _mediaStillRepositoryMock.Verify(r => r.CreateAsync(
                It.IsAny<MediaItem>(),
                It.Is<MediaStillCapture>(c => c.CaptureMethod == MediaStillCaptureMethod.Client),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CaptureStillAsync_ExtractionFailsAndNoClientBytes_ReturnsExtractionFailed()
        {
            _ = _frameExtractorMock
                .Setup(e => e.ExtractFrameAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FrameExtractionResult { Success = false, ErrorMessage = "ffmpeg not found" });

            MediaStillCaptureResult result = await _orchestrator.CaptureStillAsync(
                SourceId, 500, null, null, null, "operator-1", CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(MediaStillCaptureError.ExtractionFailed, result.Error);
            _mediaStillRepositoryMock.Verify(r => r.CreateAsync(It.IsAny<MediaItem>(), It.IsAny<MediaStillCapture>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CaptureStillAsync_WithCaseId_PinsStillAndReturnsPinnedTrue()
        {
            _ = _frameExtractorMock
                .Setup(e => e.ExtractFrameAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FrameExtractionResult { Success = true, PngBytes = new byte[] { 1 } });

            var caseId = Guid.NewGuid();
            _ = _caseRepositoryMock
                .Setup(c => c.AddItemAsync(caseId, CaseItemKind.Media, It.IsAny<Guid>(), "evidence of intrusion", "operator-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid cId, CaseItemKind kind, Guid targetId, string reason, string addedBy, CancellationToken _) => new CaseItem
                {
                    Id = Guid.NewGuid(),
                    CaseId = cId,
                    Kind = kind,
                    MediaItemId = targetId,
                    Reason = reason,
                    AddedBy = addedBy,
                    AddedAtUtc = DateTime.UtcNow
                });

            MediaStillCaptureResult result = await _orchestrator.CaptureStillAsync(
                SourceId, 100, null, caseId, "evidence of intrusion", "operator-1", CancellationToken.None);

            Assert.True(result.Success);
            Assert.True(result.Still!.PinnedToCase);
            _caseRepositoryMock.Verify(c => c.AddItemAsync(caseId, CaseItemKind.Media, It.IsAny<Guid>(), "evidence of intrusion", "operator-1", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CaptureStillAsync_PinFails_StillReturnsSuccessWithPinnedFalse()
        {
            _ = _frameExtractorMock
                .Setup(e => e.ExtractFrameAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FrameExtractionResult { Success = true, PngBytes = new byte[] { 1 } });

            _ = _caseRepositoryMock
                .Setup(c => c.AddItemAsync(It.IsAny<Guid>(), It.IsAny<CaseItemKind>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("case not found"));

            MediaStillCaptureResult result = await _orchestrator.CaptureStillAsync(
                SourceId, 100, null, Guid.NewGuid(), "reason", "operator-1", CancellationToken.None);

            Assert.True(result.Success);
            Assert.False(result.Still!.PinnedToCase);
        }

        [Fact]
        public async Task CaptureStillAsync_SourceFileMissing_ReturnsMediaNotFound()
        {
            _ = _storageProviderMock
                .Setup(s => s.ExistsAsync("/media/video.mp4", It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            MediaStillCaptureResult result = await _orchestrator.CaptureStillAsync(
                SourceId, 100, null, null, null, "operator-1", CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(MediaStillCaptureError.MediaNotFound, result.Error);
        }

        private void SetupServerExtractionFailure()
        {
            _ = _frameExtractorMock
                .Setup(e => e.ExtractFrameAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FrameExtractionResult { Success = false, ErrorMessage = "ffmpeg not available" });
        }

        [Fact]
        public async Task CaptureStillAsync_ClientBase64TooLong_ReturnsInvalidInputWithoutDecoding()
        {
            SetupServerExtractionFailure();

            // Deliberately larger than the ~20 MB decoded cap's base64 threshold - must be
            // rejected by length before any Convert.FromBase64String allocation is attempted.
            string oversizedBase64 = new string('A', 30_000_000);

            MediaStillCaptureResult result = await _orchestrator.CaptureStillAsync(
                SourceId, 100, oversizedBase64, null, null, "operator-1", CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(MediaStillCaptureError.InvalidInput, result.Error);
            _mediaStillRepositoryMock.Verify(r => r.CreateAsync(It.IsAny<MediaItem>(), It.IsAny<MediaStillCapture>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CaptureStillAsync_ClientBytesExceedDecodedSizeCap_ReturnsInvalidInput()
        {
            SetupServerExtractionFailure();

            byte[] oversized = new byte[20 * 1024 * 1024 + 1];
            string base64 = Convert.ToBase64String(oversized);

            MediaStillCaptureResult result = await _orchestrator.CaptureStillAsync(
                SourceId, 100, base64, null, null, "operator-1", CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(MediaStillCaptureError.InvalidInput, result.Error);
            _mediaStillRepositoryMock.Verify(r => r.CreateAsync(It.IsAny<MediaItem>(), It.IsAny<MediaStillCapture>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CaptureStillAsync_ClientBytesNotPngSignature_ReturnsInvalidInput()
        {
            SetupServerExtractionFailure();

            byte[] notPng = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            string base64 = Convert.ToBase64String(notPng);

            MediaStillCaptureResult result = await _orchestrator.CaptureStillAsync(
                SourceId, 100, base64, null, null, "operator-1", CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(MediaStillCaptureError.InvalidInput, result.Error);
            _mediaStillRepositoryMock.Verify(r => r.CreateAsync(It.IsAny<MediaItem>(), It.IsAny<MediaStillCapture>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CaptureStillAsync_ClientBytesValidPngSignature_Succeeds()
        {
            SetupServerExtractionFailure();

            byte[] validPng = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 42, 42 };
            string base64 = Convert.ToBase64String(validPng);

            MediaStillCaptureResult result = await _orchestrator.CaptureStillAsync(
                SourceId, 100, base64, null, null, "operator-1", CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal("Client", result.Still!.CaptureMethod);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(validPng)).ToLowerInvariant(), result.Still.Sha256Hash);
        }
    }
}
