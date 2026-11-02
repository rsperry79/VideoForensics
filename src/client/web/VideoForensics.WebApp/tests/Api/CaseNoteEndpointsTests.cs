using Microsoft.AspNetCore.Http;

using Moq;

using System.Security.Claims;

using VideoForensics.Client.WebApp.Api;
using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Api
{
    public class CaseNoteEndpoints_Tests
    {
        private static ClaimsPrincipal CreatePrincipalWithOperatorId(Guid operatorId)
        {
            var claims = new List<Claim>
            {
                new(VideoForensicsClaimTypes.OperatorId, operatorId.ToString()),
                new(ClaimTypes.Role, OperatorRole.Admin.ToString())
            };
            return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        }

        private static Note CreateCaseNote(Guid caseId, string body = "Observed motion at 14:02.")
        {
            return new Note
            {
                Id = Guid.NewGuid(),
                TargetKind = NoteTargetKind.Case,
                TargetId = caseId,
                Body = body,
                AddedBy = "operator-1",
                AddedAtUtc = DateTime.UtcNow
            };
        }

        [Fact]
        public async Task CaseNoteEndpoints_Add_Success_ReturnsCreated()
        {
            var caseId = Guid.NewGuid();
            var operatorId = Guid.NewGuid();
            var notes = new Mock<INoteRepository>();
            Note saved = CreateCaseNote(caseId);
            _ = notes.Setup(n => n.AddAsync(NoteTargetKind.Case, caseId, "Observed motion at 14:02.", operatorId.ToString(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(saved);
            var context = new DefaultHttpContext { User = CreatePrincipalWithOperatorId(operatorId) };

            IResult result = await CaseEndpoints.AddCaseNoteAsync(
                caseId, new AddCaseNoteRequest("Observed motion at 14:02."), notes.Object, context, CancellationToken.None);

            notes.Verify(n => n.AddAsync(NoteTargetKind.Case, caseId, "Observed motion at 14:02.", operatorId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
            Assert.Contains("Created", result.GetType().Name);
        }

        [Fact]
        public async Task CaseNoteEndpoints_Add_EmptyBody_ReturnsBadRequest()
        {
            var caseId = Guid.NewGuid();
            var operatorId = Guid.NewGuid();
            var notes = new Mock<INoteRepository>();
            _ = notes.Setup(n => n.AddAsync(NoteTargetKind.Case, caseId, "   ", operatorId.ToString(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Note body is required.", "body"));
            var context = new DefaultHttpContext { User = CreatePrincipalWithOperatorId(operatorId) };

            IResult result = await CaseEndpoints.AddCaseNoteAsync(
                caseId, new AddCaseNoteRequest("   "), notes.Object, context, CancellationToken.None);

            Assert.Contains("BadRequest", result.GetType().Name);
        }

        [Fact]
        public async Task CaseNoteEndpoints_Add_UnknownCase_ReturnsNotFound()
        {
            var caseId = Guid.NewGuid();
            var operatorId = Guid.NewGuid();
            var notes = new Mock<INoteRepository>();
            _ = notes.Setup(n => n.AddAsync(NoteTargetKind.Case, caseId, It.IsAny<string>(), operatorId.ToString(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new KeyNotFoundException("Case not found."));
            var context = new DefaultHttpContext { User = CreatePrincipalWithOperatorId(operatorId) };

            IResult result = await CaseEndpoints.AddCaseNoteAsync(
                caseId, new AddCaseNoteRequest("Note"), notes.Object, context, CancellationToken.None);

            Assert.Contains("NotFound", result.GetType().Name);
        }

        [Fact]
        public async Task CaseNoteEndpoints_Add_ClosedCase_ReturnsConflict()
        {
            var caseId = Guid.NewGuid();
            var operatorId = Guid.NewGuid();
            var notes = new Mock<INoteRepository>();
            _ = notes.Setup(n => n.AddAsync(NoteTargetKind.Case, caseId, It.IsAny<string>(), operatorId.ToString(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Case is closed."));
            var context = new DefaultHttpContext { User = CreatePrincipalWithOperatorId(operatorId) };

            IResult result = await CaseEndpoints.AddCaseNoteAsync(
                caseId, new AddCaseNoteRequest("Note"), notes.Object, context, CancellationToken.None);

            Assert.Contains("Conflict", result.GetType().Name);
        }

        [Fact]
        public async Task CaseNoteEndpoints_Add_MissingOperatorClaim_ReturnsUnauthorized()
        {
            var caseId = Guid.NewGuid();
            var notes = new Mock<INoteRepository>();
            var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) };

            IResult result = await CaseEndpoints.AddCaseNoteAsync(
                caseId, new AddCaseNoteRequest("Note"), notes.Object, context, CancellationToken.None);

            Assert.Contains("Unauthorized", result.GetType().Name);
            notes.Verify(n => n.AddAsync(It.IsAny<NoteTargetKind>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CaseNoteEndpoints_List_UnknownCase_ReturnsEmptyOk()
        {
            var caseId = Guid.NewGuid();
            var notes = new Mock<INoteRepository>();
            _ = notes.Setup(n => n.ListAsync(NoteTargetKind.Case, caseId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<Note>());

            IResult result = await CaseEndpoints.ListCaseNotesAsync(caseId, notes.Object, CancellationToken.None);

            Assert.Contains("Ok", result.GetType().Name);
            notes.Verify(n => n.ListAsync(NoteTargetKind.Case, caseId, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
