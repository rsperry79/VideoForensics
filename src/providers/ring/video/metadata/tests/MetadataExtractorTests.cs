using VideoForensics.Providers.Ring.Video.Metadata.Tests.Fixtures;

namespace VideoForensics.Providers.Ring.Video.Metadata.Tests
{
    public class MetadataExtractorTests
    {
        private IMetadataExtractor _extractor = null!;

        public MetadataExtractorTests()
        {
            _extractor = new MetadataExtractor();
        }

        #region Basic Extraction Tests

        [Fact]
        public void ExtractMetadata_WithValidEvent_ReturnsMetadata()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithId(12345)
                .WithKind("motion")
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.NotNull(result);
            Assert.Equal(12345, result.RingEventId);
            Assert.Equal("motion", result.RingEventKind);
        }

        [Fact]
        public void ExtractMetadata_WithNullEvent_ThrowsArgumentNullException()
        {
            try
            {
                _extractor.ExtractMetadata(null!);
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
            var ringEvent = DoorbotHistoryEventBuilder.Create().Build();

            var result = await _extractor.ExtractMetadataAsync(ringEvent);

            Assert.NotNull(result);
        }

        #endregion

        #region Device Information Extraction

        [Fact]
        public void ExtractMetadata_ExtractsDeviceName()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithDoorbot(d => d.WithDescription("Front Door Camera"))
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.Equal("Front Door Camera", result.DeviceName);
        }

