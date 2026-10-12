using VideoForensics.Providers.Ring.Video.Metadata.Tests.Fixtures;

namespace VideoForensics.Providers.Ring.Video.Metadata.Tests
{
    public class MetadataExtractorTests
    {
        private readonly IMetadataExtractor _extractor = null!;

        public MetadataExtractorTests()
        {
            _extractor = new MetadataExtractor();
        }

        #region Basic Extraction Tests

        [Fact]
        public void ExtractMetadata_WithValidEvent_ReturnsMetadata()
        {
            DoorbotHistoryEvent ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithId(12345)
                .WithKind("motion")
                .Build();

            VideoMetadata result = _extractor.ExtractMetadata(ringEvent);

            Assert.NotNull(result);
            Assert.Equal(12345, result.RingEventId);
            Assert.Equal("motion", result.RingEventKind);
        }

        [Fact]
        public void ExtractMetadata_WithNullEvent_ThrowsArgumentNullException()
        {
            try
            {
                _ = _extractor.ExtractMetadata(null!);
                Assert.Fail("Expected ArgumentNullException to be thrown");
            }
            catch (ArgumentNullException)
            {
                // expected
            }
        }

        [Fact]
        public async Task ExtractMetadataAsync_WithValidEvent_ReturnsMetadata()
        {
            DoorbotHistoryEvent ringEvent = DoorbotHistoryEventBuilder.Create().Build();

            VideoMetadata result = await _extractor.ExtractMetadataAsync(ringEvent);

            Assert.NotNull(result);
        }

        #endregion

        #region Device Information Extraction

        [Fact]
        public void ExtractMetadata_ExtractsDeviceName()
        {
            DoorbotHistoryEvent ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithDoorbot(d => d.WithDescription("Front Door Camera"))
                .Build();

            VideoMetadata result = _extractor.ExtractMetadata(ringEvent);

            Assert.Equal("Front Door Camera", result.DeviceName);
        }

        [Fact]
        public void ExtractMetadata_ExtractsDeviceTimezone()
        {
            DoorbotHistoryEvent ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithDoorbot(d => d.WithDescription("Test"))
                .Build();

            VideoMetadata result = _extractor.ExtractMetadata(ringEvent);

            Assert.Equal("America/New_York", result.Timezone);
        }

        [Fact]
        public void ExtractMetadata_ExtractsBatteryPercentage()
        {
            DoorbotHistoryEvent ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithDoorbot(d => d.WithBatteryHealth(75, -50.5))
                .Build();

            VideoMetadata result = _extractor.ExtractMetadata(ringEvent);

            Assert.Equal(75, result.BatteryPercentage);
        }

        [Fact]
        public void ExtractMetadata_ExtractsRssi()
        {
            DoorbotHistoryEvent ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithDoorbot(d => d.WithBatteryHealth(85, -45.5))
                .Build();

            VideoMetadata result = _extractor.ExtractMetadata(ringEvent);

            Assert.Equal(-45.5, result.Rssi);
        }

        #endregion

        #region Location Information Extraction

        [Fact]
        public void ExtractMetadata_ExtractsLatitude()
        {
            DoorbotHistoryEvent ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithDoorbot(d => d.WithLatitude(40.7128))
                .Build();

            VideoMetadata result = _extractor.ExtractMetadata(ringEvent);

            Assert.Equal(40.7128, result.Latitude);
        }

        [Fact]
        public void ExtractMetadata_ExtractsLongitude()
        {
            DoorbotHistoryEvent ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithDoorbot(d => d.WithLongitude(-74.0060))
                .Build();

            VideoMetadata result = _extractor.ExtractMetadata(ringEvent);

            Assert.Equal(-74.0060, result.Longitude);
        }

