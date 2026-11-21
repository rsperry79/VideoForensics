using System.Text.Json;

using Xunit;

namespace VideoForensics.Providers.Ring.Utils.Tests
{
    /// <summary>
    /// Pins the claim behind set-location-mode-settings: a body rebuilt from the typed model drops fields the model
    /// does not carry, while the raw element keeps every field. The round-trip check relies on the raw path.
    /// </summary>
    public class LocationModeSettingsRoundTripTests
    {
        private const string SettingsWithUnmodelledField =
            "{\"home\":[],\"away\":[],\"disarmed\":[],\"options\":{\"defaultCameraOnlyMode\":\"home\"},\"lastUpdateTimeMS\":5,\"fieldNotInModel\":{\"x\":1}}";

        [Fact]
        public void TypedRoundTrip_DropsFieldsTheModelDoesNotCarry()
        {
            string typed = JsonSerializer.Serialize(JsonSerializer.Deserialize<Entities.LocationModeSettingsResponse>(SettingsWithUnmodelledField));

            Assert.DoesNotContain("fieldNotInModel", typed);
        }

        [Fact]
        public void RawRoundTrip_KeepsEveryField()
        {
            JsonElement raw = JsonDocument.Parse(SettingsWithUnmodelledField).RootElement.Clone();

            Assert.Contains("fieldNotInModel", raw.GetRawText());
        }

        [Fact]
        public void Same_IgnoresOnlyTheWriteTimestamp()
        {
            JsonElement before = JsonDocument.Parse("{\"home\":[],\"lastUpdateTimeMS\":5}").RootElement.Clone();
            JsonElement stamped = JsonDocument.Parse("{\"home\":[],\"lastUpdateTimeMS\":9}").RootElement.Clone();
            JsonElement changed = JsonDocument.Parse("{\"home\":[{\"deviceId\":\"1\"}],\"lastUpdateTimeMS\":9}").RootElement.Clone();

            Assert.True(LocationModeSettingsRoundTrip.Same(before, stamped));
            Assert.False(LocationModeSettingsRoundTrip.Same(before, changed));
        }

        [Fact]
        public void Same_TrueForIdenticalDocuments_FalseWhenAFieldIsMissing()
        {
            JsonElement before = JsonDocument.Parse(SettingsWithUnmodelledField).RootElement.Clone();
            JsonElement same = JsonDocument.Parse(SettingsWithUnmodelledField).RootElement.Clone();
            JsonElement missing = JsonDocument.Parse(SettingsWithUnmodelledField.Replace(",\"fieldNotInModel\":{\"x\":1}", string.Empty)).RootElement.Clone();

            Assert.True(LocationModeSettingsRoundTrip.Same(before, same));
            Assert.False(LocationModeSettingsRoundTrip.Same(before, missing));
        }
    }
}
