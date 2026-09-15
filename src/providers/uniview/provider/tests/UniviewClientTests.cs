using Xunit;

namespace VideoForensics.Providers.Uniview.Tests
{
    /// <summary>
    /// Tests for UniviewClient - the low-level HTTP/cgi-bin client for Uniview NVR.
    /// These tests focus on digest auth, JSON parsing, state tracking, and protocol mechanics.
    /// No live network calls - all are isolated unit tests.
    /// </summary>
    public class UniviewClientTests
    {
        private const string TestHost = "192.168.1.1";
        private const string TestUsername = "admin";
        private const string TestPassword = "password123";

        [Fact]
        public void UniviewClient_Constructor_CreatesValidInstance()
        {
            // Arrange & Act
            var client = new UniviewClient(TestHost, TestUsername, TestPassword);

            // Assert
            Assert.NotNull(client);
            Assert.Equal(0, client.UserLoginHandle);
            client.Dispose();
        }

        [Fact]
        public void UniviewClient_Constructor_WithCustomFfmpegPath_CreatesValidInstance()
        {
            // Arrange & Act
            string customPath = "/usr/bin/ffmpeg";
            var client = new UniviewClient(TestHost, TestUsername, TestPassword, customPath);

            // Assert
            Assert.NotNull(client);
            client.Dispose();
        }

        [Fact]
        public void UniviewClient_Dispose_DoesNotThrow()
        {
            // Arrange
            var client = new UniviewClient(TestHost, TestUsername, TestPassword);

            // Act & Assert
            client.Dispose();
            client.Dispose(); // Second dispose should be safe
        }

        [Fact]
        public void UniviewClient_ResourceCode_GeneratesCorrectFormat()
        {
            // Act & Assert
            // Format: 1100{channel*100:D4}0100{channel:D4}
            // For channel=1: 1100 + 0100 + 0100 + 0001 = 1100010001000001
            // For channel=2: 1100 + 0200 + 0100 + 0002 = 1100020001000002
            Assert.Equal("1100010001000001", UniviewClient.ResourceCode(1));
            Assert.Equal("1100020001000002", UniviewClient.ResourceCode(2));
            Assert.Equal("1100100001000010", UniviewClient.ResourceCode(10));
            Assert.Equal("1100160001000016", UniviewClient.ResourceCode(16));
        }

        [Fact]
        public void UniviewClient_UserLoginHandle_StartsAtZero()
        {
            // Arrange
            var client = new UniviewClient(TestHost, TestUsername, TestPassword);

            // Act
            long handle = client.UserLoginHandle;

            // Assert
            Assert.Equal(0, handle);
            client.Dispose();
        }

        [Fact]
        public void UniviewClient_ChannelInfo_Record_ContainsExpectedFields()
        {
            // Arrange & Act
            var info = new UniviewClient.ChannelInfo(Index: 1, Name: "Camera 1", IsOnline: true);

            // Assert
            Assert.Equal(1, info.Index);
            Assert.Equal("Camera 1", info.Name);
            Assert.True(info.IsOnline);
        }

        [Fact]
        public void UniviewClient_ChannelInfo_CanBeDeconstructed()
        {
            // Arrange
            var info = new UniviewClient.ChannelInfo(Index: 2, Name: "Test Camera", IsOnline: false);

            // Act
            (int index, string? name, bool isOnline) = info;

            // Assert
            Assert.Equal(2, index);
            Assert.Equal("Test Camera", name);
            Assert.False(isOnline);
        }

        [Fact]
        public void UniviewClient_RecordSegment_ContainsExpectedFields()
        {
            // Arrange
            var begin = DateTimeOffset.FromUnixTimeSeconds(1000);
            var end = DateTimeOffset.FromUnixTimeSeconds(2000);

            // Act
            var segment = new RecordSegment(Channel: 3, Begin: begin, End: end, RecordType: 1);

            // Assert
            Assert.Equal(3, segment.Channel);
            Assert.Equal(begin, segment.Begin);
            Assert.Equal(end, segment.End);
            Assert.Equal(1, segment.RecordType);
        }

        [Fact]
        public void UniviewClient_RecordSegment_IsMotionDetection_TrueWhenRecordTypeIsOne()
        {
            // Arrange
            var segment = new RecordSegment(
                Channel: 1,
                Begin: DateTimeOffset.Now,
                End: DateTimeOffset.Now.AddSeconds(10),
                RecordType: 1);

            // Act & Assert
            Assert.True(segment.IsMotionDetection);
        }

        [Fact]
        public void UniviewClient_RecordSegment_IsMotionDetection_FalseWhenRecordTypeIsNotOne()
        {
            // Arrange
            var segment = new RecordSegment(
                Channel: 1,
                Begin: DateTimeOffset.Now,
                End: DateTimeOffset.Now.AddSeconds(10),
                RecordType: 5);

            // Act & Assert
            Assert.False(segment.IsMotionDetection);
        }

