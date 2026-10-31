using System.Text.Json;

using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Services;

using Xunit;

using RingDeviceDetails = VideoForensics.Providers.Ring.Services.RingDeviceDetails;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>Notes for values under <c>features</c> that were read but not stored (key and JSON type only, never the value).</summary>
    public class RingDeviceFeatureUnstoredNotesTests
    {
        private static RingDeviceDetails Load(string file, string providerId)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", file);
            Devices devices = JsonSerializer.Deserialize<Devices>(File.ReadAllText(path))!;
            return RingDeviceDetailsMapper.Find(devices, providerId)!;
        }

        private static IReadOnlyList<string> UnknownNotes => Load("device-features-unknown-keys.json", "6001").UnstoredNotes!;

        [Theory]
        [InlineData("1001")]
        [InlineData("2001")]
        [InlineData("3001")]
        public void UnstoredNotes_SanitizedFixture_HasNoFeatureNotes(string id)
        {
            Assert.DoesNotContain(Load("device-features-sanitized.json", id).UnstoredNotes!, n => n.StartsWith("features"));
        }

        [Fact]
        public void UnstoredNotes_UnknownScalarAndArrayKeys_NotedWithTypeOnly()
        {
            Assert.Contains("features.mystery_flag not stored: no column for it (True)", UnknownNotes);
            Assert.Contains("features.mystery_list not stored: no column for it (Array)", UnknownNotes);
        }

        [Fact]
        public void UnstoredNotes_KnownScalarWithWrongShape_Noted()
        {
            Assert.Contains("features.cfes_eligible not stored: expected a boolean (String)", UnknownNotes);
        }

        [Fact]
        public void UnstoredNotes_UnknownKeysInsideFeatureObjects_Noted()
        {
            Assert.Contains("features.mystery_feature.mystery_inner not stored: unknown key (String)", UnknownNotes);
            Assert.Contains("features.mystery_feature.enablement.extra_nested not stored: unknown key (Object)", UnknownNotes);
            Assert.Contains("features.smart_video_search.eligibility.extra_obj not stored: unknown key (Object)", UnknownNotes);
            Assert.Contains("features.smart_video_search.disallow_reasons_extra not stored: unknown key (Array)", UnknownNotes);
            Assert.Contains("features.smart_video_search.recording_mode_availability.other_mode not stored: unknown key (Object)", UnknownNotes);
            Assert.Contains("features.smart_video_search.recording_mode_availability.continuous.extra not stored: unknown key (Number)", UnknownNotes);
        }

        [Fact]
        public void UnstoredNotes_WrongShapeForNestedObject_Noted()
        {
            Assert.Contains("features.smart_video_search.enablement not stored: expected an object (String)", UnknownNotes);
        }

        [Fact]
        public void UnstoredNotes_NonScalarReasonItems_NotedWithPosition()
        {
            Assert.Contains("features.smart_video_search.eligibility.ineligibility_reasons[1] not stored: expected a scalar (Object)", UnknownNotes);
            Assert.Contains("features.smart_video_search.eligibility.ineligibility_reasons[2] not stored: expected a scalar (Array)", UnknownNotes);
            Assert.DoesNotContain(UnknownNotes, n => n.Contains("ineligibility_reasons[3]") || n.Contains("ineligibility_reasons[0]"));
        }

        [Fact]
        public void UnstoredNotes_GuessedTypeColumnsWithValue_NotedWithTypeOnly()
        {
            Assert.Contains("features.smart_video_search.learning has a value (String); its column type is a guess", UnknownNotes);
            Assert.Contains("features.smart_video_search.recording_mode_availability.continuous.available has a value (String); its column type is a guess", UnknownNotes);
            Assert.DoesNotContain(UnknownNotes, n => n.Contains("unusual_alert"));
        }

        [Fact]
        public void UnstoredNotes_AnyFeatureNote_NeverContainsAValue()
        {
            Assert.DoesNotContain(UnknownNotes, n => n.Contains("SECRETVALUE"));
        }

        [Fact]
        public void UnstoredLog_UnknownFeatureKeys_LogsInfoWithDeviceNameAndNoValue()
        {
            var logger = new Mock<ILogger>();
            _ = logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

            RingUnstoredValueLog.Log(logger.Object, "Gate", Load("device-features-unknown-keys.json", "6001"));

            logger.Verify(l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((s, _) => s.ToString()!.Contains("Gate") && s.ToString()!.Contains("features.mystery_flag") && !s.ToString()!.Contains("SECRETVALUE")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }
    }
}
