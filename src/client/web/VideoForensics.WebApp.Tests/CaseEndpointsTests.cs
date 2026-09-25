using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;

using Moq;

using System.Security.Claims;

using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.WebApp.Api;
using VideoForensics.WebApp.Auth;

using Xunit;

namespace VideoForensics.WebApp.Tests
{
    public class CaseEndpointsTests
    {
        private static (
            Mock<ICaseRepository> Cases,
            Mock<ILogger<Program>> Logger
        ) CreateMocks()
        {
            var cases = new Mock<ICaseRepository>();
            var logger = new Mock<ILogger<Program>>();
            return (cases, logger);
        }

        private ClaimsPrincipal CreatePrincipalWithOperatorId(Guid operatorId)
        {
            var claims = new List<Claim>
            {
                new Claim(VideoForensicsClaimTypes.OperatorId, operatorId.ToString()),
                new Claim(ClaimTypes.Role, OperatorRole.Admin.ToString())
            };
            var identity = new ClaimsIdentity(claims, "test");
            return new ClaimsPrincipal(identity);
        }

        private ForensicCase CreateForensicCase(Guid? id = null)
        {
            return new ForensicCase
            {
                Id = id ?? Guid.NewGuid(),
                CaseNumber = "CASE-001",
                Title = "Test Case",
                Description = "A test forensic case",
                Status = CaseStatus.Open,
                LeadOperatorId = Guid.NewGuid(),
                CreatedBy = "admin",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
                UpdatedAtUtc = DateTime.UtcNow,
                ClosedBy = null,
                ClosedAtUtc = null,
                ScopeFromUtc = DateTime.UtcNow.AddDays(-7),
                ScopeToUtc = DateTime.UtcNow
            };
        }

        private CaseItem CreateCaseItem(Guid? id = null, Guid? caseId = null)
        {
            return new CaseItem
            {
                Id = id ?? Guid.NewGuid(),
                CaseId = caseId ?? Guid.NewGuid(),
                Kind = CaseItemKind.Media,
                MediaItemId = Guid.NewGuid(),
                EventId = null,
                Reason = "Relevant to investigation",
                AddedBy = "admin",
                AddedAtUtc = DateTime.UtcNow,
                MediaSha256AtAdd = "abc123",
                RemovedBy = null,
                RemovedAtUtc = null,
                RemovalReason = null
            };
        }

        #region GetCasesAsync Tests