        [Fact]
        public void ExtractMetadata_ExtractsAddress()
        {
            DoorbotHistoryEvent ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithDoorbot(d => d.WithAddress("123 Main Street, New York, NY 10001"))
                .Build();

            VideoMetadata result = _extractor.ExtractMetadata(ringEvent);

            Assert.Equal("123 Main Street, New York, NY 10001", result.Address);
        }

        #endregion

        #region Computer Vision Properties Extraction

        [Fact]
        public void ExtractMetadata_WithPersonDetected_SetsBothPersonAndMotion()
        {
            DoorbotHistoryEvent ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithCvProperties(cv => cv.WithPersonDetected(true))
                .Build();

            VideoMetadata result = _extractor.ExtractMetadata(ringEvent);

            Assert.True(result.PersonDetected);
            Assert.True(result.MotionDetected);
        }

        [Fact]
        public void ExtractMetadata_WithDetectionType_SetsMotionDetected()
        {
            DoorbotHistoryEvent ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithCvProperties(cv => cv.WithDetectionType("human"))
                .Build();

            VideoMetadata result = _extractor.ExtractMetadata(ringEvent);

            Assert.True(result.MotionDetected);
            Assert.Equal("human", result.DetectionType);
        }

        [Fact]
        public void ExtractMetadata_ExtractsDetectionConfidence()
        {
            DoorbotHistoryEvent ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithCvProperties(cv => cv.WithSimilarity(0.95))
                .Build();

            VideoMetadata result = _extractor.ExtractMetadata(ringEvent);

            Assert.Equal(0.95, result.DetectionConfidence);
        }

        [Fact]
        public void ExtractMetadata_WithoutCvProperties_AssumsMotion()
        {
            DoorbotHistoryEvent ringEvent = DoorbotHistoryEventBuilder.Create()
                .Build();

            VideoMetadata result = _extractor.ExtractMetadata(ringEvent);

            Assert.True(result.MotionDetected);
        }

        #endregion

        #region Event Type Determination

        [Fact]
        public void ExtractMetadata_WithPersonDetected_SetsEventTypeToperson()
        {
            DoorbotHistoryEvent ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithCvProperties(cv => cv.WithPersonDetected(true))
                .Build();

            VideoMetadata result = _extractor.ExtractMetadata(ringEvent);

            Assert.Equal("person", result.EventType);
        }

        [Fact]
        public void ExtractMetadata_WithMotionKind_SetsEventTypeToMotion()
        {
            DoorbotHistoryEvent ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithKind("motion")
                .Build();

            VideoMetadata result = _extractor.ExtractMetadata(ringEvent);

            Assert.Equal("motion", result.EventType);
        }

        [Fact]
        public void ExtractMetadata_WithDoorbellKind_SetsEventTypeToDoorbell()
        {
            DoorbotHistoryEvent ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithKind("doorbell")
                .Build();

            VideoMetadata result = _extractor.ExtractMetadata(ringEvent);

            Assert.Equal("doorbell", result.EventType);
        }

        [Fact]
        public void ExtractMetadata_WithButtonKind_SetsEventTypeToDoorbell()
        {
            DoorbotHistoryEvent ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithKind("button")
                .Build();

            VideoMetadata result = _extractor.ExtractMetadata(ringEvent);

            Assert.Equal("doorbell", result.EventType);
        }

        [Fact]
        public void ExtractMetadata_WithUnknownKind_SetsEventTypeToRing()
        {
            DoorbotHistoryEvent ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithKind("unknown")
                .Build();

            VideoMetadata result = _extractor.ExtractMetadata(ringEvent);

            Assert.Equal("ring", result.EventType);
        }

        #endregion

        #region Keywords Building

        [Fact]
        public void ExtractMetadata_BuildsKeywordsFromEventType()
        {
            DoorbotHistoryEvent ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithKind("motion")
                .Build();

            VideoMetadata result = _extractor.ExtractMetadata(ringEvent);

            Assert.NotNull(result.Keywords);
            Assert.True(result.Keywords.Contains("motion"));
        }

