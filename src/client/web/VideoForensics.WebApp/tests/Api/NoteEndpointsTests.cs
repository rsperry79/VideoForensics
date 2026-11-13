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
    public class NoteEndpoints_Tests
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

        private static Note CreateNote(NoteTargetKind kind, Guid targetId, string body = "Observed jamming at 14:02.")
        {
            return new Note
            {
                Id = Guid.NewGuid(),
                TargetKind = kind,
                TargetId = targetId,
                Body = body,
                AddedBy = "operator-1",
                AddedAtUtc = DateTime.UtcNow
            };
        }

        private static JammingIncidentRecord CreateIncident(Guid incidentId)
        {
            return new JammingIncidentRecord { Id = incidentId, DeviceId = Guid.NewGuid() };
        }

        private static MediaFlag CreateFlag(Guid flagId)
        {
            return new MediaFlag { Id = flagId, DeviceId = Guid.NewGuid() };
        }

        [Fact]
        public async Task NoteEndpoints_AddToIncident_Success_ReturnsCreated()
        {
            var incidentId = Guid.NewGuid();
            var operatorId = Guid.NewGuid();
            var cases = new Mock<ICaseRepository>();
            var jamming = new Mock<IJammingRepository>();
            var flags = new Mock<IMediaFlagRepository>();
            var notes = new Mock<INoteRepository>();
            _ = jamming.Setup(j => j.GetIncidentAsync(incidentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateIncident(incidentId));
            Note saved = CreateNote(NoteTargetKind.JammingIncident, incidentId);
            _ = notes.Setup(n => n.AddAsync(NoteTargetKind.JammingIncident, incidentId, "Observed jamming at 14:02.", operatorId.ToString(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(saved);
            var context = new DefaultHttpContext { User = CreatePrincipalWithOperatorId(operatorId) };

            IResult result = await NoteEndpoints.AddNoteAsync(
                "JammingIncident", incidentId, new AddNoteRequest("Observed jamming at 14:02."),
                cases.Object, jamming.Object, flags.Object, notes.Object, context, CancellationToken.None);

            notes.Verify(n => n.AddAsync(NoteTargetKind.JammingIncident, incidentId, "Observed jamming at 14:02.", operatorId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
            Assert.Contains("Created", result.GetType().Name);
        }

        [Fact]
        public async Task NoteEndpoints_AddToFlag_Success_ReturnsCreated()
        {
            var flagId = Guid.NewGuid();
            var operatorId = Guid.NewGuid();
            var cases = new Mock<ICaseRepository>();
            var jamming = new Mock<IJammingRepository>();
            var flags = new Mock<IMediaFlagRepository>();
            var notes = new Mock<INoteRepository>();
            _ = flags.Setup(f => f.GetAsync(flagId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateFlag(flagId));
            Note saved = CreateNote(NoteTargetKind.MediaFlag, flagId);
            _ = notes.Setup(n => n.AddAsync(NoteTargetKind.MediaFlag, flagId, "Checked against the clip.", operatorId.ToString(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(saved);
            var context = new DefaultHttpContext { User = CreatePrincipalWithOperatorId(operatorId) };

            IResult result = await NoteEndpoints.AddNoteAsync(
                "MediaFlag", flagId, new AddNoteRequest("Checked against the clip."),
                cases.Object, jamming.Object, flags.Object, notes.Object, context, CancellationToken.None);

            notes.Verify(n => n.AddAsync(NoteTargetKind.MediaFlag, flagId, "Checked against the clip.", operatorId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
            Assert.Contains("Created", result.GetType().Name);
        }

        [Fact]
        public async Task NoteEndpoints_AddToUnknownIncident_ReturnsNotFound()
        {
            var incidentId = Guid.NewGuid();
            var operatorId = Guid.NewGuid();
            var cases = new Mock<ICaseRepository>();
            var jamming = new Mock<IJammingRepository>();
            var flags = new Mock<IMediaFlagRepository>();
            var notes = new Mock<INoteRepository>();
            _ = jamming.Setup(j => j.GetIncidentAsync(incidentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((JammingIncidentRecord?)null);
            var context = new DefaultHttpContext { User = CreatePrincipalWithOperatorId(operatorId) };

            IResult result = await NoteEndpoints.AddNoteAsync(
                "JammingIncident", incidentId, new AddNoteRequest("Note"),
                cases.Object, jamming.Object, flags.Object, notes.Object, context, CancellationToken.None);

            Assert.Contains("NotFound", result.GetType().Name);
            notes.Verify(n => n.AddAsync(It.IsAny<NoteTargetKind>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task NoteEndpoints_AddToUnknownFlag_ReturnsNotFound()
        {
            var flagId = Guid.NewGuid();
            var operatorId = Guid.NewGuid();
            var cases = new Mock<ICaseRepository>();
            var jamming = new Mock<IJammingRepository>();
            var flags = new Mock<IMediaFlagRepository>();
            var notes = new Mock<INoteRepository>();
            _ = flags.Setup(f => f.GetAsync(flagId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((MediaFlag?)null);
            var context = new DefaultHttpContext { User = CreatePrincipalWithOperatorId(operatorId) };

            IResult result = await NoteEndpoints.AddNoteAsync(
                "MediaFlag", flagId, new AddNoteRequest("Note"),
                cases.Object, jamming.Object, flags.Object, notes.Object, context, CancellationToken.None);

            Assert.Contains("NotFound", result.GetType().Name);
            notes.Verify(n => n.AddAsync(It.IsAny<NoteTargetKind>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task NoteEndpoints_AddWithUnknownKind_ReturnsBadRequest()
        {
            var operatorId = Guid.NewGuid();
            var cases = new Mock<ICaseRepository>();
            var jamming = new Mock<IJammingRepository>();
            var flags = new Mock<IMediaFlagRepository>();
            var notes = new Mock<INoteRepository>();
            var context = new DefaultHttpContext { User = CreatePrincipalWithOperatorId(operatorId) };

            IResult result = await NoteEndpoints.AddNoteAsync(
                "Bogus", Guid.NewGuid(), new AddNoteRequest("Note"),
                cases.Object, jamming.Object, flags.Object, notes.Object, context, CancellationToken.None);

            Assert.Contains("BadRequest", result.GetType().Name);
            notes.Verify(n => n.AddAsync(It.IsAny<NoteTargetKind>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task NoteEndpoints_AddWithNumericKind_ReturnsBadRequest()
        {
            var operatorId = Guid.NewGuid();
            var cases = new Mock<ICaseRepository>();
            var jamming = new Mock<IJammingRepository>();
            var flags = new Mock<IMediaFlagRepository>();
            var notes = new Mock<INoteRepository>();
            var context = new DefaultHttpContext { User = CreatePrincipalWithOperatorId(operatorId) };

            IResult result = await NoteEndpoints.AddNoteAsync(
                "7", Guid.NewGuid(), new AddNoteRequest("Note"),
                cases.Object, jamming.Object, flags.Object, notes.Object, context, CancellationToken.None);

            Assert.Contains("BadRequest", result.GetType().Name);
            notes.Verify(n => n.AddAsync(It.IsAny<NoteTargetKind>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task NoteEndpoints_AddWithMissingOperatorClaim_ReturnsUnauthorized()
        {
            var incidentId = Guid.NewGuid();
            var cases = new Mock<ICaseRepository>();
            var jamming = new Mock<IJammingRepository>();
            var flags = new Mock<IMediaFlagRepository>();
            var notes = new Mock<INoteRepository>();
            _ = jamming.Setup(j => j.GetIncidentAsync(incidentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateIncident(incidentId));
            var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) };

            IResult result = await NoteEndpoints.AddNoteAsync(
                "JammingIncident", incidentId, new AddNoteRequest("Note"),
                cases.Object, jamming.Object, flags.Object, notes.Object, context, CancellationToken.None);

            Assert.Contains("Unauthorized", result.GetType().Name);
            notes.Verify(n => n.AddAsync(It.IsAny<NoteTargetKind>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task NoteEndpoints_AddToClosedCase_ReturnsConflict()
        {
            var caseId = Guid.NewGuid();
            var operatorId = Guid.NewGuid();
            var cases = new Mock<ICaseRepository>();
            var jamming = new Mock<IJammingRepository>();
            var flags = new Mock<IMediaFlagRepository>();
            var notes = new Mock<INoteRepository>();
            _ = cases.Setup(c => c.GetAsync(caseId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ForensicCase { CaseNumber = "C-1", Title = "Closed case", CreatedBy = "operator-1" });
            _ = notes.Setup(n => n.AddAsync(NoteTargetKind.Case, caseId, It.IsAny<string>(), operatorId.ToString(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Case is closed."));
            var context = new DefaultHttpContext { User = CreatePrincipalWithOperatorId(operatorId) };

            IResult result = await NoteEndpoints.AddNoteAsync(
                "Case", caseId, new AddNoteRequest("Note"),
                cases.Object, jamming.Object, flags.Object, notes.Object, context, CancellationToken.None);

            Assert.Contains("Conflict", result.GetType().Name);
        }

        [Fact]
        public async Task NoteEndpoints_ListForUnknownIncident_ReturnsNotFound()
        {
            var incidentId = Guid.NewGuid();
            var cases = new Mock<ICaseRepository>();
            var jamming = new Mock<IJammingRepository>();
            var flags = new Mock<IMediaFlagRepository>();
            var notes = new Mock<INoteRepository>();
            _ = jamming.Setup(j => j.GetIncidentAsync(incidentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((JammingIncidentRecord?)null);

            IResult result = await NoteEndpoints.ListNotesAsync(
                "JammingIncident", incidentId, cases.Object, jamming.Object, flags.Object, notes.Object, CancellationToken.None);

            Assert.Contains("NotFound", result.GetType().Name);
            notes.Verify(n => n.ListAsync(It.IsAny<NoteTargetKind>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task NoteEndpoints_ListForKnownIncident_ReturnsOkWithNotes()
        {
            var incidentId = Guid.NewGuid();
            var cases = new Mock<ICaseRepository>();
            var jamming = new Mock<IJammingRepository>();
            var flags = new Mock<IMediaFlagRepository>();
            var notes = new Mock<INoteRepository>();
            _ = jamming.Setup(j => j.GetIncidentAsync(incidentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateIncident(incidentId));
            Note stored = CreateNote(NoteTargetKind.JammingIncident, incidentId, "First note.");
            _ = notes.Setup(n => n.ListAsync(NoteTargetKind.JammingIncident, incidentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync([stored]);

            IResult result = await NoteEndpoints.ListNotesAsync(
                "JammingIncident", incidentId, cases.Object, jamming.Object, flags.Object, notes.Object, CancellationToken.None);

            IValueHttpResult ok = Assert.IsAssignableFrom<IValueHttpResult>(result);
            IEnumerable<NoteDto> dtos = Assert.IsAssignableFrom<IEnumerable<NoteDto>>(ok.Value);
            NoteDto dto = Assert.Single(dtos);
            Assert.Equal(stored.Id, dto.Id);
            Assert.Equal(NoteTargetKindDto.JammingIncident, dto.TargetKind);
            Assert.Equal("First note.", dto.Body);
        }
    }
}
