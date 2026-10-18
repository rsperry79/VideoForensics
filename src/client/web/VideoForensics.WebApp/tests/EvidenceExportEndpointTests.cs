using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.WebApp.Api;
using VideoForensics.Hosting;
using Xunit;

namespace VideoForensics.WebApp.Tests
{
    /// <summary>The export endpoint is case-scoped: it needs a case id and exports only that case.</summary>
    public class EvidenceExportEndpointTests
    {
        private readonly Mock<IEvidenceExportService> _export = new();
        private readonly Mock<ISecurityAuditLogger> _audit = new();
        private readonly Mock<INetworkTierResolver> _tier = new();

        public EvidenceExportEndpointTests()
        {
            _ = _tier.Setup(t => t.ResolveClientIp(It.IsAny<HttpContext>())).Returns("127.0.0.1");
            _ = _export
                .Setup(e => e.ExportEvidenceAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
                .ReturnsAsync(new ExportResult { Success = true, ArchivePath = "a.zip", ItemsIncluded = 2 });
        }

        private Task<IResult> Call(ExportEvidenceRequest request) =>
            EvidenceEndpoints.ExportAsync(request, _export.Object, _audit.Object, _tier.Object, new DefaultHttpContext(), CancellationToken.None);

        [Fact]
        public async Task Export_MissingCaseId_ReturnsBadRequestAndDoesNotExport()
        {
            IResult result = await Call(new ExportEvidenceRequest(Guid.Empty, "out", null, null));

            _ = Assert.IsType<BadRequest<string>>(result);
            _export.Verify(e => e.ExportEvidenceAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Never);
            _audit.Verify(a => a.LogAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Export_WithCaseId_ExportsThatCase()
        {
            Guid caseId = Guid.NewGuid();

            IResult result = await Call(new ExportEvidenceRequest(caseId, "out", "recipient", "pass"));

            _ = Assert.IsType<Ok<ExportResultDto>>(result);
            _export.Verify(e => e.ExportEvidenceAsync(caseId, "out", "recipient", "pass", It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Once);
        }

        [Fact]
        public async Task Export_WithCaseId_AuditsTheCase()
        {
            Guid caseId = Guid.NewGuid();

            _ = await Call(new ExportEvidenceRequest(caseId, "out", null, null));

            _audit.Verify(a => a.LogAsync(SecurityAuditEventTypes.EvidenceExported, It.IsAny<Guid?>(), null, "127.0.0.1", It.Is<string?>(d => d != null && d.Contains(caseId.ToString())), true, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