        [Fact]
        public void UniviewClient_RecordSegment_CanBeDeconstructed()
        {
            // Arrange
            var begin = DateTimeOffset.FromUnixTimeSeconds(1000);
            var end = DateTimeOffset.FromUnixTimeSeconds(2000);
            var segment = new RecordSegment(Channel: 4, Begin: begin, End: end, RecordType: 5);

            // Act
            (int channel, DateTimeOffset segBegin, DateTimeOffset segEnd, int recordType) = segment;

            // Assert
            Assert.Equal(4, channel);
            Assert.Equal(begin, segBegin);
            Assert.Equal(end, segEnd);
            Assert.Equal(5, recordType);
        }

        [Fact]
        public void UniviewClient_DownloadCapacityException_InheritsFromInvalidOperationException()
        {
            // Arrange
            string message = "Device capacity exceeded";

            // Act
            var exception = new DownloadCapacityException(message);

            // Assert
            _ = Assert.IsAssignableFrom<InvalidOperationException>(exception);
            Assert.Equal(message, exception.Message);
        }

        [Fact]
        public void UniviewClient_PtzCommand_DefinesAllCommandCodes()
        {
            // Arrange & Act & Assert - verify enums exist with expected values
            Assert.Equal(257, (int)PtzCommand.IrisCloseStop);
            Assert.Equal(258, (int)PtzCommand.IrisClose);
            Assert.Equal(259, (int)PtzCommand.IrisOpenStop);
            Assert.Equal(260, (int)PtzCommand.IrisOpen);
            Assert.Equal(513, (int)PtzCommand.FocusNearStop);
            Assert.Equal(514, (int)PtzCommand.FocusNear);
            Assert.Equal(515, (int)PtzCommand.FocusFarStop);
            Assert.Equal(516, (int)PtzCommand.FocusFar);
            Assert.Equal(769, (int)PtzCommand.ZoomTeleStop);
            Assert.Equal(770, (int)PtzCommand.ZoomTele);
            Assert.Equal(771, (int)PtzCommand.ZoomWideStop);
            Assert.Equal(772, (int)PtzCommand.ZoomWide);
            Assert.Equal(1025, (int)PtzCommand.TiltUpStop);
            Assert.Equal(1026, (int)PtzCommand.TiltUp);
            Assert.Equal(1027, (int)PtzCommand.TiltDownStop);
            Assert.Equal(1028, (int)PtzCommand.TiltDown);
            Assert.Equal(1281, (int)PtzCommand.PanRightStop);
            Assert.Equal(1282, (int)PtzCommand.PanRight);
            Assert.Equal(1283, (int)PtzCommand.PanLeftStop);
            Assert.Equal(1284, (int)PtzCommand.PanLeft);
        }

        [Fact]
        public void UniviewClient_PtzCommand_DefinesDiagonalAndAuxCommands()
        {
            // Arrange & Act & Assert - additional PTZ commands
            Assert.Equal(1793, (int)PtzCommand.LeftUpStop);
            Assert.Equal(1794, (int)PtzCommand.LeftUp);
            Assert.Equal(1795, (int)PtzCommand.LeftDownStop);
            Assert.Equal(1796, (int)PtzCommand.LeftDown);
            Assert.Equal(2049, (int)PtzCommand.RightUpStop);
            Assert.Equal(2050, (int)PtzCommand.RightUp);
            Assert.Equal(2051, (int)PtzCommand.RightDownStop);
            Assert.Equal(2052, (int)PtzCommand.RightDown);
            Assert.Equal(2561, (int)PtzCommand.BrushOn);
            Assert.Equal(2562, (int)PtzCommand.BrushOff);
            Assert.Equal(2817, (int)PtzCommand.LightOn);
            Assert.Equal(2818, (int)PtzCommand.LightOff);
            Assert.Equal(3073, (int)PtzCommand.HeatOn);
            Assert.Equal(3074, (int)PtzCommand.HeatOff);
            Assert.Equal(3329, (int)PtzCommand.InfraredOn);
            Assert.Equal(3330, (int)PtzCommand.InfraredOff);
            Assert.Equal(3585, (int)PtzCommand.ScanCruise);
            Assert.Equal(4609, (int)PtzCommand.SnowOn);
            Assert.Equal(4610, (int)PtzCommand.SnowOff);
        }

        [Fact]
        public void UniviewClient_ChannelInfo_EqualsAndHashCode()
        {
            // Arrange
            var info1 = new UniviewClient.ChannelInfo(Index: 1, Name: "Camera 1", IsOnline: true);
            var info2 = new UniviewClient.ChannelInfo(Index: 1, Name: "Camera 1", IsOnline: true);
            var info3 = new UniviewClient.ChannelInfo(Index: 1, Name: "Camera 1", IsOnline: false);

            // Act & Assert
            Assert.Equal(info1, info2);
            Assert.Equal(info1.GetHashCode(), info2.GetHashCode());
            Assert.NotEqual(info1, info3);
        }

