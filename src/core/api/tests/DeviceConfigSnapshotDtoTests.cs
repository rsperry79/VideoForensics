using System.Text.Json;

using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Core.Api.Contracts.Tests
{
    /// <summary>The snapshot DTO carries custom settings as a JSON object, one entry per stored key.</summary>
    public class DeviceConfigSnapshotDtoTests
    {
        [Fact]
        public void DeviceConfigSnapshot_ToDto_ThenToDomain_RoundTripsCustomSettings()
        {
            var entity = new DeviceConfigSnapshot
            {
                Id = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                CapturedAtUtc = DateTime.UtcNow,
                Source = DeviceConfigSource.Fetched,
                CustomSettings =
                [
                    new DeviceConfigSnapshotSetting { Id = Guid.NewGuid(), Key = "alpha", ValueJson = "1" },
                    new DeviceConfigSnapshotSetting { Id = Guid.NewGuid(), Key = "beta", ValueJson = "\"b\"" }
                ]
            };

            DeviceConfigSnapshotDto dto = entity.ToDto();

            Assert.Equal(1, dto.CustomSettings!["alpha"].GetInt32());
            Assert.Equal("b", dto.CustomSettings["beta"].GetString());

            DeviceConfigSnapshot back = dto.ToDomain();
            var rows = back.CustomSettings.OrderBy(r => r.Key, StringComparer.Ordinal).Select(r => (r.Key, r.ValueJson)).ToList();
            Assert.Equal(new List<(string, string)> { ("alpha", "1"), ("beta", "\"b\"") }, rows);
        }

        [Fact]
        public void DeviceConfigSnapshot_ToDto_NoSettings_GivesEmptyObject()
        {
            var entity = new DeviceConfigSnapshot { Id = Guid.NewGuid(), DeviceId = Guid.NewGuid(), CapturedAtUtc = DateTime.UtcNow };

            DeviceConfigSnapshotDto dto = entity.ToDto();

            Assert.NotNull(dto.CustomSettings);
            Assert.Empty(dto.CustomSettings);
        }
    }
}