        [Fact]
        public async Task GetCasesAsync_WithoutStatus_ReturnsCases()
        {
            (var cases, _) = CreateMocks();
            var case1 = CreateForensicCase();
            var case2 = CreateForensicCase();
            cases.Setup(c => c.ListAsync(null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ForensicCase> { case1, case2 });

            IResult result = await CaseEndpoints.GetCasesAsync(null, cases.Object, CancellationToken.None);

            cases.Verify(c => c.ListAsync(null, It.IsAny<CancellationToken>()), Times.Once);
            Assert.NotNull(result);
            // Verify it's an Ok result
            Assert.True(result.GetType().Name.Contains("Ok"));
        }

        [Fact]
        public async Task GetCasesAsync_WithStatus_FiltersCorrectly()
        {
            (var cases, _) = CreateMocks();
            var closedCase = CreateForensicCase();
            closedCase.Status = CaseStatus.Closed;
            cases.Setup(c => c.ListAsync(CaseStatus.Closed, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ForensicCase> { closedCase });

            IResult result = await CaseEndpoints.GetCasesAsync("Closed", cases.Object, CancellationToken.None);

            cases.Verify(c => c.ListAsync(CaseStatus.Closed, It.IsAny<CancellationToken>()), Times.Once);
            Assert.NotNull(result);
        }

        #endregion

        #region GetCaseAsync Tests

        [Fact]
        public async Task GetCaseAsync_NotFound_ReturnsNotFound()
        {
            (var cases, _) = CreateMocks();
            var id = Guid.NewGuid();
            cases.Setup(c => c.GetAsync(id, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ForensicCase?)null);

            IResult result = await CaseEndpoints.GetCaseAsync(id, cases.Object, CancellationToken.None);

            Assert.IsType<NotFound>(result);
        }

        [Fact]
        public async Task GetCaseAsync_Found_ReturnsCaseWithDeviceIds()
        {
            (var cases, _) = CreateMocks();
            var @case = CreateForensicCase();
            var deviceIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
            cases.Setup(c => c.GetAsync(@case.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(@case);
            cases.Setup(c => c.GetDeviceIdsAsync(@case.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(deviceIds);

            IResult result = await CaseEndpoints.GetCaseAsync(@case.Id, cases.Object, CancellationToken.None);

            cases.Verify(c => c.GetDeviceIdsAsync(@case.Id, It.IsAny<CancellationToken>()), Times.Once);
            Assert.NotNull(result);
            Assert.True(result.GetType().Name.StartsWith("Ok`"));
        }

        #endregion

        #region GetCaseByNumberAsync Tests

        [Fact]
        public async Task GetCaseByNumberAsync_NotFound_ReturnsNotFound()
        {
            (var cases, _) = CreateMocks();
            cases.Setup(c => c.GetByNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ForensicCase?)null);

            IResult result = await CaseEndpoints.GetCaseByNumberAsync("INVALID", cases.Object, CancellationToken.None);

            Assert.IsType<NotFound>(result);
        }

        [Fact]
        public async Task GetCaseByNumberAsync_Found_ReturnsCaseWithDeviceIds()
        {
            (var cases, _) = CreateMocks();
            var @case = CreateForensicCase();
            var deviceIds = new List<Guid> { Guid.NewGuid() };
            cases.Setup(c => c.GetByNumberAsync(@case.CaseNumber, It.IsAny<CancellationToken>()))
                .ReturnsAsync(@case);
            cases.Setup(c => c.GetDeviceIdsAsync(@case.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(deviceIds);

            IResult result = await CaseEndpoints.GetCaseByNumberAsync(@case.CaseNumber, cases.Object, CancellationToken.None);

            cases.Verify(c => c.GetByNumberAsync(@case.CaseNumber, It.IsAny<CancellationToken>()), Times.Once);
            Assert.NotNull(result);
        }

        #endregion

        #region GetCaseDevicesAsync Tests

        [Fact]
        public async Task GetCaseDevicesAsync_ReturnsDeviceIds()
        {
            (var cases, _) = CreateMocks();
            var caseId = Guid.NewGuid();
            var deviceIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
            cases.Setup(c => c.GetDeviceIdsAsync(caseId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(deviceIds);

            IResult result = await CaseEndpoints.GetCaseDevicesAsync(caseId, cases.Object, CancellationToken.None);

            Assert.NotNull(result);
            Assert.True(result.GetType().Name.StartsWith("Ok`"));
        }

        #endregion

        #region GetCaseItemsAsync Tests

        [Fact]
        public async Task GetCaseItemsAsync_NoRepository_ReturnsBadRequest()
        {
            var caseId = Guid.NewGuid();

            IResult result = await CaseEndpoints.GetCaseItemsAsync(caseId, false);

            Assert.True(result.GetType().Name.Contains("BadRequest"));
        }

        [Fact]
        public async Task GetCaseItemsAsync_WithIncludeRemoved_ReturnsItems()
        {
            (var cases, _) = CreateMocks();
            var caseId = Guid.NewGuid();
            var item1 = CreateCaseItem(caseId: caseId);
            var item2 = CreateCaseItem(caseId: caseId);
            item2.RemovedAtUtc = DateTime.UtcNow;
            cases.Setup(c => c.ListItemsAsync(caseId, true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<CaseItem> { item1, item2 });

            IResult result = await CaseEndpoints.GetCaseItemsAsync(caseId, true, cases.Object, CancellationToken.None);

            cases.Verify(c => c.ListItemsAsync(caseId, true, It.IsAny<CancellationToken>()), Times.Once);
            Assert.NotNull(result);
        }

        #endregion

        #region ListCasesContainingAsync Tests

        [Fact]
        public async Task ListCasesContainingAsync_MissingParameters_ReturnsBadRequest()
        {
            (var cases, _) = CreateMocks();

            IResult result = await CaseEndpoints.ListCasesContainingAsync(null, null, cases.Object, CancellationToken.None);

            Assert.True(result.GetType().Name.Contains("BadRequest"));
        }

        [Fact]
        public async Task ListCasesContainingAsync_InvalidKind_ReturnsBadRequest()
        {
            (var cases, _) = CreateMocks();

            IResult result = await CaseEndpoints.ListCasesContainingAsync("InvalidKind", Guid.NewGuid(), cases.Object, CancellationToken.None);

            Assert.True(result.GetType().Name.Contains("BadRequest"));
        }

        [Fact]
        public async Task ListCasesContainingAsync_ValidParameters_ReturnsCases()
        {
            (var cases, _) = CreateMocks();
            var targetId = Guid.NewGuid();
            var @case = CreateForensicCase();
            cases.Setup(c => c.ListCasesContainingAsync(CaseItemKind.Media, targetId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ForensicCase> { @case });

            IResult result = await CaseEndpoints.ListCasesContainingAsync("Media", targetId, cases.Object, CancellationToken.None);

            cases.Verify(c => c.ListCasesContainingAsync(CaseItemKind.Media, targetId, It.IsAny<CancellationToken>()), Times.Once);
            Assert.NotNull(result);
        }

        #endregion

        #region CreateCaseAsync Tests

        [Fact]
        public async Task CreateCaseAsync_NoOperatorClaim_ReturnsUnauthorized()
        {
            (var cases, _) = CreateMocks();
            var request = new CreateCaseRequestDto("CASE-001", "Test", null, null, null, null, new List<Guid>());
            var context = new DefaultHttpContext();

            IResult result = await CaseEndpoints.CreateCaseAsync(request, cases.Object, context, CancellationToken.None);

            Assert.IsType<UnauthorizedHttpResult>(result);
        }

        [Fact]
        public async Task CreateCaseAsync_EmptyCaseNumber_ReturnsBadRequest()
        {
            (var cases, _) = CreateMocks();
            var operatorId = Guid.NewGuid();
            var request = new CreateCaseRequestDto("", "Test", null, null, null, null, new List<Guid>());
            var context = new DefaultHttpContext { User = CreatePrincipalWithOperatorId(operatorId) };

            IResult result = await CaseEndpoints.CreateCaseAsync(request, cases.Object, context, CancellationToken.None);

            Assert.True(result.GetType().Name.Contains("BadRequest"));
        }

        [Fact]
        public async Task CreateCaseAsync_CaseNumberTooLong_ReturnsBadRequest()
        {
            (var cases, _) = CreateMocks();
            var operatorId = Guid.NewGuid();
            var longNumber = new string('A', 65);
            var request = new CreateCaseRequestDto(longNumber, "Test", null, null, null, null, new List<Guid>());
            var context = new DefaultHttpContext { User = CreatePrincipalWithOperatorId(operatorId) };

            IResult result = await CaseEndpoints.CreateCaseAsync(request, cases.Object, context, CancellationToken.None);

            Assert.True(result.GetType().Name.Contains("BadRequest"));
        }

        [Fact]
        public async Task CreateCaseAsync_EmptyTitle_ReturnsBadRequest()
        {
            (var cases, _) = CreateMocks();
            var operatorId = Guid.NewGuid();
            var request = new CreateCaseRequestDto("CASE-001", "", null, null, null, null, new List<Guid>());
            var context = new DefaultHttpContext { User = CreatePrincipalWithOperatorId(operatorId) };

            IResult result = await CaseEndpoints.CreateCaseAsync(request, cases.Object, context, CancellationToken.None);

            Assert.True(result.GetType().Name.Contains("BadRequest"));
        }

        [Fact]
        public async Task CreateCaseAsync_InvalidScopeRange_ReturnsBadRequest()
        {
            (var cases, _) = CreateMocks();
            var operatorId = Guid.NewGuid();
            var from = DateTime.UtcNow.AddDays(1);
            var to = DateTime.UtcNow;
            var request = new CreateCaseRequestDto("CASE-001", "Test", null, null, from, to, new List<Guid>());
            var context = new DefaultHttpContext { User = CreatePrincipalWithOperatorId(operatorId) };

            IResult result = await CaseEndpoints.CreateCaseAsync(request, cases.Object, context, CancellationToken.None);

            Assert.True(result.GetType().Name.Contains("BadRequest"));
        }

        [Fact]
        public async Task CreateCaseAsync_DuplicateCaseNumber_ReturnsConflict()
        {
            (var cases, _) = CreateMocks();
            var operatorId = Guid.NewGuid();
            var request = new CreateCaseRequestDto("CASE-001", "Test", null, null, null, null, new List<Guid>());
            cases.Setup(c => c.CreateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<Guid?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Duplicate case number"));
            var context = new DefaultHttpContext { User = CreatePrincipalWithOperatorId(operatorId) };

            IResult result = await CaseEndpoints.CreateCaseAsync(request, cases.Object, context, CancellationToken.None);

            Assert.True(result.GetType().Name.Contains("Conflict"));
        }

        [Fact]
        public async Task CreateCaseAsync_Success_ReturnsCreated()
        {
            (var cases, _) = CreateMocks();
            var operatorId = Guid.NewGuid();
            var @case = CreateForensicCase();
            var request = new CreateCaseRequestDto("CASE-001", "Test", null, null, null, null, new List<Guid>());
            cases.Setup(c => c.CreateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<Guid?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(@case);
            cases.Setup(c => c.GetDeviceIdsAsync(@case.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Guid>());
            var context = new DefaultHttpContext { User = CreatePrincipalWithOperatorId(operatorId) };

            IResult result = await CaseEndpoints.CreateCaseAsync(request, cases.Object, context, CancellationToken.None);

            cases.Verify(c => c.CreateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<Guid?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<IReadOnlyCollection<Guid>>(),
                operatorId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
            Assert.NotNull(result);
            Assert.True(result.GetType().Name.StartsWith("Created"));
        }

        #endregion

        #region UpdateCaseDetailsAsync Tests

        [Fact]
        public async Task UpdateCaseDetailsAsync_NoOperatorClaim_ReturnsUnauthorized()
        {
            (var cases, _) = CreateMocks();
            var request = new UpdateCaseDetailsRequestDto("New Title", null, null);
            var context = new DefaultHttpContext();

            IResult result = await CaseEndpoints.UpdateCaseDetailsAsync(Guid.NewGuid(), request, cases.Object, context, CancellationToken.None);

            Assert.IsType<UnauthorizedHttpResult>(result);
        }

        [Fact]
        public async Task UpdateCaseDetailsAsync_EmptyTitle_ReturnsBadRequest()
        {
            (var cases, _) = CreateMocks();
            var operatorId = Guid.NewGuid();
            var request = new UpdateCaseDetailsRequestDto("", null, null);
            var context = new DefaultHttpContext { User = CreatePrincipalWithOperatorId(operatorId) };

            IResult result = await CaseEndpoints.UpdateCaseDetailsAsync(Guid.NewGuid(), request, cases.Object, context, CancellationToken.None);

            Assert.True(result.GetType().Name.Contains("BadRequest"));
        }

        [Fact]
        public async Task UpdateCaseDetailsAsync_Success_ReturnsNoContent()
        {
            (var cases, _) = CreateMocks();
            var operatorId = Guid.NewGuid();
            var caseId = Guid.NewGuid();
            var request = new UpdateCaseDetailsRequestDto("New Title", "New Description", null);
            cases.Setup(c => c.UpdateDetailsAsync(caseId, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            var context = new DefaultHttpContext { User = CreatePrincipalWithOperatorId(operatorId) };

            IResult result = await CaseEndpoints.UpdateCaseDetailsAsync(caseId, request, cases.Object, context, CancellationToken.None);

            Assert.IsType<NoContent>(result);
        }

        #endregion

        #region SetCaseScopeAsync Tests

        [Fact]
        public async Task SetCaseScopeAsync_InvalidScopeRange_ReturnsBadRequest()
        {
            (var cases, _) = CreateMocks();
            var operatorId = Guid.NewGuid();
            var from = DateTime.UtcNow.AddDays(1);
            var to = DateTime.UtcNow;
            var request = new SetCaseScopeRequestDto(from, to, new List<Guid>());
            var context = new DefaultHttpContext { User = CreatePrincipalWithOperatorId(operatorId) };

            IResult result = await CaseEndpoints.SetCaseScopeAsync(Guid.NewGuid(), request, cases.Object, context, CancellationToken.None);

            Assert.True(result.GetType().Name.Contains("BadRequest"));
        }

        [Fact]
        public async Task SetCaseScopeAsync_Success_ReturnsNoContent()
        {
            (var cases, _) = CreateMocks();
            var operatorId = Guid.NewGuid();
            var caseId = Guid.NewGuid();
            var request = new SetCaseScopeRequestDto(DateTime.UtcNow, DateTime.UtcNow.AddDays(1), new List<Guid> { Guid.NewGuid() });
            cases.Setup(c => c.SetScopeAsync(caseId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            var context = new DefaultHttpContext { User = CreatePrincipalWithOperatorId(operatorId) };

            IResult result = await CaseEndpoints.SetCaseScopeAsync(caseId, request, cases.Object, context, CancellationToken.None);

            Assert.IsType<NoContent>(result);
        }

        #endregion

        #region AddCaseItemAsync Tests

        [Fact]
        public async Task AddCaseItemAsync_NoOperatorClaim_ReturnsUnauthorized()
        {
            (var cases, _) = CreateMocks();
            var request = new AddCaseItemRequestDto("Media", Guid.NewGuid(), "Test reason");
            var context = new DefaultHttpContext();

            IResult result = await CaseEndpoints.AddCaseItemAsync(Guid.NewGuid(), request, cases.Object, context, CancellationToken.None);

            Assert.IsType<UnauthorizedHttpResult>(result);
        }

        [Fact]
        public async Task AddCaseItemAsync_EmptyReason_ReturnsBadRequest()
        {
            (var cases, _) = CreateMocks();
            var operatorId = Guid.NewGuid();
            var request = new AddCaseItemRequestDto("Media", Guid.NewGuid(), "");
            var context = new DefaultHttpContext { User = CreatePrincipalWithOperatorId(operatorId) };

            IResult result = await CaseEndpoints.AddCaseItemAsync(Guid.NewGuid(), request, cases.Object, context, CancellationToken.None);

            Assert.True(result.GetType().Name.Contains("BadRequest"));
        }

        [Fact]
        public async Task AddCaseItemAsync_InvalidKind_ReturnsBadRequest()
        {
            (var cases, _) = CreateMocks();
            var operatorId = Guid.NewGuid();
            var request = new AddCaseItemRequestDto("InvalidKind", Guid.NewGuid(), "Test reason");
            var context = new DefaultHttpContext { User = CreatePrincipalWithOperatorId(operatorId) };

            IResult result = await CaseEndpoints.AddCaseItemAsync(Guid.NewGuid(), request, cases.Object, context, CancellationToken.None);

            Assert.True(result.GetType().Name.Contains("BadRequest"));
        }

        [Fact]
        public async Task AddCaseItemAsync_Success_ReturnsCreated()
        {
            (var cases, _) = CreateMocks();
            var operatorId = Guid.NewGuid();
            var caseId = Guid.NewGuid();
            var item = CreateCaseItem(caseId: caseId);
            var request = new AddCaseItemRequestDto("Media", item.MediaItemId!.Value, "Test reason");
            cases.Setup(c => c.AddItemAsync(caseId, CaseItemKind.Media, item.MediaItemId!.Value, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(item);
            var context = new DefaultHttpContext { User = CreatePrincipalWithOperatorId(operatorId) };

            IResult result = await CaseEndpoints.AddCaseItemAsync(caseId, request, cases.Object, context, CancellationToken.None);

            Assert.NotNull(result);
            Assert.True(result.GetType().Name.StartsWith("Created"));
        }

        #endregion

        #region RemoveCaseItemAsync Tests

        [Fact]
        public async Task RemoveCaseItemAsync_NoOperatorClaim_ReturnsUnauthorized()
        {
            (var cases, _) = CreateMocks();
            var request = new RemoveCaseItemRequestDto("No longer relevant");
            var context = new DefaultHttpContext();

            IResult result = await CaseEndpoints.RemoveCaseItemAsync(Guid.NewGuid(), request, cases.Object, context, CancellationToken.None);

            Assert.IsType<UnauthorizedHttpResult>(result);
        }

        [Fact]
        public async Task RemoveCaseItemAsync_EmptyReason_ReturnsBadRequest()
        {
            (var cases, _) = CreateMocks();
            var operatorId = Guid.NewGuid();
            var request = new RemoveCaseItemRequestDto("");
            var context = new DefaultHttpContext { User = CreatePrincipalWithOperatorId(operatorId) };

            IResult result = await CaseEndpoints.RemoveCaseItemAsync(Guid.NewGuid(), request, cases.Object, context, CancellationToken.None);

            Assert.True(result.GetType().Name.Contains("BadRequest"));
        }

        [Fact]
        public async Task RemoveCaseItemAsync_Success_ReturnsNoContent()
        {
            (var cases, _) = CreateMocks();
            var operatorId = Guid.NewGuid();
            var itemId = Guid.NewGuid();
            var request = new RemoveCaseItemRequestDto("No longer relevant");
            cases.Setup(c => c.RemoveItemAsync(itemId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            var context = new DefaultHttpContext { User = CreatePrincipalWithOperatorId(operatorId) };

            IResult result = await CaseEndpoints.RemoveCaseItemAsync(itemId, request, cases.Object, context, CancellationToken.None);

            Assert.IsType<NoContent>(result);
        }

        #endregion

        #region CloseCaseAsync Tests

        [Fact]
        public async Task CloseCaseAsync_NoOperatorClaim_ReturnsUnauthorized()
        {
            (var cases, _) = CreateMocks();
            var context = new DefaultHttpContext();

            IResult result = await CaseEndpoints.CloseCaseAsync(Guid.NewGuid(), cases.Object, context, CancellationToken.None);

            Assert.IsType<UnauthorizedHttpResult>(result);
        }

        [Fact]
        public async Task CloseCaseAsync_AlreadyClosed_ReturnsConflict()
        {
            (var cases, _) = CreateMocks();
            var operatorId = Guid.NewGuid();
            var caseId = Guid.NewGuid();
            cases.Setup(c => c.CloseAsync(caseId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Case is already closed"));
            var context = new DefaultHttpContext { User = CreatePrincipalWithOperatorId(operatorId) };

            IResult result = await CaseEndpoints.CloseCaseAsync(caseId, cases.Object, context, CancellationToken.None);

            Assert.True(result.GetType().Name.Contains("Conflict"));
        }

        [Fact]
        public async Task CloseCaseAsync_Success_ReturnsNoContent()
        {
            (var cases, _) = CreateMocks();
            var operatorId = Guid.NewGuid();
            var caseId = Guid.NewGuid();
            cases.Setup(c => c.CloseAsync(caseId, operatorId.ToString(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            var context = new DefaultHttpContext { User = CreatePrincipalWithOperatorId(operatorId) };

            IResult result = await CaseEndpoints.CloseCaseAsync(caseId, cases.Object, context, CancellationToken.None);

            Assert.IsType<NoContent>(result);
        }

        #endregion

        #region ReopenCaseAsync Tests

        [Fact]
        public async Task ReopenCaseAsync_NoOperatorClaim_ReturnsUnauthorized()
        {
            (var cases, _) = CreateMocks();
            var context = new DefaultHttpContext();

            IResult result = await CaseEndpoints.ReopenCaseAsync(Guid.NewGuid(), cases.Object, context, CancellationToken.None);

            Assert.IsType<UnauthorizedHttpResult>(result);
        }

        [Fact]
        public async Task ReopenCaseAsync_Success_ReturnsNoContent()
        {
            (var cases, _) = CreateMocks();
            var operatorId = Guid.NewGuid();
            var caseId = Guid.NewGuid();
            cases.Setup(c => c.ReopenAsync(caseId, operatorId.ToString(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            var context = new DefaultHttpContext { User = CreatePrincipalWithOperatorId(operatorId) };

            IResult result = await CaseEndpoints.ReopenCaseAsync(caseId, cases.Object, context, CancellationToken.None);

            Assert.IsType<NoContent>(result);
        }

        #endregion
    }
}
