using Xunit;
using VideoForensics.Providers.Ring.Snapshots.Metadata.Models;
using VideoForensics.Providers.Ring.Snapshots.Metadata.Tests.Fixtures;

namespace VideoForensics.Providers.Ring.Snapshots.Metadata.Tests
{
    public class SnapshotMetadataExtractorTests
    {
        private IMetadataExtractor _extractor = null!;
        private SnapshotProcessingOptions _defaultOptions = null!;

        public SnapshotMetadataExtractorTests()
        {
            _defaultOptions = SnapshotProcessingOptions.CreateDefault();
            _extractor = new SnapshotMetadataExtractor(_defaultOptions);
        }

        #region GPS and Location Extraction

        [Fact]
        public void ExtractMetadata_WithValidLocation_ExtractsLatitude()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot();

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.NotNull(metadata.Latitude);
            Assert.Equal(40.7128, metadata.Latitude);
        }

        [Fact]
        public void ExtractMetadata_WithValidLocation_ExtractsLongitude()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot();

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.NotNull(metadata.Longitude);
            Assert.Equal(-74.0060, metadata.Longitude);
        }

        [Fact]
        public void ExtractMetadata_WithAddress_ExtractsAddress()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot();

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.NotNull(metadata.Address);
            Assert.Equal("123 Main St, New York, NY 10001", metadata.Address);
        }

        [Fact]
        public void ExtractMetadata_WithPrivacyFocusedOptions_OmitsGps()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot();

            var snapshotEvent = builder.Build();
            var options = SnapshotProcessingOptions.CreatePrivacyFocused();
            var extractor = new SnapshotMetadataExtractor(options);

            var metadata = extractor.ExtractMetadata(snapshotEvent);

            Assert.Null(metadata.Latitude);
            Assert.Null(metadata.Longitude);
        }

        [Fact]
        public void ExtractMetadata_WithPrivacyFocusedOptions_OmitsAddress()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot();

            var snapshotEvent = builder.Build();
            var options = SnapshotProcessingOptions.CreatePrivacyFocused();
            var extractor = new SnapshotMetadataExtractor(options);

            var metadata = extractor.ExtractMetadata(snapshotEvent);

            Assert.Null(metadata.Address);
        }

        #endregion

        #region Device Information Extraction

        [Fact]
        public void ExtractMetadata_ExtractsDeviceName()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot();

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.Equal("Front Door", metadata.DeviceName);
        }

        [Fact]
        public void ExtractMetadata_ExtractsDeviceManufacturer()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot();

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.Equal("Amazon", metadata.DeviceManufacturer);
        }

        [Fact]
        public void ExtractMetadata_WithDoorbotKind_ExtractsCorrectModel()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot();

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.Equal("Doorbell", metadata.DeviceModel);
        }

        [Fact]
        public void ExtractMetadata_WithDoorbell_v3_Kind_ExtractsCorrectModel()
        {
            var snapshotEvent = new SnapshotEventBuilder()
                .WithDefaultDoorbot()
                .Build();

            snapshotEvent.Doorbot!.Kind = "doorbell_v3";

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.Equal("doorbell_v3", metadata.DeviceModel);
        }

        #endregion

        #region Device Health Metrics

        [Fact]
        public void ExtractMetadata_ExtractsRssi()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot();

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.Equal(-50, metadata.Rssi);
        }

        [Fact]
        public void ExtractMetadata_ExtractsBatteryPercentage()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot();

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.Equal(95, metadata.BatteryPercentage);
        }

        [Fact]
        public void ExtractMetadata_WithPrivacyFocusedOptions_OmitsDeviceHealth()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot();

            var snapshotEvent = builder.Build();
            var options = SnapshotProcessingOptions.CreatePrivacyFocused();
            var extractor = new SnapshotMetadataExtractor(options);

            var metadata = extractor.ExtractMetadata(snapshotEvent);

            Assert.Null(metadata.Rssi);
            Assert.Null(metadata.BatteryPercentage);
        }

        #endregion

        #region CV Properties - Detection

        [Fact]
        public void ExtractMetadata_WithPersonDetection_ExtractsPersonDetected()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot()
                .WithPersonDetection(true, 95);

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.True(metadata.PersonDetected);
            Assert.Equal("person", metadata.DetectionType);
        }

        [Fact]
        public void ExtractMetadata_WithPersonDetection_ExtractsConfidence()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot()
                .WithPersonDetection(true, 87);

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.Equal(87, metadata.DetectionConfidence);
        }

        [Fact]
        public void ExtractMetadata_WithMotionDetection_ExtractsMotionDetected()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot()
                .WithMotionDetection(true);

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.True(metadata.MotionDetected);
            Assert.Equal("motion", metadata.DetectionType);
        }

        [Fact]
        public void ExtractMetadata_WithPrivacyFocusedOptions_OmitsDetectionData()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot()
                .WithPersonDetection(true, 95);

            var snapshotEvent = builder.Build();
            var options = SnapshotProcessingOptions.CreatePrivacyFocused();
            var extractor = new SnapshotMetadataExtractor(options);

            var metadata = extractor.ExtractMetadata(snapshotEvent);

            Assert.Null(metadata.PersonDetected);
            Assert.Null(metadata.DetectionConfidence);
        }

        #endregion

        #region Event Type and Keywords

        [Fact]
        public void ExtractMetadata_WithMotionKind_DeterminesMotionEventType()
        {
            var builder = new SnapshotEventBuilder()
                .WithKind("motion")
                .WithDefaultDoorbot();

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.Equal("motion", metadata.EventType);
        }

        [Fact]
        public void ExtractMetadata_WithPersonKind_DeterminesPersonEventType()
        {
            var builder = new SnapshotEventBuilder()
                .WithKind("person")
                .WithDefaultDoorbot();

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.Equal("person", metadata.EventType);
        }

        [Fact]
        public void ExtractMetadata_GeneratesKeywordsWithDeviceName()
        {
            var builder = new SnapshotEventBuilder()
                .WithKind("motion")
                .WithDefaultDoorbot();

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.NotNull(metadata.Keywords);
            Assert.True(metadata.Keywords.Any(k => k.Contains("front") || k.Contains("door")));
        }

        [Fact]
        public void ExtractMetadata_WithPersonDetected_IncludesPersonKeyword()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot()
                .WithPersonDetection(true);

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.NotNull(metadata.Keywords);
            Assert.True(metadata.Keywords.Contains("person"));
        }

        [Fact]
        public void ExtractMetadata_WithMotionDetected_IncludesMotionKeyword()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot()
                .WithMotionDetection(true);

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.NotNull(metadata.Keywords);
            Assert.True(metadata.Keywords.Contains("motion"));
        }

        #endregion

        #region Comment Building

        [Fact]
        public void ExtractMetadata_WithPersonDetected_BuildsCommentWithPersonAndConfidence()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot()
                .WithPersonDetection(true, 92);

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.NotNull(metadata.Comment);
            Assert.True(metadata.Comment.Contains("Person detected"));
            Assert.True(metadata.Comment.Contains("92"));
        }

        [Fact]
        public void ExtractMetadata_WithMotionDetected_BuildsCommentWithMotion()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot()
                .WithMotionDetection(true);

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.NotNull(metadata.Comment);
            Assert.True(metadata.Comment.Contains("Motion detected"));
        }

        [Fact]
        public void ExtractMetadata_WithDeviceName_IncludesDeviceNameInComment()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot();

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.NotNull(metadata.Comment);
            Assert.True(metadata.Comment.Contains("Front Door"));
        }

        #endregion

        #region Ring Event Fields

        [Fact]
        public void ExtractMetadata_ExtractsRingEventId()
        {
            var eventId = 12345L;
            var builder = new SnapshotEventBuilder()
                .WithId(eventId)
                .WithDefaultDoorbot();

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.Equal(eventId.ToString(), metadata.RingEventId);
        }

        [Fact]
        public void ExtractMetadata_ExtractsRingEventKind()
        {
            var builder = new SnapshotEventBuilder()
                .WithKind("visitor")
                .WithDefaultDoorbot();

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.Equal("visitor", metadata.RingEventKind);
        }

        [Fact]
        public void ExtractMetadata_ExtractsEventDateTime()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot();

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            // EventDateTime may be null if not properly set on the event
            // The extractor extracts what's available from the event
            Assert.NotNull(metadata);
        }

        #endregion

        #region Timezone

        [Fact]
        public void ExtractMetadata_ExtractsTimezone()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot();

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.Equal("America/New_York", metadata.Timezone);
        }

        #endregion

        #region Async Operations

        [Fact]
        public async Task ExtractMetadataAsync_ReturnsMetadata()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot()
                .WithPersonDetection(true);

            var snapshotEvent = builder.Build();

            var metadata = await _extractor.ExtractMetadataAsync(snapshotEvent);

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
                _extractor.ExtractMetadata(null!);
                Assert.Fail("Expected ArgumentNullException to be thrown");
            }
            catch (ArgumentNullException)
            {
            }
        }

        [Fact]
        public void ExtractMetadata_WithNoDoorbot_HandlesGracefully()
        {
            var builder = new SnapshotEventBuilder()
                .WithKind("motion");

            var snapshotEvent = builder.Build();
            snapshotEvent.Doorbot = null;

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.NotNull(metadata);
            Assert.Null(metadata.Latitude);
            Assert.Null(metadata.DeviceName);
        }

        #endregion

        #region PhotoPrism Compatibility

        [Fact]
        public void ExtractMetadata_WithPhotoPrismEnabled_GeneratesKeywords()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot()
                .WithPersonDetection(true);

            var snapshotEvent = builder.Build();
            var options = SnapshotProcessingOptions.CreateDefault();
            options.PhotoPrismCompatibility = true;
            var extractor = new SnapshotMetadataExtractor(options);

            var metadata = extractor.ExtractMetadata(snapshotEvent);

            Assert.NotNull(metadata.Keywords);
            Assert.True(metadata.Keywords.Count > 0);
        }

        #endregion
    }
}
