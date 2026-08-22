using VideoForensics.Providers.Ring.Video.Metadata.Tests.Fixtures;

namespace VideoForensics.Providers.Ring.Video.Metadata.Tests
{
    [TestClass]
    public class MetadataExtractorTests
    {
        private IMetadataExtractor _extractor = null!;

        [TestInitialize]
        public void Setup()
        {
            _extractor = new MetadataExtractor();
        }

        #region Basic Extraction Tests

        [TestMethod]
        public void ExtractMetadata_WithValidEvent_ReturnsMetadata()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithId(12345)
                .WithKind("motion")
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.IsNotNull(result);
            Assert.AreEqual(12345, result.RingEventId);
            Assert.AreEqual("motion", result.RingEventKind);
        }

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
                // expected
            }
        }

        [TestMethod]
        public async Task ExtractMetadataAsync_WithValidEvent_ReturnsMetadata()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create().Build();

            var result = await _extractor.ExtractMetadataAsync(ringEvent);

            Assert.IsNotNull(result);
        }

        #endregion

        #region Device Information Extraction

        [TestMethod]
        public void ExtractMetadata_ExtractsDeviceName()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithDoorbot(d => d.WithDescription("Front Door Camera"))
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.AreEqual("Front Door Camera", result.DeviceName);
        }

        [TestMethod]
        public void ExtractMetadata_ExtractsDeviceTimezone()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithDoorbot(d => d.WithDescription("Test"))
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.AreEqual("America/New_York", result.Timezone);
        }

        [TestMethod]
        public void ExtractMetadata_ExtractsBatteryPercentage()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithDoorbot(d => d.WithBatteryHealth(75, -50.5))
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.AreEqual(75, result.BatteryPercentage);
        }

        [TestMethod]
        public void ExtractMetadata_ExtractsRssi()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithDoorbot(d => d.WithBatteryHealth(85, -45.5))
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.AreEqual(-45.5, result.Rssi);
        }

        #endregion

        #region Location Information Extraction

        [TestMethod]
        public void ExtractMetadata_ExtractsLatitude()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithDoorbot(d => d.WithLatitude(40.7128))
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.AreEqual(40.7128, result.Latitude);
        }

        [TestMethod]
        public void ExtractMetadata_ExtractsLongitude()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithDoorbot(d => d.WithLongitude(-74.0060))
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.AreEqual(-74.0060, result.Longitude);
        }

        [TestMethod]
        public void ExtractMetadata_ExtractsAddress()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithDoorbot(d => d.WithAddress("123 Main Street, New York, NY 10001"))
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.AreEqual("123 Main Street, New York, NY 10001", result.Address);
        }

        #endregion

        #region Computer Vision Properties Extraction

        [TestMethod]
        public void ExtractMetadata_WithPersonDetected_SetsBothPersonAndMotion()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithCvProperties(cv => cv.WithPersonDetected(true))
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.IsTrue(result.PersonDetected);
            Assert.IsTrue(result.MotionDetected);
        }

        [TestMethod]
        public void ExtractMetadata_WithDetectionType_SetsMotionDetected()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithCvProperties(cv => cv.WithDetectionType("human"))
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.IsTrue(result.MotionDetected);
            Assert.AreEqual("human", result.DetectionType);
        }

        [TestMethod]
        public void ExtractMetadata_ExtractsDetectionConfidence()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithCvProperties(cv => cv.WithSimilarity(0.95))
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.AreEqual(0.95, result.DetectionConfidence);
        }

        [TestMethod]
        public void ExtractMetadata_WithoutCvProperties_AssumsMotion()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.IsTrue(result.MotionDetected);
        }

        #endregion

        #region Event Type Determination

        [TestMethod]
        public void ExtractMetadata_WithPersonDetected_SetsEventTypeToperson()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithCvProperties(cv => cv.WithPersonDetected(true))
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.AreEqual("person", result.EventType);
        }

        [TestMethod]
        public void ExtractMetadata_WithMotionKind_SetsEventTypeToMotion()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithKind("motion")
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.AreEqual("motion", result.EventType);
        }

        [TestMethod]
        public void ExtractMetadata_WithDoorbellKind_SetsEventTypeToDoorbell()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithKind("doorbell")
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.AreEqual("doorbell", result.EventType);
        }

        [TestMethod]
        public void ExtractMetadata_WithButtonKind_SetsEventTypeToDoorbell()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithKind("button")
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.AreEqual("doorbell", result.EventType);
        }

        [TestMethod]
        public void ExtractMetadata_WithUnknownKind_SetsEventTypeToRing()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithKind("unknown")
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.AreEqual("ring", result.EventType);
        }

        #endregion

        #region Keywords Building

        [TestMethod]
        public void ExtractMetadata_BuildsKeywordsFromEventType()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithKind("motion")
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.IsNotNull(result.Keywords);
            Assert.IsTrue(result.Keywords.Contains("motion"));
        }

        [TestMethod]
        public void ExtractMetadata_BuildsKeywordsFromDetectionType()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithCvProperties(cv => cv.WithDetectionType("human"))
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.IsNotNull(result.Keywords);
            Assert.IsTrue(result.Keywords.Contains("human"));
        }

        [TestMethod]
        public void ExtractMetadata_IncludesPersonKeywordWhenPersonDetected()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithCvProperties(cv => cv.WithPersonDetected(true))
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.IsNotNull(result.Keywords);
            Assert.IsTrue(result.Keywords.Contains("person"));
        }

        [TestMethod]
        public void ExtractMetadata_NormalizeDeviceNameInKeywords()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithDoorbot(d => d.WithDescription("Front Door_Camera"))
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.IsNotNull(result.Keywords);
            var deviceKeyword = result.Keywords.FirstOrDefault(k => k.Contains("door"));
            Assert.IsNotNull(deviceKeyword);
            Assert.IsFalse(deviceKeyword!.Contains("_"), "Device keyword should not contain underscores");
        }

        [TestMethod]
        public void ExtractMetadata_KeywordsAreDistinct()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithKind("motion")
                .WithCvProperties(cv => cv.WithDetectionType("human").WithPersonDetected(true))
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.IsNotNull(result.Keywords);
            Assert.AreEqual(result.Keywords.Count, result.Keywords.Distinct().Count());
        }

        #endregion

        #region Comment Building

        [TestMethod]
        public void ExtractMetadata_BuildsComment()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithCreatedAt(new DateTime(2026, 8, 20, 14, 30, 45))
                .WithDoorbot(d => d.WithDescription("Front Door"))
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.IsNotNull(result.Comment);
            StringAssert.Contains(result.Comment, "Front Door");
            StringAssert.Contains(result.Comment, "2026");
        }

        [TestMethod]
        public void ExtractMetadata_CommentIncludesPersonDetection()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithCvProperties(cv => cv.WithPersonDetected(true))
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.IsNotNull(result.Comment);
            StringAssert.Contains(result.Comment, "Person detected");
        }

        [TestMethod]
        public void ExtractMetadata_CommentIncludesBatteryInfo()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithDoorbot(d => d.WithBatteryHealth(75, -50.0))
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.IsNotNull(result.Comment);
            StringAssert.Contains(result.Comment, "Battery: 75%");
        }

        [TestMethod]
        public void ExtractMetadata_CommentIncludesSignalInfo()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithDoorbot(d => d.WithBatteryHealth(85, -45.5))
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.IsNotNull(result.Comment);
            StringAssert.Contains(result.Comment, "Signal:");
        }

        #endregion

        #region Event DateTime

        [TestMethod]
        public void ExtractMetadata_ExtractsEventDateTime()
        {
            var expectedDateTime = new DateTime(2026, 8, 20, 14, 30, 45);
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithCreatedAt(expectedDateTime)
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.AreEqual(expectedDateTime, result.EventDateTime);
        }

        #endregion

        #region PhotoPrism Compatibility

        [TestMethod]
        public void ExtractMetadata_BuildsKeywordsForPhotoPrism()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithKind("motion")
                .WithCvProperties(cv => cv
                    .WithPersonDetected(true)
                    .WithDetectionType("human"))
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.IsNotNull(result.Keywords);
            Assert.IsTrue(result.Keywords.Contains("person"));
            Assert.IsTrue(result.Keywords.Contains("motion"));
            Assert.IsTrue(result.Keywords.Contains("human"));
        }

        [TestMethod]
        public void ExtractMetadata_EventTypeIsPhotoPrismCompatible()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithCvProperties(cv => cv.WithPersonDetected(true))
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            var validEventTypes = new[] { "motion", "person", "ring", "doorbell" };
            Assert.IsTrue(validEventTypes.Contains(result.EventType));
        }

        #endregion

        #region Null/Empty Handling

        [TestMethod]
        public void ExtractMetadata_WithoutDoorbot_HandlesNullGracefully()
        {
            var ringEvent = new DoorbotHistoryEvent { Id = 1 };

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.IsNotNull(result);
            Assert.IsNull(result.DeviceName);
            Assert.IsNull(result.Address);
        }

        [TestMethod]
        public void ExtractMetadata_WithEmptyAddress_DoesNotSetAddress()
        {
            var ringEvent = DoorbotHistoryEventBuilder.Create()
                .WithDoorbot(d => d.WithAddress(""))
                .Build();

            var result = _extractor.ExtractMetadata(ringEvent);

            Assert.IsNull(result.Address);
        }

        #endregion

        #region Complex Scenarios

        [TestMethod]
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

            Assert.AreEqual(98765, result.RingEventId);
            Assert.AreEqual("motion", result.RingEventKind);
            Assert.AreEqual(dateTime, result.EventDateTime);
            Assert.AreEqual("Front Door Camera", result.DeviceName);
            Assert.AreEqual("123 Main St, Springfield, IL 62701", result.Address);
            Assert.AreEqual(39.7817, result.Latitude);
            Assert.AreEqual(-89.6501, result.Longitude);
            Assert.AreEqual(92, result.BatteryPercentage);
            Assert.AreEqual(-42.5, result.Rssi);
            Assert.IsTrue(result.PersonDetected);
            Assert.AreEqual("human", result.DetectionType);
            Assert.AreEqual(0.98, result.DetectionConfidence);
            Assert.AreEqual("person", result.EventType);
            Assert.IsNotNull(result.Keywords);
            Assert.IsTrue(result.Keywords.Count > 0);
        }

        #endregion
    }
}
