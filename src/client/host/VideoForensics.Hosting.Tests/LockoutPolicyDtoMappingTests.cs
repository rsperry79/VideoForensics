using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Entities;
using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public class LockoutPolicyDtoMappingTests
    {
        [Fact]
        public void ToDto_AutoBanFields_AreCarriedToDto()
        {
            var settings = new LockoutPolicySettings
            {
                Id = Guid.NewGuid(),
                MaxFailedAttempts = 5,
                LockoutDurationMinutes = 15,
                AutoBanEnabled = true,
                AutoBanDistinctAccountThreshold = 4,
                AutoBanWindowMinutes = 90,
                AutoBanDurationMinutes = 2880,
                UpdatedAtUtc = DateTime.UtcNow
            };

            LockoutPolicySettingsDto dto = settings.ToDto();

            Assert.True(dto.AutoBanEnabled);
            Assert.Equal(4, dto.AutoBanDistinctAccountThreshold);
            Assert.Equal(90, dto.AutoBanWindowMinutes);
            Assert.Equal(2880, dto.AutoBanDurationMinutes);
        }

        [Fact]
        public void ToDomain_AutoBanFields_AreCarriedToDomain()
        {
            var dto = new LockoutPolicySettingsDto(
                MaxFailedAttempts: 5,
                LockoutDurationMinutes: 15,
                BlockedCountryCodes: null,
                FailClosedOnLookupError: false,
                UpdatedAtUtc: DateTime.UtcNow,
                AutoBanEnabled: true,
                AutoBanDistinctAccountThreshold: 4,
                AutoBanWindowMinutes: 90,
                AutoBanDurationMinutes: 2880);

            LockoutPolicySettings settings = dto.ToDomain();

            Assert.True(settings.AutoBanEnabled);
            Assert.Equal(4, settings.AutoBanDistinctAccountThreshold);
            Assert.Equal(90, settings.AutoBanWindowMinutes);
            Assert.Equal(2880, settings.AutoBanDurationMinutes);
        }

        [Fact]
        public void ToDomain_DefaultAutoBanFields_AreDisabledWithDefaultThresholds()
        {
            var dto = new LockoutPolicySettingsDto(
                MaxFailedAttempts: 5,
                LockoutDurationMinutes: 15,
                BlockedCountryCodes: null,
                FailClosedOnLookupError: false,
                UpdatedAtUtc: DateTime.UtcNow);

            LockoutPolicySettings settings = dto.ToDomain();

            Assert.False(settings.AutoBanEnabled);
            Assert.Equal(3, settings.AutoBanDistinctAccountThreshold);
            Assert.Equal(60, settings.AutoBanWindowMinutes);
            Assert.Equal(1440, settings.AutoBanDurationMinutes);
        }
    }
}
