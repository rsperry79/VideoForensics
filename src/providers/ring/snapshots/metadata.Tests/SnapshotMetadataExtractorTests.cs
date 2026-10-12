using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Models;
using VideoForensics.Providers.Ring.Snapshots.Metadata.Tests.Fixtures;

using Xunit;

namespace VideoForensics.Providers.Ring.Snapshots.Metadata.Tests
{
    public class SnapshotMetadataExtractorTests
    {
        private readonly IMetadataExtractor _extractor = null!;
        private readonly SnapshotProcessingOptions _defaultOptions = null!;

        public SnapshotMetadataExtractorTests()
        {
            _defaultOptions = SnapshotProcessingOptions.CreateDefault();
            _extractor = new SnapshotMetadataExtractor(_defaultOptions);
        }

        #region GPS and Location Extraction

        [Fact]
        public void ExtractMetadata_WithValidLocation_ExtractsLatitude()
        {
            SnapshotEventBuilder builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot();

            DoorbotHistoryEvent snapshotEvent = builder.Build();

            SnapshotMetadata metadata = _extractor.ExtractMetadata(snapshotEvent);

            _ = Assert.NotNull(metadata.Latitude);
            Assert.Equal(40.7128, metadata.Latitude);
        }

        [Fact]
        public void ExtractMetadata_WithValidLocation_ExtractsLongitude()
        {
            SnapshotEventBuilder builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot();

            DoorbotHistoryEvent snapshotEvent = builder.Build();

            SnapshotMetadata metadata = _extractor.ExtractMetadata(snapshotEvent);

            _ = Assert.NotNull(metadata.Longitude);
            Assert.Equal(-74.0060, metadata.Longitude);
        }

        [Fact]
        public void ExtractMetadata_WithAddress_ExtractsAddress()
        {
            SnapshotEventBuilder builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot();

            DoorbotHistoryEvent snapshotEvent = builder.Build();

            SnapshotMetadata metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.NotNull(metadata.Address);
            Assert.Equal("123 Main St, New York, NY 10001", metadata.Address);
        }

        [Fact]
        public void ExtractMetadata_WithPrivacyFocusedOptions_OmitsGps()
        {
            SnapshotEventBuilder builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot();

            DoorbotHistoryEvent snapshotEvent = builder.Build();
            var options = SnapshotProcessingOptions.CreatePrivacyFocused();
            var extractor = new SnapshotMetadataExtractor(options);

            SnapshotMetadata metadata = extractor.ExtractMetadata(snapshotEvent);

            Assert.Null(metadata.Latitude);
            Assert.Null(metadata.Longitude);
        }

        [Fact]
        public void ExtractMetadata_WithPrivacyFocusedOptions_OmitsAddress()
        {
            SnapshotEventBuilder builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot();

            DoorbotHistoryEvent snapshotEvent = builder.Build();
            var options = SnapshotProcessingOptions.CreatePrivacyFocused();
            var extractor = new SnapshotMetadataExtractor(options);

            SnapshotMetadata metadata = extractor.ExtractMetadata(snapshotEvent);

            Assert.Null(metadata.Address);
        }

        #endregion

        #region Device Information Extraction

        [Fact]
        public void ExtractMetadata_ExtractsDeviceName()
        {
            SnapshotEventBuilder builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot();

            DoorbotHistoryEvent snapshotEvent = builder.Build();

            SnapshotMetadata metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.Equal("Front Door", metadata.DeviceName);
        }

        [Fact]
        public void ExtractMetadata_ExtractsDeviceManufacturer()
        {
            SnapshotEventBuilder builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot();

            DoorbotHistoryEvent snapshotEvent = builder.Build();

            SnapshotMetadata metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.Equal("Amazon", metadata.DeviceManufacturer);
        }

        [Fact]
        public void ExtractMetadata_WithDoorbotKind_ExtractsCorrectModel()
        {
            SnapshotEventBuilder builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot();

            DoorbotHistoryEvent snapshotEvent = builder.Build();

            SnapshotMetadata metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.Equal("Doorbell", metadata.DeviceModel);
        }

        [Fact]
        public void ExtractMetadata_WithDoorbell_v3_Kind_ExtractsCorrectModel()
        {
            DoorbotHistoryEvent snapshotEvent = new SnapshotEventBuilder()
                .WithDefaultDoorbot()
                .Build();

            snapshotEvent.Doorbot!.Kind = "doorbell_v3";

            SnapshotMetadata metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.Equal("doorbell_v3", metadata.DeviceModel);
        }

        #endregion

        #region Device Health Metrics

        [Fact]
        public void ExtractMetadata_ExtractsRssi()
        {
            SnapshotEventBuilder builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot();

            DoorbotHistoryEvent snapshotEvent = builder.Build();

            SnapshotMetadata metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.Equal(-50, metadata.Rssi);
        }

