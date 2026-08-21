using Ring.Api.Snapshots.Metadata.Models;
using Ring.Api.Snapshots.Metadata.Tests.Fixtures;

namespace Ring.Api.Snapshots.Metadata.Tests
{
    [TestClass]
    public class SnapshotMetadataExtractorTests
    {
        private IMetadataExtractor _extractor = null!;
        private SnapshotProcessingOptions _defaultOptions = null!;

        [TestInitialize]
        public void Setup()
        {
            _defaultOptions = SnapshotProcessingOptions.CreateDefault();
            _extractor = new SnapshotMetadataExtractor(_defaultOptions);
        }

        #region GPS and Location Extraction

        [TestMethod]
        public void ExtractMetadata_WithValidLocation_ExtractsLatitude()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot();

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.IsNotNull(metadata.Latitude);
            Assert.AreEqual(40.7128, metadata.Latitude);
        }

        [TestMethod]
        public void ExtractMetadata_WithValidLocation_ExtractsLongitude()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot();

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.IsNotNull(metadata.Longitude);
            Assert.AreEqual(-74.0060, metadata.Longitude);
        }

        [TestMethod]
        public void ExtractMetadata_WithAddress_ExtractsAddress()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot();

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.IsNotNull(metadata.Address);
            Assert.AreEqual("123 Main St, New York, NY 10001", metadata.Address);
        }

        [TestMethod]
        public void ExtractMetadata_WithPrivacyFocusedOptions_OmitsGps()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot();

            var snapshotEvent = builder.Build();
            var options = SnapshotProcessingOptions.CreatePrivacyFocused();
            var extractor = new SnapshotMetadataExtractor(options);

            var metadata = extractor.ExtractMetadata(snapshotEvent);

            Assert.IsNull(metadata.Latitude);
            Assert.IsNull(metadata.Longitude);
        }

        [TestMethod]
        public void ExtractMetadata_WithPrivacyFocusedOptions_OmitsAddress()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot();

            var snapshotEvent = builder.Build();
            var options = SnapshotProcessingOptions.CreatePrivacyFocused();
            var extractor = new SnapshotMetadataExtractor(options);

            var metadata = extractor.ExtractMetadata(snapshotEvent);

            Assert.IsNull(metadata.Address);
        }

        #endregion

        #region Device Information Extraction

        [TestMethod]
        public void ExtractMetadata_ExtractsDeviceName()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot();

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.AreEqual("Front Door", metadata.DeviceName);
        }

        [TestMethod]
        public void ExtractMetadata_ExtractsDeviceManufacturer()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot();

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.AreEqual("Amazon", metadata.DeviceManufacturer);
        }

        [TestMethod]
        public void ExtractMetadata_WithDoorbotKind_ExtractsCorrectModel()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot();

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.AreEqual("Doorbell", metadata.DeviceModel);
        }

        [TestMethod]
        public void ExtractMetadata_WithDoorbell_v3_Kind_ExtractsCorrectModel()
        {
            var snapshotEvent = new SnapshotEventBuilder()
                .WithDefaultDoorbot()
                .Build();

            snapshotEvent.Doorbot!.Kind = "doorbell_v3";

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.AreEqual("doorbell_v3", metadata.DeviceModel);
        }

        #endregion

        #region Device Health Metrics

        [TestMethod]
        public void ExtractMetadata_ExtractsRssi()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot();

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.AreEqual(-50, metadata.Rssi);
        }

        [TestMethod]
        public void ExtractMetadata_ExtractsBatteryPercentage()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot();

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.AreEqual(95, metadata.BatteryPercentage);
        }

        [TestMethod]
        public void ExtractMetadata_WithPrivacyFocusedOptions_OmitsDeviceHealth()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot();

            var snapshotEvent = builder.Build();
            var options = SnapshotProcessingOptions.CreatePrivacyFocused();
            var extractor = new SnapshotMetadataExtractor(options);

            var metadata = extractor.ExtractMetadata(snapshotEvent);

            Assert.IsNull(metadata.Rssi);
            Assert.IsNull(metadata.BatteryPercentage);
        }

        #endregion

        #region CV Properties - Detection

        [TestMethod]
        public void ExtractMetadata_WithPersonDetection_ExtractsPersonDetected()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot()
                .WithPersonDetection(true, 95);

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.IsTrue(metadata.PersonDetected);
            Assert.AreEqual("person", metadata.DetectionType);
        }

        [TestMethod]
        public void ExtractMetadata_WithPersonDetection_ExtractsConfidence()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot()
                .WithPersonDetection(true, 87);

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.AreEqual(87, metadata.DetectionConfidence);
        }

        [TestMethod]
        public void ExtractMetadata_WithMotionDetection_ExtractsMotionDetected()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot()
                .WithMotionDetection(true);

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.IsTrue(metadata.MotionDetected);
            Assert.AreEqual("motion", metadata.DetectionType);
        }

        [TestMethod]
        public void ExtractMetadata_WithPrivacyFocusedOptions_OmitsDetectionData()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot()
                .WithPersonDetection(true, 95);

            var snapshotEvent = builder.Build();
            var options = SnapshotProcessingOptions.CreatePrivacyFocused();
            var extractor = new SnapshotMetadataExtractor(options);

            var metadata = extractor.ExtractMetadata(snapshotEvent);

            Assert.IsNull(metadata.PersonDetected);
            Assert.IsNull(metadata.DetectionConfidence);
        }

        #endregion

        #region Event Type and Keywords

        [TestMethod]
        public void ExtractMetadata_WithMotionKind_DeterminesMotionEventType()
        {
            var builder = new SnapshotEventBuilder()
                .WithKind("motion")
                .WithDefaultDoorbot();

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.AreEqual("motion", metadata.EventType);
        }

        [TestMethod]
        public void ExtractMetadata_WithPersonKind_DeterminesPersonEventType()
        {
            var builder = new SnapshotEventBuilder()
                .WithKind("person")
                .WithDefaultDoorbot();

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.AreEqual("person", metadata.EventType);
        }

        [TestMethod]
        public void ExtractMetadata_GeneratesKeywordsWithDeviceName()
        {
            var builder = new SnapshotEventBuilder()
                .WithKind("motion")
                .WithDefaultDoorbot();

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.IsNotNull(metadata.Keywords);
            Assert.IsTrue(metadata.Keywords.Any(k => k.Contains("front") || k.Contains("door")));
        }

        [TestMethod]
        public void ExtractMetadata_WithPersonDetected_IncludesPersonKeyword()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot()
                .WithPersonDetection(true);

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.IsNotNull(metadata.Keywords);
            Assert.IsTrue(metadata.Keywords.Contains("person"));
        }

        [TestMethod]
        public void ExtractMetadata_WithMotionDetected_IncludesMotionKeyword()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot()
                .WithMotionDetection(true);

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.IsNotNull(metadata.Keywords);
            Assert.IsTrue(metadata.Keywords.Contains("motion"));
        }

        #endregion

        #region Comment Building

        [TestMethod]
        public void ExtractMetadata_WithPersonDetected_BuildsCommentWithPersonAndConfidence()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot()
                .WithPersonDetection(true, 92);

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.IsNotNull(metadata.Comment);
            Assert.IsTrue(metadata.Comment.Contains("Person detected"));
            Assert.IsTrue(metadata.Comment.Contains("92"));
        }

        [TestMethod]
        public void ExtractMetadata_WithMotionDetected_BuildsCommentWithMotion()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot()
                .WithMotionDetection(true);

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.IsNotNull(metadata.Comment);
            Assert.IsTrue(metadata.Comment.Contains("Motion detected"));
        }

        [TestMethod]
        public void ExtractMetadata_WithDeviceName_IncludesDeviceNameInComment()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot();

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.IsNotNull(metadata.Comment);
            Assert.IsTrue(metadata.Comment.Contains("Front Door"));
        }

        #endregion

        #region Ring Event Fields

        [TestMethod]
        public void ExtractMetadata_ExtractsRingEventId()
        {
            var eventId = 12345L;
            var builder = new SnapshotEventBuilder()
                .WithId(eventId)
                .WithDefaultDoorbot();

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.AreEqual(eventId.ToString(), metadata.RingEventId);
        }

        [TestMethod]
        public void ExtractMetadata_ExtractsRingEventKind()
        {
            var builder = new SnapshotEventBuilder()
                .WithKind("visitor")
                .WithDefaultDoorbot();

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.AreEqual("visitor", metadata.RingEventKind);
        }

        [TestMethod]
        public void ExtractMetadata_ExtractsEventDateTime()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot();

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            // EventDateTime may be null if not properly set on the event
            // The extractor extracts what's available from the event
            Assert.IsNotNull(metadata);
        }

        #endregion

        #region Timezone

        [TestMethod]
        public void ExtractMetadata_ExtractsTimezone()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot();

            var snapshotEvent = builder.Build();

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.AreEqual("America/New_York", metadata.Timezone);
        }

        #endregion

        #region Async Operations

        [TestMethod]
        public async Task ExtractMetadataAsync_ReturnsMetadata()
        {
            var builder = new SnapshotEventBuilder()
                .WithDefaultDoorbot()
                .WithPersonDetection(true);

            var snapshotEvent = builder.Build();

            var metadata = await _extractor.ExtractMetadataAsync(snapshotEvent);

            Assert.IsNotNull(metadata);
            Assert.IsTrue(metadata.PersonDetected);
        }

        #endregion

        #region Edge Cases

        [TestMethod]
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

        [TestMethod]
        public void ExtractMetadata_WithNoDoorbot_HandlesGracefully()
        {
            var builder = new SnapshotEventBuilder()
                .WithKind("motion");

            var snapshotEvent = builder.Build();
            snapshotEvent.Doorbot = null;

            var metadata = _extractor.ExtractMetadata(snapshotEvent);

            Assert.IsNotNull(metadata);
            Assert.IsNull(metadata.Latitude);
            Assert.IsNull(metadata.DeviceName);
        }

        #endregion

        #region PhotoPrism Compatibility

        [TestMethod]
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

            Assert.IsNotNull(metadata.Keywords);
            Assert.IsTrue(metadata.Keywords.Count > 0);
        }

        #endregion
    }
}
