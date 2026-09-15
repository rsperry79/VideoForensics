using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>
    /// Tests for SecurityAuditLogRepository.
    /// Verifies server security event logging and audit trail tracking.
    /// </summary>
    public class SecurityAuditLogRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private SecurityAuditLogRepository _repository = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            _repository = new SecurityAuditLogRepository(_fixture.Factory, loggerFactory.CreateLogger<SecurityAuditLogRepository>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task SecurityAuditLogRepository_AppendAsync_CreatesSecurityAuditEntry()
        {
            // Arrange
            var operatorId = Guid.NewGuid();
            var pairedDeviceId = Guid.NewGuid();
            var entry = new SecurityAuditLogEntry
            {
                Id = Guid.NewGuid(),
                TimestampUtc = DateTime.UtcNow,
                EventType = SecurityAuditEventTypes.AuthSuccess,
                OperatorId = operatorId,
                PairedDeviceId = pairedDeviceId,
                SourceIp = "192.168.1.100",
                Details = "Authentication successful",
                IsUrgent = false
            };

            // Act
            var result = await _repository.AppendAsync(entry, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(entry.Id, result.Id);
            Assert.Equal(entry.EventType, result.EventType);
            Assert.Equal(operatorId, result.OperatorId);
            Assert.Equal(pairedDeviceId, result.PairedDeviceId);
            Assert.Equal("192.168.1.100", result.SourceIp);
            Assert.False(result.IsUrgent);
        }

        [Fact]
        public async Task SecurityAuditLogRepository_AppendAsync_PersistsEntryToDB()
        {
            // Arrange
            var operatorId = Guid.NewGuid();
            var entry = new SecurityAuditLogEntry
            {
                Id = Guid.NewGuid(),
                TimestampUtc = DateTime.UtcNow,
                EventType = SecurityAuditEventTypes.PairingCompleted,
                OperatorId = operatorId,
                PairedDeviceId = Guid.NewGuid(),
                SourceIp = "10.0.0.1",
                Details = "Pairing completed successfully",
                IsUrgent = true
            };

            // Act
            await _repository.AppendAsync(entry, CancellationToken.None);

            // Assert - retrieve via ListAsync to verify persistence
            var entries = await _repository.ListAsync(operatorId, 10, CancellationToken.None);
            Assert.Single(entries);
            Assert.Equal(entry.Id, entries[0].Id);
            Assert.Equal(SecurityAuditEventTypes.PairingCompleted, entries[0].EventType);
        }

        [Fact]
        public async Task SecurityAuditLogRepository_AppendAsync_PreservesUrgentFlag()
        {
            // Arrange
            var urgentEntry = new SecurityAuditLogEntry
            {
                Id = Guid.NewGuid(),
                TimestampUtc = DateTime.UtcNow,
                EventType = SecurityAuditEventTypes.AuthFailure,
                OperatorId = Guid.NewGuid(),
                PairedDeviceId = null,
                SourceIp = "192.168.1.50",
                Details = "Authentication failed",
                IsUrgent = true
            };

            var nonUrgentEntry = new SecurityAuditLogEntry
            {
                Id = Guid.NewGuid(),
                TimestampUtc = DateTime.UtcNow,
                EventType = SecurityAuditEventTypes.SessionVerified,
                OperatorId = Guid.NewGuid(),
                PairedDeviceId = null,
                SourceIp = "192.168.1.60",
                Details = "Session verified",
                IsUrgent = false
            };

            // Act
            var resultUrgent = await _repository.AppendAsync(urgentEntry, CancellationToken.None);
            var resultNonUrgent = await _repository.AppendAsync(nonUrgentEntry, CancellationToken.None);

            // Assert
            Assert.True(resultUrgent.IsUrgent);
            Assert.False(resultNonUrgent.IsUrgent);
        }

        [Fact]
        public async Task SecurityAuditLogRepository_AppendAsync_AllowsNullOptionalFields()
        {
            // Arrange
            var entry = new SecurityAuditLogEntry
            {
                Id = Guid.NewGuid(),
                TimestampUtc = DateTime.UtcNow,
                EventType = SecurityAuditEventTypes.ProviderRateLimitHit,
                OperatorId = null,
                PairedDeviceId = null,
                SourceIp = null,
                Details = null,
                IsUrgent = true
            };

            // Act
            var result = await _repository.AppendAsync(entry, CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Null(result.OperatorId);
            Assert.Null(result.PairedDeviceId);
            Assert.Null(result.SourceIp);
            Assert.Null(result.Details);
        }

        [Fact]
        public async Task SecurityAuditLogRepository_ListAsync_ReturnsAllEntriesWhenNoFilter()
        {
            // Arrange
            var operator1 = Guid.NewGuid();
            var operator2 = Guid.NewGuid();

            await _repository.AppendAsync(new SecurityAuditLogEntry
            {
                Id = Guid.NewGuid(),
                TimestampUtc = DateTime.UtcNow,
                EventType = SecurityAuditEventTypes.AuthSuccess,
                OperatorId = operator1,
                PairedDeviceId = null,
                SourceIp = "192.168.1.1",
                Details = null,
                IsUrgent = false
            }, CancellationToken.None);

            await _repository.AppendAsync(new SecurityAuditLogEntry
            {
                Id = Guid.NewGuid(),
                TimestampUtc = DateTime.UtcNow.AddSeconds(1),
                EventType = SecurityAuditEventTypes.AuthFailure,
                OperatorId = operator2,
                PairedDeviceId = null,
                SourceIp = "192.168.1.2",
                Details = null,
                IsUrgent = true
            }, CancellationToken.None);

            // Act
            var entries = await _repository.ListAsync(null, 10, CancellationToken.None);

            // Assert
            Assert.Equal(2, entries.Count);
        }

        [Fact]
        public async Task SecurityAuditLogRepository_ListAsync_FiltersByOperatorId()
        {
            // Arrange
            var targetOperator = Guid.NewGuid();
            var otherOperator = Guid.NewGuid();

            await _repository.AppendAsync(new SecurityAuditLogEntry
            {
                Id = Guid.NewGuid(),
                TimestampUtc = DateTime.UtcNow,
                EventType = SecurityAuditEventTypes.PairingInitiated,
                OperatorId = targetOperator,
                PairedDeviceId = null,
                SourceIp = "192.168.1.1",
                Details = null,
                IsUrgent = false
            }, CancellationToken.None);

            await _repository.AppendAsync(new SecurityAuditLogEntry
            {
                Id = Guid.NewGuid(),
                TimestampUtc = DateTime.UtcNow.AddSeconds(1),
                EventType = SecurityAuditEventTypes.AuthSuccess,
                OperatorId = otherOperator,
                PairedDeviceId = null,
                SourceIp = "192.168.1.2",
                Details = null,
                IsUrgent = false
            }, CancellationToken.None);

            await _repository.AppendAsync(new SecurityAuditLogEntry
            {
                Id = Guid.NewGuid(),
                TimestampUtc = DateTime.UtcNow.AddSeconds(2),
                EventType = SecurityAuditEventTypes.PairingCompleted,
                OperatorId = targetOperator,
                PairedDeviceId = null,
                SourceIp = "192.168.1.3",
                Details = null,
                IsUrgent = true
            }, CancellationToken.None);

            // Act
            var entries = await _repository.ListAsync(targetOperator, 10, CancellationToken.None);

            // Assert
            Assert.Equal(2, entries.Count);
            Assert.All(entries, e => Assert.Equal(targetOperator, e.OperatorId));
        }

        [Fact]
        public async Task SecurityAuditLogRepository_ListAsync_ReturnsEmptyWhenOperatorHasNoEntries()
        {
            // Arrange
            var targetOperator = Guid.NewGuid();
            var otherOperator = Guid.NewGuid();

            await _repository.AppendAsync(new SecurityAuditLogEntry
            {
                Id = Guid.NewGuid(),
                TimestampUtc = DateTime.UtcNow,
                EventType = SecurityAuditEventTypes.AuthSuccess,
                OperatorId = otherOperator,
                PairedDeviceId = null,
                SourceIp = "192.168.1.1",
                Details = null,
                IsUrgent = false
            }, CancellationToken.None);

            // Act
            var entries = await _repository.ListAsync(targetOperator, 10, CancellationToken.None);

            // Assert
            Assert.Empty(entries);
        }

        [Fact]
        public async Task SecurityAuditLogRepository_ListAsync_OrdersByTimestampDescending()
        {
            // Arrange
            var operatorId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            var entry1 = new SecurityAuditLogEntry
            {
                Id = Guid.NewGuid(),
                TimestampUtc = now,
                EventType = SecurityAuditEventTypes.AuthSuccess,
                OperatorId = operatorId,
                PairedDeviceId = null,
                SourceIp = "192.168.1.1",
                Details = null,
                IsUrgent = false
            };

            var entry2 = new SecurityAuditLogEntry
            {
                Id = Guid.NewGuid(),
                TimestampUtc = now.AddSeconds(5),
                EventType = SecurityAuditEventTypes.AuthFailure,
                OperatorId = operatorId,
                PairedDeviceId = null,
                SourceIp = "192.168.1.2",
                Details = null,
                IsUrgent = true
            };

            var entry3 = new SecurityAuditLogEntry
            {
                Id = Guid.NewGuid(),
                TimestampUtc = now.AddSeconds(10),
                EventType = SecurityAuditEventTypes.SessionVerified,
                OperatorId = operatorId,
                PairedDeviceId = null,
                SourceIp = "192.168.1.3",
                Details = null,
                IsUrgent = false
            };

            await _repository.AppendAsync(entry1, CancellationToken.None);
            await _repository.AppendAsync(entry2, CancellationToken.None);
            await _repository.AppendAsync(entry3, CancellationToken.None);

            // Act
            var entries = await _repository.ListAsync(operatorId, 10, CancellationToken.None);

            // Assert
            Assert.Equal(3, entries.Count);
            Assert.Equal(entry3.Id, entries[0].Id); // Most recent
            Assert.Equal(entry2.Id, entries[1].Id);
            Assert.Equal(entry1.Id, entries[2].Id); // Oldest
        }

        [Fact]
        public async Task SecurityAuditLogRepository_ListAsync_RespectsMaxResults()
        {
            // Arrange
            var operatorId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            for (int i = 0; i < 10; i++)
            {
                await _repository.AppendAsync(new SecurityAuditLogEntry
                {
                    Id = Guid.NewGuid(),
                    TimestampUtc = now.AddSeconds(i),
                    EventType = SecurityAuditEventTypes.AuthSuccess,
                    OperatorId = operatorId,
                    PairedDeviceId = null,
                    SourceIp = "192.168.1.1",
                    Details = null,
                    IsUrgent = false
                }, CancellationToken.None);
            }

            // Act
            var entries = await _repository.ListAsync(operatorId, 5, CancellationToken.None);

            // Assert
            Assert.Equal(5, entries.Count);
        }

        [Fact]
        public async Task SecurityAuditLogRepository_ListAsync_ReturnsFewerResultsIfLessThanMaxExists()
        {
            // Arrange
            var operatorId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            for (int i = 0; i < 3; i++)
            {
                await _repository.AppendAsync(new SecurityAuditLogEntry
                {
                    Id = Guid.NewGuid(),
                    TimestampUtc = now.AddSeconds(i),
                    EventType = SecurityAuditEventTypes.AuthSuccess,
                    OperatorId = operatorId,
                    PairedDeviceId = null,
                    SourceIp = "192.168.1.1",
                    Details = null,
                    IsUrgent = false
                }, CancellationToken.None);
            }

            // Act
            var entries = await _repository.ListAsync(operatorId, 10, CancellationToken.None);

            // Assert
            Assert.Equal(3, entries.Count);
        }

        [Fact]
        public async Task SecurityAuditLogRepository_ListAsync_WithFilterAndMaxResults()
        {
            // Arrange
            var operator1 = Guid.NewGuid();
            var operator2 = Guid.NewGuid();
            var now = DateTime.UtcNow;

            // Add 8 entries for operator1
            for (int i = 0; i < 8; i++)
            {
                await _repository.AppendAsync(new SecurityAuditLogEntry
                {
                    Id = Guid.NewGuid(),
                    TimestampUtc = now.AddSeconds(i),
                    EventType = SecurityAuditEventTypes.AuthSuccess,
                    OperatorId = operator1,
                    PairedDeviceId = null,
                    SourceIp = "192.168.1.1",
                    Details = null,
                    IsUrgent = false
                }, CancellationToken.None);
            }

            // Add 5 entries for operator2
            for (int i = 0; i < 5; i++)
            {
                await _repository.AppendAsync(new SecurityAuditLogEntry
                {
                    Id = Guid.NewGuid(),
                    TimestampUtc = now.AddSeconds(i),
                    EventType = SecurityAuditEventTypes.AuthFailure,
                    OperatorId = operator2,
                    PairedDeviceId = null,
                    SourceIp = "192.168.1.2",
                    Details = null,
                    IsUrgent = true
                }, CancellationToken.None);
            }

            // Act
            var operator1Entries = await _repository.ListAsync(operator1, 5, CancellationToken.None);
            var operator2Entries = await _repository.ListAsync(operator2, 3, CancellationToken.None);

            // Assert
            Assert.Equal(5, operator1Entries.Count); // max results limit applied
            Assert.All(operator1Entries, e => Assert.Equal(operator1, e.OperatorId));

            Assert.Equal(3, operator2Entries.Count); // max results limit applied
            Assert.All(operator2Entries, e => Assert.Equal(operator2, e.OperatorId));
        }

        [Fact]
        public async Task SecurityAuditLogRepository_ListAsync_HandlesMultipleEventTypes()
        {
            // Arrange
            var operatorId = Guid.NewGuid();
            var eventTypes = new[]
            {
                SecurityAuditEventTypes.PairingInitiated,
                SecurityAuditEventTypes.PairingCompleted,
                SecurityAuditEventTypes.AuthSuccess,
                SecurityAuditEventTypes.AuthFailure,
                SecurityAuditEventTypes.SessionVerified
            };

            var now = DateTime.UtcNow;
            foreach (var eventType in eventTypes)
            {
                await _repository.AppendAsync(new SecurityAuditLogEntry
                {
                    Id = Guid.NewGuid(),
                    TimestampUtc = now.AddSeconds(Array.IndexOf(eventTypes, eventType)),
                    EventType = eventType,
                    OperatorId = operatorId,
                    PairedDeviceId = null,
                    SourceIp = "192.168.1.1",
                    Details = null,
                    IsUrgent = false
                }, CancellationToken.None);
            }

            // Act
            var entries = await _repository.ListAsync(operatorId, 10, CancellationToken.None);

            // Assert
            Assert.Equal(5, entries.Count);
            var retrievedEventTypes = entries.Select(e => e.EventType).ToList();
            foreach (var eventType in eventTypes)
            {
                Assert.Contains(eventType, retrievedEventTypes);
            }
        }

        [Fact]
        public async Task SecurityAuditLogRepository_ListAsync_WithPairedDeviceInfo()
        {
            // Arrange
            var operatorId = Guid.NewGuid();
            var pairedDevice1 = Guid.NewGuid();
            var pairedDevice2 = Guid.NewGuid();
            var now = DateTime.UtcNow;

            await _repository.AppendAsync(new SecurityAuditLogEntry
            {
                Id = Guid.NewGuid(),
                TimestampUtc = now,
                EventType = SecurityAuditEventTypes.PairingCompleted,
                OperatorId = operatorId,
                PairedDeviceId = pairedDevice1,
                SourceIp = "192.168.1.1",
                Details = "Device paired",
                IsUrgent = true
            }, CancellationToken.None);

            await _repository.AppendAsync(new SecurityAuditLogEntry
            {
                Id = Guid.NewGuid(),
                TimestampUtc = now.AddSeconds(1),
                EventType = SecurityAuditEventTypes.TunnelStarted,
                OperatorId = operatorId,
                PairedDeviceId = pairedDevice2,
                SourceIp = "192.168.1.2",
                Details = "Tunnel established",
                IsUrgent = true
            }, CancellationToken.None);

            // Act
            var entries = await _repository.ListAsync(operatorId, 10, CancellationToken.None);

            // Assert
            Assert.Equal(2, entries.Count);
            Assert.Equal(pairedDevice2, entries[0].PairedDeviceId); // Most recent
            Assert.Equal(pairedDevice1, entries[1].PairedDeviceId);
        }
    }
}