        [Fact]
        public void ExtractMetadata_BuildsKeywordsFromDetectionType()
        {
            DoorbotHistoryEvent ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithCvProperties(cv => cv.WithDetectionType("human"))
                .Build();

            VideoMetadata result = _extractor.ExtractMetadata(ringEvent);

            Assert.NotNull(result.Keywords);
            Assert.True(result.Keywords.Contains("human"));
        }

        [Fact]
        public void ExtractMetadata_IncludesPersonKeywordWhenPersonDetected()
        {
            DoorbotHistoryEvent ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithCvProperties(cv => cv.WithPersonDetected(true))
                .Build();

            VideoMetadata result = _extractor.ExtractMetadata(ringEvent);

            Assert.NotNull(result.Keywords);
            Assert.True(result.Keywords.Contains("person"));
        }

        [Fact]
        public void ExtractMetadata_NormalizeDeviceNameInKeywords()
        {
            DoorbotHistoryEvent ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithDoorbot(d => d.WithDescription("Front Door_Camera"))
                .Build();

            VideoMetadata result = _extractor.ExtractMetadata(ringEvent);

            Assert.NotNull(result.Keywords);
            string? deviceKeyword = result.Keywords.FirstOrDefault(k => k.Contains("door"));
            Assert.NotNull(deviceKeyword);
            Assert.False(deviceKeyword!.Contains("_"), "Device keyword should not contain underscores");
        }

        [Fact]
        public void ExtractMetadata_KeywordsAreDistinct()
        {
            DoorbotHistoryEvent ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithKind("motion")
                .WithCvProperties(cv => cv.WithDetectionType("human").WithPersonDetected(true))
                .Build();

            VideoMetadata result = _extractor.ExtractMetadata(ringEvent);

            Assert.NotNull(result.Keywords);
            Assert.Equal(result.Keywords.Count, result.Keywords.Distinct().Count());
        }

        #endregion

        #region Comment Building

        [Fact]
        public void ExtractMetadata_BuildsComment()
        {
            DoorbotHistoryEvent ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithCreatedAt(new DateTime(2026, 8, 20, 14, 30, 45))
                .WithDoorbot(d => d.WithDescription("Front Door"))
                .Build();

            VideoMetadata result = _extractor.ExtractMetadata(ringEvent);

            Assert.NotNull(result.Comment);
            Assert.Contains("Front Door", result.Comment);
            Assert.Contains("2026", result.Comment);
        }

        [Fact]
        public void ExtractMetadata_CommentIncludesPersonDetection()
        {
            DoorbotHistoryEvent ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithCvProperties(cv => cv.WithPersonDetected(true))
                .Build();

            VideoMetadata result = _extractor.ExtractMetadata(ringEvent);

            Assert.NotNull(result.Comment);
            Assert.Contains("Person detected", result.Comment);
        }

        [Fact]
        public void ExtractMetadata_CommentIncludesBatteryInfo()
        {
            DoorbotHistoryEvent ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithDoorbot(d => d.WithBatteryHealth(75, -50.0))
                .Build();

            VideoMetadata result = _extractor.ExtractMetadata(ringEvent);

            Assert.NotNull(result.Comment);
            Assert.Contains("Battery: 75%", result.Comment);
        }

        [Fact]
        public void ExtractMetadata_CommentIncludesSignalInfo()
        {
            DoorbotHistoryEvent ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithDoorbot(d => d.WithBatteryHealth(85, -45.5))
                .Build();

            VideoMetadata result = _extractor.ExtractMetadata(ringEvent);

            Assert.NotNull(result.Comment);
            Assert.Contains("Signal:", result.Comment);
        }

        #endregion

        #region Event DateTime

        [Fact]
        public void ExtractMetadata_ExtractsEventDateTime()
        {
            var expectedDateTime = new DateTime(2026, 8, 20, 14, 30, 45);
            DoorbotHistoryEvent ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithCreatedAt(expectedDateTime)
                .Build();

            VideoMetadata result = _extractor.ExtractMetadata(ringEvent);

            Assert.Equal(expectedDateTime, result.EventDateTime);
        }

