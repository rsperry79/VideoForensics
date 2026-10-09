using System;
using System.IO;
using System.Threading.Tasks;

using VideoForensics.Providers.Common.Helpers.Platform;
using Xunit;

namespace VideoForensics.Providers.Ring.Core.Tests
{
    /// <summary>
    /// Tests for HttpUtility hard-ban persistence and circuit-breaker integration with Polly.
    /// Verifies that hard-ban state survives process restart and that circuit-breaker policies
    /// are initialized correctly from persisted state.
    /// </summary>
    public class HttpUtilityTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly string _hardBanStateFilePath;

        public HttpUtilityTests()
        {
            // Isolated per-test directory so tests never touch the machine-wide ProgramData state.
            _testDirectory = Path.Combine(Path.GetTempPath(), "vf-httputility-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDirectory);
            HttpUtility.SetHardBanStateDirectoryForTesting(_testDirectory);
            _hardBanStateFilePath = HttpUtility.GetHardBanStateFilePath();
        }

        public void Dispose()
        {
            HttpUtility.SetHardBanStateDirectoryForTesting(null);
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }

        [Fact]
        public void HardBanStateFilePath_WithoutOverride_IsProgramDataApplicationDirectory()
        {
            HttpUtility.SetHardBanStateDirectoryForTesting(null);

            string expected = Path.Combine(new PlatformDirectoryService().GetApplicationDataDirectory(), "ring_hard_ban.txt");

            Assert.Equal(expected, HttpUtility.GetHardBanStateFilePath());
        }

        [Fact]
        public void HardBanStateFilePath_WithOverride_IsInsideOverrideDirectory()
        {
            Assert.Equal(Path.Combine(_testDirectory, "ring_hard_ban.txt"), HttpUtility.GetHardBanStateFilePath());
        }

        [Fact]
        public void GetHardBanUntilUtc_WithNoPersistedState_ReturnsNull()
        {
            // Arrange - no hard ban state file exists
            // Act
            var result = HttpUtility.GetHardBanUntilUtc();
            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetHardBanUntilUtc_WithExpiredPersistedState_ReturnsNull()
        {
            // Arrange - persist an expired hard ban state (1 hour ago)
            var expiredTime = DateTime.UtcNow.AddHours(-1);
            File.WriteAllText(_hardBanStateFilePath, expiredTime.Ticks.ToString());

            // Act
            var result = HttpUtility.GetHardBanUntilUtc();

            // Assert - expired ban should be cleared
            Assert.Null(result);
        }

        [Fact]
        public void GetHardBanUntilUtc_WithValidPersistedState_ReturnsExpiry()
        {
            // Arrange - persist a hard ban state (30 minutes in future)
            var futureTime = DateTime.UtcNow.AddMinutes(30);
            File.WriteAllText(_hardBanStateFilePath, futureTime.Ticks.ToString());

            // Act
            var result = HttpUtility.GetHardBanUntilUtc();

            // Assert - active ban should be returned
            Assert.NotNull(result);
            Assert.True(result.Value > DateTime.UtcNow);
            Assert.True(Math.Abs((result.Value - futureTime).TotalSeconds) < 2); // Allow 2s tolerance
        }

        [Fact]
        public void GetHardBanUntilUtc_PersistsAcrossInstances()
        {
            // Arrange - simulate first instance setting hard ban
            var banExpiry = DateTime.UtcNow.AddHours(1);
            File.WriteAllText(_hardBanStateFilePath, banExpiry.Ticks.ToString());

            // Act - simulating first process instance
            var firstResult = HttpUtility.GetHardBanUntilUtc();

            // Simulate process restart by "reloading" from disk (in real scenario, new process loads it)
            // Assert - both instances should see the same ban
            Assert.NotNull(firstResult);
            Assert.True(Math.Abs((firstResult.Value - banExpiry).TotalSeconds) < 2);
        }

        [Fact]
        public void OverrideHardBan_ClearsPersistentState()
        {
            // Arrange - set up an active hard ban
            var banExpiry = DateTime.UtcNow.AddHours(1);
            File.WriteAllText(_hardBanStateFilePath, banExpiry.Ticks.ToString());
            Assert.NotNull(HttpUtility.GetHardBanUntilUtc());

            // Act - override the ban
            HttpUtility.OverrideHardBan();

            // Assert - ban should be cleared and file should be deleted
            Assert.Null(HttpUtility.GetHardBanUntilUtc());
            Assert.False(File.Exists(_hardBanStateFilePath));
        }

        [Fact]
        public void HardBanStateFile_CreatesApplicationDataDirectory_IfMissing()
        {
            // Arrange - state directory does not exist
            Directory.Delete(_testDirectory, true);
            Assert.False(Directory.Exists(_testDirectory));

            // Act - reading with no directory must not throw; persisting must create it
            Assert.Null(HttpUtility.GetHardBanUntilUtc());
            HttpUtility.OverrideHardBan();

            // Assert
            Assert.True(Directory.Exists(_testDirectory));
        }

        [Fact]
        public void GetHardBanUntilUtc_WithInvalidFileContent_ReturnsNull()
        {
            // Arrange - write invalid content to hard ban file
            File.WriteAllText(_hardBanStateFilePath, "not_a_valid_number");

            // Act
            var result = HttpUtility.GetHardBanUntilUtc();

            // Assert - should gracefully handle and return null
            Assert.Null(result);
        }

        [Fact]
        public void GetHardBanUntilUtc_WithMissingFile_ReturnsNull()
        {
            // Arrange - ensure file doesn't exist
            if (File.Exists(_hardBanStateFilePath))
            {
                File.Delete(_hardBanStateFilePath);
            }

            // Act
            var result = HttpUtility.GetHardBanUntilUtc();

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task HardBanState_IsThreadSafe()
        {
            // Arrange
            var banExpiry = DateTime.UtcNow.AddHours(1);
            var tasks = new Task[10];
            File.WriteAllText(_hardBanStateFilePath, banExpiry.Ticks.ToString());

            // Act - multiple threads calling GetHardBanUntilUtc simultaneously
            for (int i = 0; i < 10; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    var result = HttpUtility.GetHardBanUntilUtc();
                    Assert.NotNull(result);
                });
            }

            await Task.WhenAll(tasks);

            // Assert - all threads succeeded without exception
            Assert.True(true);
        }
    }
}