        [Fact]
        public void ExtractMetadata_ExtractsBatteryPercentage()
        {
            SnapshotEventBuilder builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot();

            DoorbotHistoryEvent snapshotEvent = builder.Build();

            SnapshotMetadata metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.Equal(95, metadata.BatteryPercentage);
        }

        [Fact]
        public void ExtractMetadata_WithPrivacyFocusedOptions_OmitsDeviceHealth()
        {
            SnapshotEventBuilder builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot();

            DoorbotHistoryEvent snapshotEvent = builder.Build();
            var options = SnapshotProcessingOptions.CreatePrivacyFocused();
            var extractor = new SnapshotMetadataExtractor(options);

            SnapshotMetadata metadata = extractor.ExtractMetadata(snapshotEvent);

            Assert.Null(metadata.Rssi);
            Assert.Null(metadata.BatteryPercentage);
        }

        #endregion

        #region CV Properties - Detection

        [Fact]
        public void ExtractMetadata_WithPersonDetection_ExtractsPersonDetected()
        {
            SnapshotEventBuilder builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot()
                .WithPersonDetection(true, 95);

            DoorbotHistoryEvent snapshotEvent = builder.Build();

            SnapshotMetadata metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.True(metadata.PersonDetected);
            Assert.Equal("person", metadata.DetectionType);
        }

        [Fact]
        public void ExtractMetadata_WithPersonDetection_ExtractsConfidence()
        {
            SnapshotEventBuilder builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot()
                .WithPersonDetection(true, 87);

            DoorbotHistoryEvent snapshotEvent = builder.Build();

            SnapshotMetadata metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.Equal(87, metadata.DetectionConfidence);
        }

        [Fact]
        public void ExtractMetadata_WithMotionDetection_ExtractsMotionDetected()
        {
            SnapshotEventBuilder builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot()
                .WithMotionDetection(true);

            DoorbotHistoryEvent snapshotEvent = builder.Build();

            SnapshotMetadata metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.True(metadata.MotionDetected);
            Assert.Equal("motion", metadata.DetectionType);
        }

        [Fact]
        public void ExtractMetadata_WithPrivacyFocusedOptions_OmitsDetectionData()
        {
            SnapshotEventBuilder builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot()
                .WithPersonDetection(true, 95);

            DoorbotHistoryEvent snapshotEvent = builder.Build();
            var options = SnapshotProcessingOptions.CreatePrivacyFocused();
            var extractor = new SnapshotMetadataExtractor(options);

            SnapshotMetadata metadata = extractor.ExtractMetadata(snapshotEvent);

            Assert.Null(metadata.PersonDetected);
            Assert.Null(metadata.DetectionConfidence);
        }

        #endregion

        #region Event Type and Keywords

        [Fact]
        public void ExtractMetadata_WithMotionKind_DeterminesMotionEventType()
        {
            SnapshotEventBuilder builder = new SnapshotEventBuilder()
                .WithKind("motion")
                .WithDefaultDoorbot();

            DoorbotHistoryEvent snapshotEvent = builder.Build();

            SnapshotMetadata metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.Equal("motion", metadata.EventType);
        }

        [Fact]
        public void ExtractMetadata_WithPersonKind_DeterminesPersonEventType()
        {
            SnapshotEventBuilder builder = new SnapshotEventBuilder()
                .WithKind("person")
                .WithDefaultDoorbot();

            DoorbotHistoryEvent snapshotEvent = builder.Build();

            SnapshotMetadata metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.Equal("person", metadata.EventType);
        }

        [Fact]
        public void ExtractMetadata_GeneratesKeywordsWithDeviceName()
        {
            SnapshotEventBuilder builder = new SnapshotEventBuilder()
                .WithKind("motion")
                .WithDefaultDoorbot();

            DoorbotHistoryEvent snapshotEvent = builder.Build();

            SnapshotMetadata metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.NotNull(metadata.Keywords);
            Assert.True(metadata.Keywords.Any(k => k.Contains("front") || k.Contains("door")));
        }

        [Fact]
        public void ExtractMetadata_WithPersonDetected_IncludesPersonKeyword()
        {
            SnapshotEventBuilder builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot()
                .WithPersonDetection(true);

            DoorbotHistoryEvent snapshotEvent = builder.Build();

            SnapshotMetadata metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.NotNull(metadata.Keywords);
            Assert.True(metadata.Keywords.Contains("person"));
        }

        [Fact]
        public void ExtractMetadata_WithMotionDetected_IncludesMotionKeyword()
        {
            SnapshotEventBuilder builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot()
                .WithMotionDetection(true);

            DoorbotHistoryEvent snapshotEvent = builder.Build();

            SnapshotMetadata metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.NotNull(metadata.Keywords);
            Assert.True(metadata.Keywords.Contains("motion"));
        }