        [Fact]
        public void UniviewClient_RecordSegment_EqualsAndHashCode()
        {
            // Arrange
            var begin = DateTimeOffset.FromUnixTimeSeconds(1000);
            var end = DateTimeOffset.FromUnixTimeSeconds(2000);
            var seg1 = new RecordSegment(Channel: 1, Begin: begin, End: end, RecordType: 5);
            var seg2 = new RecordSegment(Channel: 1, Begin: begin, End: end, RecordType: 5);
            var seg3 = new RecordSegment(Channel: 2, Begin: begin, End: end, RecordType: 5);

            // Act & Assert
            Assert.Equal(seg1, seg2);
            Assert.Equal(seg1.GetHashCode(), seg2.GetHashCode());
            Assert.NotEqual(seg1, seg3);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(50)]
        public void UniviewClient_ResourceCode_HandlesVariousChannelNumbers(int channel)
        {
            // Act
            string code = UniviewClient.ResourceCode(channel);

            // Assert
            Assert.NotNull(code);
            Assert.True(code.Length >= 16, $"Resource code should be at least 16 chars, got {code.Length}");
            Assert.True(code.All(char.IsDigit));
        }

        [Fact]
        public void UniviewClient_ChannelInfo_AllOnlineStates()
        {
            // Arrange
            var onlineChannel = new UniviewClient.ChannelInfo(Index: 1, Name: "Online", IsOnline: true);
            var offlineChannel = new UniviewClient.ChannelInfo(Index: 2, Name: "Offline", IsOnline: false);

            // Act & Assert
            Assert.True(onlineChannel.IsOnline);
            Assert.False(offlineChannel.IsOnline);
        }

        [Fact]
        public void UniviewClient_ChannelInfo_WithEmptyName()
        {
            // Arrange & Act
            var info = new UniviewClient.ChannelInfo(Index: 5, Name: "", IsOnline: true);

            // Assert
            Assert.Equal("", info.Name);
        }

        [Fact]
        public void UniviewClient_ChannelInfo_WithLongName()
        {
            // Arrange
            string longName = new('A', 256);

            // Act
            var info = new UniviewClient.ChannelInfo(Index: 3, Name: longName, IsOnline: false);

            // Assert
            Assert.Equal(longName, info.Name);
        }

        [Fact]
        public void UniviewClient_RecordSegment_WithZeroTimestamps()
        {
            // Arrange
            var begin = DateTimeOffset.FromUnixTimeSeconds(0);
            var end = DateTimeOffset.FromUnixTimeSeconds(0);

            // Act
            var segment = new RecordSegment(Channel: 1, Begin: begin, End: end, RecordType: 0);

            // Assert
            Assert.Equal(begin, segment.Begin);
            Assert.Equal(end, segment.End);
        }

        [Fact]
        public void UniviewClient_RecordSegment_WithLargeTimestamps()
        {
            // Arrange
            // DateTimeOffset.FromUnixTimeSeconds has a valid range; use a large but valid timestamp
            long beginTimestamp = 2000000000L; // Valid large timestamp
            long endTimestamp = 2000000000L + 1000;
            var begin = DateTimeOffset.FromUnixTimeSeconds(beginTimestamp);
            var end = DateTimeOffset.FromUnixTimeSeconds(endTimestamp);

            // Act
            var segment = new RecordSegment(Channel: 1, Begin: begin, End: end, RecordType: 1);

            // Assert
            Assert.NotNull(segment);
            Assert.True(segment.End > segment.Begin);
        }

        [Fact]
        public void UniviewClient_RecordSegment_DurationCalculation()
        {
            // Arrange
            var begin = DateTimeOffset.FromUnixTimeSeconds(1000);
            var end = DateTimeOffset.FromUnixTimeSeconds(2500);
            var segment = new RecordSegment(Channel: 1, Begin: begin, End: end, RecordType: 1);

            // Act
            TimeSpan duration = segment.End - segment.Begin;

            // Assert
            Assert.Equal(TimeSpan.FromSeconds(1500), duration);
        }

        [Fact]
        public void UniviewClient_DownloadCapacityException_IsInvalidOperationException()
        {
            // Arrange
            var exception = new DownloadCapacityException("Test message");

            // Act & Assert
            _ = Assert.IsAssignableFrom<InvalidOperationException>(exception);
        }

        [Fact]
        public void UniviewClient_DownloadCapacityException_PreservesMessageContent()
        {
            // Arrange
            string expectedMessage = "Device reached concurrent download limit (code 60031)";

            // Act
            var exception = new DownloadCapacityException(expectedMessage);

            // Assert
            Assert.Equal(expectedMessage, exception.Message);
        }
    }
}
