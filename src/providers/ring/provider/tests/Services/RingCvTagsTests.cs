using System.Text.Json;

using VideoForensics.Providers.Ring.Entities;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests.Services
{
    /// <summary>
    /// Ring sends cv.tags as objects with a "tag" property, not as plain strings. These tests pin that shape
    /// for both the location history and the event CV payloads.
    /// </summary>
    public class RingCvTagsTests
    {
        [Fact]
        public void LocationHistoryCv_TagsAsObjects_ReadTagValues()
        {
            const string json = """{"tags": [{"tag": "person"}, {"tag": "motion"}]}""";

            LocationHistoryCv? cv = JsonSerializer.Deserialize<LocationHistoryCv>(json);

            Assert.NotNull(cv);
            Assert.Equal(new[] { "person", "motion" }, cv!.Tags);
        }

        [Fact]
        public void CvProperties_TagsAsObjects_ReadTagValues()
        {
            const string json = """{"tags": [{"tag": "person"}]}""";

            CvProperties? cv = JsonSerializer.Deserialize<CvProperties>(json);

            Assert.NotNull(cv);
            Assert.Equal(new[] { "person" }, cv!.Tags);
        }

        [Fact]
        public void CvProperties_TagsAsStrings_StillRead()
        {
            const string json = """{"tags": ["person"]}""";

            CvProperties? cv = JsonSerializer.Deserialize<CvProperties>(json);

            Assert.NotNull(cv);
            Assert.Equal(new[] { "person" }, cv!.Tags);
        }

        [Fact]
        public void CvProperties_TagsNull_ReadsNull()
        {
            const string json = """{"tags": null}""";

            CvProperties? cv = JsonSerializer.Deserialize<CvProperties>(json);

            Assert.NotNull(cv);
            Assert.Null(cv!.Tags);
        }
    }
}