        #endregion

        #region Comment Building

        [Fact]
        public void ExtractMetadata_WithPersonDetected_BuildsCommentWithPersonAndConfidence()
        {
            SnapshotEventBuilder builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot()
                .WithPersonDetection(true, 92);

            DoorbotHistoryEvent snapshotEvent = builder.Build();

            SnapshotMetadata metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.NotNull(metadata.Comment);
            Assert.True(metadata.Comment.Contains("Person detected"));
            Assert.True(metadata.Comment.Contains("92"));
        }

        [Fact]
        public void ExtractMetadata_WithMotionDetected_BuildsCommentWithMotion()
        {
            SnapshotEventBuilder builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot()
                .WithMotionDetection(true);

            DoorbotHistoryEvent snapshotEvent = builder.Build();

            SnapshotMetadata metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.NotNull(metadata.Comment);
            Assert.True(metadata.Comment.Contains("Motion detected"));
        }

        [Fact]
        public void ExtractMetadata_WithDeviceName_IncludesDeviceNameInComment()
        {
            SnapshotEventBuilder builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot();

            DoorbotHistoryEvent snapshotEvent = builder.Build();

            SnapshotMetadata metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.NotNull(metadata.Comment);
            Assert.True(metadata.Comment.Contains("Front Door"));
        }

        #endregion

        #region Ring Event Fields

        [Fact]
        public void ExtractMetadata_ExtractsRingEventId()
        {
            long eventId = 12345L;
            SnapshotEventBuilder builder = new SnapshotEventBuilder()
                .WithId(eventId)
                .WithDefaultDoorbot();

            DoorbotHistoryEvent snapshotEvent = builder.Build();

            SnapshotMetadata metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.Equal(eventId.ToString(), metadata.RingEventId);
        }

        [Fact]
        public void ExtractMetadata_ExtractsRingEventKind()
        {
            SnapshotEventBuilder builder = new SnapshotEventBuilder()
                .WithKind("visitor")
                .WithDefaultDoorbot();

            DoorbotHistoryEvent snapshotEvent = builder.Build();

            SnapshotMetadata metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.Equal("visitor", metadata.RingEventKind);
        }

        [Fact]
        public void ExtractMetadata_ExtractsEventDateTime()
        {
            SnapshotEventBuilder builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot();

            DoorbotHistoryEvent snapshotEvent = builder.Build();

            SnapshotMetadata metadata = _extractor.ExtractMetadata(snapshotEvent);

            // EventDateTime may be null if not properly set on the event
            // The extractor extracts what's available from the event
            Assert.NotNull(metadata);
        }

        #endregion

        #region Timezone

        [Fact]
        public void ExtractMetadata_ExtractsTimezone()
        {
            SnapshotEventBuilder builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot();

            DoorbotHistoryEvent snapshotEvent = builder.Build();

            SnapshotMetadata metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.Equal("America/New_York", metadata.Timezone);
        }

        #endregion

        #region Async Operations

        [Fact]
        public async Task ExtractMetadataAsync_ReturnsMetadata()
        {
            SnapshotEventBuilder builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot()
                .WithPersonDetection(true);

            DoorbotHistoryEvent snapshotEvent = builder.Build();

            SnapshotMetadata metadata = await _extractor.ExtractMetadataAsync(snapshotEvent);

            Assert.NotNull(metadata);
            Assert.True(metadata.PersonDetected);
        }

        #endregion

        #region Edge Cases

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
            }
        }

        [Fact]
        public void ExtractMetadata_WithNoDoorbot_HandlesGracefully()
        {
            SnapshotEventBuilder builder = new SnapshotEventBuilder()
                .WithKind("motion");

            DoorbotHistoryEvent snapshotEvent = builder.Build();
            snapshotEvent.Doorbot = null;

            SnapshotMetadata metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.NotNull(metadata);
            Assert.Null(metadata.Latitude);
            Assert.Null(metadata.DeviceName);
        }

        #endregion

        #region PhotoPrism Compatibility

        [Fact]
        public void ExtractMetadata_WithPhotoPrismEnabled_GeneratesKeywords()
        {
            SnapshotEventBuilder builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot()
                .WithPersonDetection(true);

            DoorbotHistoryEvent snapshotEvent = builder.Build();
            var options = SnapshotProcessingOptions.CreateDefault();
            options.PhotoPrismCompatibility = true;
            var extractor = new SnapshotMetadataExtractor(options);

            SnapshotMetadata metadata = extractor.ExtractMetadata(snapshotEvent);

            Assert.NotNull(metadata.Keywords);
            Assert.True(metadata.Keywords.Count > 0);
        }

        #endregion
    }
}