        [Fact]
        public void ExtractMetadata_ExtractsDeviceTimezone()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithDoorbot(d => d.WithDescription("Test"))
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.Equal("America/New_York", result.Timezone);
        }

        [Fact]
        public void ExtractMetadata_ExtractsBatteryPercentage()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithDoorbot(d => d.WithBatteryHealth(75, -50.5))
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.Equal(75, result.BatteryPercentage);
        }

        [Fact]
        public void ExtractMetadata_ExtractsRssi()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithDoorbot(d => d.WithBatteryHealth(85, -45.5))
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.Equal(-45.5, result.Rssi);
        }

        #endregion

        #region Location Information Extraction

        [Fact]
        public void ExtractMetadata_ExtractsLatitude()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithDoorbot(d => d.WithLatitude(40.7128))
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.Equal(40.7128, result.Latitude);
        }

        [Fact]
        public void ExtractMetadata_ExtractsLongitude()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithDoorbot(d => d.WithLongitude(-74.0060))
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.Equal(-74.0060, result.Longitude);
        }

        [Fact]
        public void ExtractMetadata_ExtractsAddress()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithDoorbot(d => d.WithAddress("123 Main Street, New York, NY 10001"))
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.Equal("123 Main Street, New York, NY 10001", result.Address);
        }

        #endregion

        #region Computer Vision Properties Extraction

        [Fact]
        public void ExtractMetadata_WithPersonDetected_SetsBothPersonAndMotion()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithCvProperties(cv => cv.WithPersonDetected(true))
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.True(result.PersonDetected);
            Assert.True(result.MotionDetected);
        }

        [Fact]
        public void ExtractMetadata_WithDetectionType_SetsMotionDetected()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithCvProperties(cv => cv.WithDetectionType("human"))
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.True(result.MotionDetected);
            Assert.Equal("human", result.DetectionType);
        }

        [Fact]
        public void ExtractMetadata_ExtractsDetectionConfidence()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithCvProperties(cv => cv.WithSimilarity(0.95))
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.Equal(0.95, result.DetectionConfidence);
        }

        [Fact]
        public void ExtractMetadata_WithoutCvProperties_AssumsMotion()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.True(result.MotionDetected);
        }

        #endregion

        #region Event Type Determination

        [Fact]
        public void ExtractMetadata_WithPersonDetected_SetsEventTypeToperson()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithCvProperties(cv => cv.WithPersonDetected(true))
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.Equal("person", result.EventType);
        }

        [Fact]
        public void ExtractMetadata_WithMotionKind_SetsEventTypeToMotion()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithKind("motion")
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.Equal("motion", result.EventType);
        }

        [Fact]
        public void ExtractMetadata_WithDoorbellKind_SetsEventTypeToDoorbell()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithKind("doorbell")
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.Equal("doorbell", result.EventType);
        }

        [Fact]
        public void ExtractMetadata_WithButtonKind_SetsEventTypeToDoorbell()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithKind("button")
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.Equal("doorbell", result.EventType);
        }

        [Fact]
        public void ExtractMetadata_WithUnknownKind_SetsEventTypeToRing()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithKind("unknown")
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.Equal("ring", result.EventType);
        }

        #endregion

        #region Keywords Building

        [Fact]
        public void ExtractMetadata_BuildsKeywordsFromEventType()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithKind("motion")
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.NotNull(result.Keywords);
            Assert.True(result.Keywords.Contains("motion"));
        }

        [Fact]
        public void ExtractMetadata_BuildsKeywordsFromDetectionType()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithCvProperties(cv => cv.WithDetectionType("human"))
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.NotNull(result.Keywords);
            Assert.True(result.Keywords.Contains("human"));
        }

        [Fact]
        public void ExtractMetadata_IncludesPersonKeywordWhenPersonDetected()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithCvProperties(cv => cv.WithPersonDetected(true))
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.NotNull(result.Keywords);
            Assert.True(result.Keywords.Contains("person"));
        }

        [Fact]
        public void ExtractMetadata_NormalizeDeviceNameInKeywords()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithDoorbot(d => d.WithDescription("Front Door_Camera"))
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.NotNull(result.Keywords);
            var deviceKeyword = result.Keywords.FirstOrDefault(k => k.Contains("door"));
            Assert.NotNull(deviceKeyword);
            Assert.False(deviceKeyword!.Contains("_"), "Device keyword should not contain underscores");
        }

        [Fact]
        public void ExtractMetadata_KeywordsAreDistinct()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithKind("motion")
                .WithCvProperties(cv => cv.WithDetectionType("human").WithPersonDetected(true))
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.NotNull(result.Keywords);
            Assert.Equal(result.Keywords.Count, result.Keywords.Distinct().Count());
        }

        #endregion

        #region Comment Building

        [Fact]
        public void ExtractMetadata_BuildsComment()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithCreatedAt(new DateTime(2026, 8, 20, 14, 30, 45))
                .WithDoorbot(d => d.WithDescription("Front Door"))
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.NotNull(result.Comment);
            Assert.Contains("Front Door", result.Comment);
            Assert.Contains("2026", result.Comment);
        }

        [Fact]
        public void ExtractMetadata_CommentIncludesPersonDetection()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithCvProperties(cv => cv.WithPersonDetected(true))
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.NotNull(result.Comment);
            Assert.Contains("Person detected", result.Comment);
        }

        [Fact]
        public void ExtractMetadata_CommentIncludesBatteryInfo()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithDoorbot(d => d.WithBatteryHealth(75, -50.0))
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.NotNull(result.Comment);
            Assert.Contains("Battery: 75%", result.Comment);
        }

        [Fact]
        public void ExtractMetadata_CommentIncludesSignalInfo()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithDoorbot(d => d.WithBatteryHealth(85, -45.5))
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.NotNull(result.Comment);
            Assert.Contains("Signal:", result.Comment);
        }

        #endregion

        #region Event DateTime

        [Fact]
        public void ExtractMetadata_ExtractsEventDateTime()
        {
            var expectedDateTime = new DateTime(2026, 8, 20, 14, 30, 45);
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithCreatedAt(expectedDateTime)
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.Equal(expectedDateTime, result.EventDateTime);
        }

        #endregion

        #region PhotoPrism Compatibility

        [Fact]
        public void ExtractMetadata_BuildsKeywordsForPhotoPrism()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithKind("motion")
                .WithCvProperties(cv => cv
                    .WithPersonDetected(true)
                    .WithDetectionType("human"))
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.NotNull(result.Keywords);
            Assert.True(result.Keywords.Contains("person"));
            Assert.True(result.Keywords.Contains("motion"));
            Assert.True(result.Keywords.Contains("human"));
        }

        [Fact]
        public void ExtractMetadata_EventTypeIsPhotoPrismCompatible()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithCvProperties(cv => cv.WithPersonDetected(true))
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            var validEventTypes = new[] { "motion", "person", "ring", "doorbell" };
            Assert.True(validEventTypes.Contains(result.EventType));
        }

        #endregion

        #region Null/Empty Handling

        [Fact]
        public void ExtractMetadata_WithoutDoorbot_HandlesNullGracefully()
        {
            var ringEvent = new DoorbotHistoryEvent { Id = 1 };

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.NotNull(result);
            Assert.Null(result.DeviceName);
            Assert.Null(result.Address);
        }

        [Fact]
        public void ExtractMetadata_WithEmptyAddress_DoesNotSetAddress()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithDoorbot(d => d.WithAddress(""))
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.Null(result.Address);
        }

        #endregion

        #region Complex Scenarios

        [Fact]
        public void ExtractMetadata_WithFullData_ExtractsAllInformation()
        {
            var dateTime = new DateTime(2026, 8, 20, 14, 30, 45);
            var ringEvent = DoorbotHistoryEventBuilder.Create()
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

            var result = _extractor.ExtractMetadata(ringEvent);

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
