using VideoForensics.Core.Api.Contracts;

using Xunit;

namespace VideoForensics.Core.Api.Contracts.Tests
{
    public class QueueAccountExportRequestTests
    {
        [Fact]
        public void ValidationError_ExportOnly_IsAccepted()
        {
            var request = new QueueAccountExportRequest(PurgeAfterExport: false);

            Assert.Null(request.ValidationError);
        }

        [Fact]
        public void ValidationError_PurgeWithoutConfirmation_IsRefused()
        {
            var request = new QueueAccountExportRequest(PurgeAfterExport: true);

            Assert.Equal(QueueAccountExportRequest.PurgeNotConfirmedMessage, request.ValidationError);
        }

        [Fact]
        public void ValidationError_PurgeWithConfirmation_IsAccepted()
        {
            var request = new QueueAccountExportRequest(PurgeAfterExport: true, ConfirmPurge: true);

            Assert.Null(request.ValidationError);
        }
    }
}