        #endregion

        #region PhotoPrism Compatibility

        [Fact]
        public void ExtractMetadata_BuildsKeywordsForPhotoPrism()
        {
            DoorbotHistoryEvent ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithKind("motion")
                .WithCvProperties(cv => cv
                    .WithPersonDetected(true)
                    .WithDetectionType("human"))
                .Build();

            VideoMetadata result = _extractor.ExtractMetadata(ringEvent);

            Assert.NotNull(result.Keywords);
            Assert.True(result.Keywords.Contains("person"));
            Assert.True(result.Keywords.Contains("motion"));
            Assert.True(result.Keywords.Contains("human"));
        }

        [Fact]
        public void ExtractMetadata_EventTypeIsPhotoPrismCompatible()
        {
            DoorbotHistoryEvent ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithCvProperties(cv => cv.WithPersonDetected(true))
                .Build();

            VideoMetadata result = _extractor.ExtractMetadata(ringEvent);

            string[] validEventTypes = new[] { "motion", "person", "ring", "doorbell" };
            Assert.True(validEventTypes.Contains(result.EventType));
        }

        #endregion

        #region Null/Empty Handling

        [Fact]
        public void ExtractMetadata_WithoutDoorbot_HandlesNullGracefully()
        {
            var ringEvent = new DoorbotHistoryEvent { Id = 1 };

            VideoMetadata result = _extractor.ExtractMetadata(ringEvent);

            Assert.NotNull(result);
            Assert.Null(result.DeviceName);
            Assert.Null(result.Address);
        }

        [Fact]
        public void ExtractMetadata_WithEmptyAddress_DoesNotSetAddress()
        {
            DoorbotHistoryEvent ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithDoorbot(d => d.WithAddress(""))
                .Build();

            VideoMetadata result = _extractor.ExtractMetadata(ringEvent);

            Assert.Null(result.Address);
        }

        #endregion

        #region Complex Scenarios

        [Fact]
        public void ExtractMetadata_WithFullData_ExtractsAllInformation()
        {
            var dateTime = new DateTime(2026, 8, 20, 14, 30, 45);
            DoorbotHistoryEvent ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithId(98765)
                .WithKind("motion")
                .WithCreatedAt(dateTime)
                .WithAnswered(true)
                .WithFavorite(true)
                .WithDoorbot(d => d
                    .WithDescription("Front Door Camera")
                    .WithAddress("123 Main St, Springfield, IL 62701")
                    .WithLatitude(39.7817)
                    .WithLongitude(-89.6501)
                    .WithBatteryHealth(92, -42.5))
                .WithCvProperties(cv => cv
                    .WithPersonDetected(true)
                    .WithDetectionType("human")
                    .WithSimilarity(0.98))
                .Build();

            VideoMetadata result = _extractor.ExtractMetadata(ringEvent);

            Assert.Equal(98765, result.RingEventId);
            Assert.Equal("motion", result.RingEventKind);
            Assert.Equal(dateTime, result.EventDateTime);
            Assert.Equal("Front Door Camera", result.DeviceName);
            Assert.Equal("123 Main St, Springfield, IL 62701", result.Address);
            Assert.Equal(39.7817, result.Latitude);
            Assert.Equal(-89.6501, result.Longitude);
            Assert.Equal(92, result.BatteryPercentage);
            Assert.Equal(-42.5, result.Rssi);
            Assert.True(result.PersonDetected);
            Assert.Equal("human", result.DetectionType);
            Assert.Equal(0.98, result.DetectionConfidence);
            Assert.Equal("person", result.EventType);
            Assert.NotNull(result.Keywords);
            Assert.True(result.Keywords.Count > 0);
        }

        #endregion
    }
}
