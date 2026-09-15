using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Ring.Exceptions;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    public class ProviderApiErrorClassificationTests
    {
        [Fact]
        public void ClassifyProviderApiError_DeviceUnknownException_WithPriorSuccess_ReturnsRecordingDeletedAfterDownload()
        {
            var existingRecord = new DownloadEvent
            {
                Id = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                ProviderEventId = "evt-1",
                Success = true,
                AppVersion = "1.0"
            };

            string category = RingProviderApiErrorClassifier.ClassifyProviderApiError(
                new DeviceUnknownException(new Uri("https://api.ring.com/clients_api/dings/123/recording")),
                existingRecord);

            Assert.Equal("RecordingDeletedAfterDownload", category);
        }

        [Fact]
        public void ClassifyProviderApiError_DeviceUnknownException_WithNoPriorRecord_ReturnsRecordingNotFound()
        {
            string category = RingProviderApiErrorClassifier.ClassifyProviderApiError(
                new DeviceUnknownException(new Uri("https://api.ring.com/clients_api/dings/123/recording")),
                existingRecord: null);

            Assert.Equal("RecordingNotFound", category);
        }

        [Fact]
        public void ClassifyProviderApiError_DeviceUnknownException_WithPriorFailure_ReturnsRecordingNotFound()
        {
            var existingRecord = new DownloadEvent
            {
                Id = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                ProviderEventId = "evt-1",
                Success = false,
                AppVersion = "1.0"
            };

            string category = RingProviderApiErrorClassifier.ClassifyProviderApiError(
                new DeviceUnknownException(new Uri("https://api.ring.com/clients_api/dings/123/recording")),
                existingRecord);

            Assert.Equal("RecordingNotFound", category);
        }

        [Fact]
        public void ClassifyProviderApiError_ThrottledException_ReturnsRateLimited()
        {
            string category = RingProviderApiErrorClassifier.ClassifyProviderApiError(new ThrottledException(), existingRecord: null);
            Assert.Equal("RateLimited", category);
        }

        [Fact]
        public void ClassifyProviderApiError_DownloadFailedException_ReturnsDownloadFailed()
        {
            string category = RingProviderApiErrorClassifier.ClassifyProviderApiError(new DownloadFailedException("https://example.com/video.mp4"), existingRecord: null);
            Assert.Equal("DownloadFailed", category);
        }

        [Fact]
        public void ClassifyProviderApiError_UnexpectedOutcomeException_ReturnsUnexpectedStatus()
        {
            string category = RingProviderApiErrorClassifier.ClassifyProviderApiError(
                new UnexpectedOutcomeException(System.Net.HttpStatusCode.InternalServerError),
                existingRecord: null);

            Assert.Equal("UnexpectedStatus", category);
        }

        [Fact]
        public void ClassifyProviderApiError_OperationCanceledException_ReturnsCancelled()
        {
            string category = RingProviderApiErrorClassifier.ClassifyProviderApiError(new OperationCanceledException(), existingRecord: null);
            Assert.Equal("Cancelled", category);
        }

        [Fact]
        public void ClassifyProviderApiError_GenericException_ReturnsOther()
        {
            string category = RingProviderApiErrorClassifier.ClassifyProviderApiError(new InvalidOperationException("boom"), existingRecord: null);
            Assert.Equal("Other", category);
        }
    }
}
