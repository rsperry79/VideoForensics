using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Repositories;
using VideoForensics.Utils.Testing;

using Xunit;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    /// <summary>
    /// Tests for SecurityAuditService recording of failed logins against an unknown username.
    /// </summary>
    public class SecurityAuditServiceTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private SecurityAuditService _service = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            var loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            var unitOfWork = new UnitOfWork(_fixture.Factory, new TestServiceProvider(_fixture, loggerFactory), loggerFactory.CreateLogger<UnitOfWork>());
            _service = new SecurityAuditService(unitOfWork, NullLogger<SecurityAuditService>.Instance);
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task SecurityAuditServiceTests_RecordUnknownUserLogin_WritesOneEventWithNullOperatorAndTypedName()
        {
            // Act
            await _service.RecordUnknownUserLoginAsync("zumba", "192.168.1.50", "Invalid username", CancellationToken.None);

            // Assert
            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            SecurityEvent single = await db.SecurityEvents.SingleAsync();
            Assert.Null(single.OperatorId);
            Assert.Equal("zumba", single.AttemptedUsername);
            Assert.Equal(SecurityEventType.LoginAttemptFailure, single.EventType);
            Assert.False(single.Success);
            Assert.Equal("192.168.1.50", single.IpAddress);
            Assert.Equal("Invalid username", single.Reason);
        }

        [Fact]
        public async Task SecurityAuditServiceTests_GetOperatorEvents_UnknownOnly_ReturnsOnlyNullOperatorRows()
        {
            // Arrange - one operator event and one unknown-username event
            var operatorId = Guid.NewGuid();
            await _service.RecordLoginAttemptAsync(operatorId, "10.0.0.1", success: false, reason: "Invalid password", CancellationToken.None);
            await _service.RecordUnknownUserLoginAsync("zumba", "192.168.1.50", "Invalid username", CancellationToken.None);

            // Act
            var unknown = new List<SecurityEventDto>();
            await foreach (SecurityEventDto row in _service.GetOperatorEventsAsync(null, unknownOnly: true, skip: 0, take: 20, CancellationToken.None))
            {
                unknown.Add(row);
            }

            var forOperator = new List<SecurityEventDto>();
            await foreach (SecurityEventDto row in _service.GetOperatorEventsAsync(operatorId, unknownOnly: false, skip: 0, take: 20, CancellationToken.None))
            {
                forOperator.Add(row);
            }

            // Assert
            SecurityEventDto unknownRow = Assert.Single(unknown);
            Assert.Null(unknownRow.OperatorId);
            Assert.Equal("zumba", unknownRow.AttemptedUsername);
            SecurityEventDto operatorRow = Assert.Single(forOperator);
            Assert.Equal(operatorId, operatorRow.OperatorId);
        }

        [Fact]
        public async Task SecurityAuditServiceTests_RecordUnknownUserLogin_TrimsAndTruncatesUsername()
        {
            // Arrange
            string padded = "  " + new string('a', 300) + "  ";

            // Act
            await _service.RecordUnknownUserLoginAsync(padded, null, "Invalid username", CancellationToken.None);

            // Assert
            await using VideoForensicsDbContext db = await _fixture.Factory.CreateDbContextAsync();
            SecurityEvent single = await db.SecurityEvents.SingleAsync();
            Assert.Equal(new string('a', 256), single.AttemptedUsername);
        }
    }
}
