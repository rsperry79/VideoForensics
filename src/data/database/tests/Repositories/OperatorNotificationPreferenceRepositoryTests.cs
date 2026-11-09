using Microsoft.Extensions.Logging;

using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;
using VideoForensics.Utils.Testing;

using Xunit;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    public class OperatorNotificationPreferenceRepositoryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private OperatorNotificationPreferenceRepository _repository = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            var serviceProvider = new TestServiceProvider(_fixture, loggerFactory);
            var unitOfWork = new UnitOfWork(_fixture.Factory, serviceProvider, loggerFactory.CreateLogger<UnitOfWork>());
            _repository = new OperatorNotificationPreferenceRepository(unitOfWork);
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public void OperatorNotificationPreference_ParseEventTypes_TrimsAndIgnoresBlanks()
        {
            IReadOnlySet<string> names = NotificationEventTypes.Parse("Pairing,AuthFailure, ,  LogViewed");

            Assert.Equal(3, names.Count);
            Assert.Contains("Pairing", names);
            Assert.Contains("AuthFailure", names);
            Assert.Contains("LogViewed", names);
        }

        [Fact]
        public void OperatorNotificationPreference_JoinThenParse_RoundTrips()
        {
            var original = new[] { "PairingInitiated", "PairingCompleted", "LogViewed" };

            string joined = NotificationEventTypes.Join(original);
            IReadOnlySet<string> parsed = NotificationEventTypes.Parse(joined);

            Assert.Equal(original.Length, parsed.Count);
            Assert.True(parsed.SetEquals(original));
        }

        [Fact]
        public async Task OperatorNotificationPreferenceRepository_UpsertThenGet_RoundTripsEmailSettings()
        {
            var operatorId = Guid.NewGuid();
            var preference = new OperatorNotificationPreference
            {
                OperatorId = operatorId,
                PushEnabled = true,
                MinimumSeverity = NoticeSeverity.Warning,
                EmailEnabled = true,
                EmailEventTypes = NotificationEventTypes.Join(new[] { "PairingCompleted", "LogViewed" })
            };

            await _repository.UpsertAsync(preference, CancellationToken.None);

            OperatorNotificationPreference? retrieved = await _repository.GetAsync(operatorId, CancellationToken.None);
            Assert.NotNull(retrieved);
            Assert.True(retrieved.EmailEnabled);
            Assert.Equal(preference.EmailEventTypes, retrieved.EmailEventTypes);
            Assert.True(retrieved.EmailEventTypeSet.SetEquals(new[] { "PairingCompleted", "LogViewed" }));
        }
    }
}
