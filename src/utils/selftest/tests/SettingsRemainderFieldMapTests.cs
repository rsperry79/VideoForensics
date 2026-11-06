using System.Text.Json;
using System.Text.Json.Nodes;

using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Utils.SelfTest.Tests
{
    /// <summary>
    /// Feeds settings objects through the Ring device mappers and checks the settings JSON remainders against the field map: no path the map
    /// stores in a column or child row may stay in a remainder, and an unknown key must survive. Doorbots and stickup cams are both tested,
    /// because the two mappers read different parts of the same settings object.
    /// </summary>
    public class SettingsRemainderFieldMapTests
    {
        private const string SettingsEndpoint = "device-settings";
        private const string DeviceId = "7001";

        private static string SelfTestFixtureDir => Path.Combine(AppContext.BaseDirectory, "fixtures");

        /// <summary>Settings objects to feed through the mappers: the selftest captures plus the provider's sanitized doorbot settings.</summary>
        private static IEnumerable<(string Name, JsonObject Settings)> SettingsSources()
        {
            foreach (string file in Directory.EnumerateFiles(SelfTestFixtureDir, "device-settings_*.json").OrderBy(f => f, StringComparer.Ordinal))
            {
                yield return (Path.GetFileName(file), JsonNode.Parse(File.ReadAllText(file))!.AsObject());
            }

            string providerFixture = Path.Combine(AppContext.BaseDirectory, "ring-fixtures", "doorbot-settings-sanitized.json");
            yield return ("doorbot-settings-sanitized.json", JsonNode.Parse(File.ReadAllText(providerFixture))!.AsObject());
        }

        /// <summary>Wraps a settings object as one doorbot's settings.</summary>
        private static Devices DoorbotWith(JsonObject settings)
        {
            var doorbot = new JsonObject { ["id"] = long.Parse(DeviceId), ["settings"] = settings.DeepClone() };
            return JsonSerializer.Deserialize<Devices>(new JsonObject { ["doorbots"] = new JsonArray(doorbot) }.ToJsonString())!;
        }

        /// <summary>Wraps a settings object as one stickup cam's settings. A flat motion_detection_enabled is added when absent: the stickup mapper reads it, and some captures send it.</summary>
        private static Devices StickupWith(JsonObject settings)
        {
            JsonObject copy = settings.DeepClone().AsObject();
            copy["motion_detection_enabled"] ??= true;
            var cam = new JsonObject { ["id"] = long.Parse(DeviceId), ["settings"] = copy };
            return JsonSerializer.Deserialize<Devices>(new JsonObject { ["stickup_cams"] = new JsonArray(cam) }.ToJsonString())!;
        }

        /// <summary>Leaf paths of a JSON node in the field-map spelling: object keys joined by dots, array elements as "[]".</summary>
        private static IEnumerable<string> LeafPaths(JsonNode? node, string prefix)
        {
            switch (node)
            {
                case JsonObject obj when obj.Count > 0:
                    foreach (KeyValuePair<string, JsonNode?> child in obj)
                    {
                        foreach (string path in LeafPaths(child.Value, prefix.Length == 0 ? child.Key : $"{prefix}.{child.Key}"))
                        {
                            yield return path;
                        }
                    }

                    break;
                case JsonArray arr when arr.Count > 0:
                    foreach (JsonNode? item in arr)
                    {
                        foreach (string path in LeafPaths(item, $"{prefix}[]"))
                        {
                            yield return path;
                        }
                    }

                    break;
                default:
                    yield return prefix;
                    break;
            }
        }

        /// <summary>Remainder leaf paths (settings-root relative) that the field map stores in a column or child row.</summary>
        private static List<string> MappedPathsIn(string? remainderJson, string prefix, FieldMap map)
        {
            var mapped = new List<string>();
            if (remainderJson == null)
            {
                return mapped;
            }

            var root = JsonNode.Parse(remainderJson);
            foreach (string path in LeafPaths(root, prefix))
            {
                FieldTarget? target = map.Resolve(SettingsEndpoint, path);
                if (target is { Kind: FieldTargetKind.Column or FieldTargetKind.Rows })
                {
                    mapped.Add($"{path} -> {target.Text}");
                }
            }

            return mapped;
        }

        private static IEnumerable<string> RemainderLeaves(string? remainderJson, string prefix)
        {
            return remainderJson == null ? [] : LeafPaths(JsonNode.Parse(remainderJson), prefix);
        }

        [Fact]
        public void SettingsRemainder_DoorbotSettings_HoldsNoMappedColumnOrRowPath()
        {
            var map = FieldMap.LoadEmbedded();
            var violations = new List<string>();
            foreach ((string name, JsonObject settings) in SettingsSources())
            {
                RingDeviceDetails? details = RingDeviceDetailsMapper.Find(DoorbotWith(settings), DeviceId);
                Assert.NotNull(details);
                violations.AddRange(MappedPathsIn(details!.DeviceFields!.SettingsMetadataJson, string.Empty, map).Select(v => $"{name}: {v}"));
            }

            Assert.Empty(violations);
        }

        [Fact]
        public void SettingsRemainder_StickupCamSettings_HoldsNoMappedColumnOrRowPath()
        {
            var map = FieldMap.LoadEmbedded();
            var violations = new List<string>();
            foreach ((string name, JsonObject settings) in SettingsSources())
            {
                RingDeviceDetails? details = RingDeviceDetailsMapper.Find(StickupWith(settings), DeviceId);
                Assert.NotNull(details);
                violations.AddRange(MappedPathsIn(details!.DeviceFields!.SettingsMetadataJson, string.Empty, map).Select(v => $"{name}: {v}"));
            }

            Assert.Empty(violations);
        }

        [Fact]
        public void SettingsRemainder_StickupLightSettings_HoldsNoMappedColumnOrRowPath()
        {
            var map = FieldMap.LoadEmbedded();
            JsonObject settings = new() { ["light_settings"] = new JsonObject { ["brightness"] = 5, ["schedule_enabled"] = true } };

            RingDeviceDetails? details = RingDeviceDetailsMapper.Find(StickupWith(settings), DeviceId);

            Assert.NotNull(details);
            Assert.Empty(MappedPathsIn(details!.DeviceFields!.LightSettingsMetadataJson, "light_settings", map));
        }

        [Fact]
        public void SettingsRemainder_InventedUnknownKeys_SurviveInSettingsAndLightRemainders()
        {
            var settings = (JsonObject)JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ring-fixtures", "doorbot-settings-sanitized.json")))!;
            settings["zz_invented_top_key"] = 1;
            settings["motion_settings"]!.AsObject()["zz_invented_nested_key"] = "x";
            settings["pir_settings"] = new JsonObject { ["zz_invented_pir_key"] = true };
            settings["light_settings"] = new JsonObject { ["brightness"] = 5, ["zz_invented_light_key"] = 2 };

            RingDeviceDetails? stickup = RingDeviceDetailsMapper.Find(StickupWith(settings), DeviceId);
            RingDeviceDetails? doorbot = RingDeviceDetailsMapper.Find(DoorbotWith(settings), DeviceId);

            Assert.NotNull(stickup);
            Assert.NotNull(doorbot);
            var stickupLeaves = RemainderLeaves(stickup!.DeviceFields!.SettingsMetadataJson, string.Empty).ToList();
            var doorbotLeaves = RemainderLeaves(doorbot!.DeviceFields!.SettingsMetadataJson, string.Empty).ToList();
            Assert.Contains("zz_invented_top_key", stickupLeaves);
            Assert.Contains("motion_settings.zz_invented_nested_key", stickupLeaves);
            Assert.Contains("zz_invented_top_key", doorbotLeaves);
            Assert.Contains("motion_settings.zz_invented_nested_key", doorbotLeaves);
            Assert.Contains("pir_settings.zz_invented_pir_key", doorbotLeaves);
            var lightLeaves = RemainderLeaves(stickup.DeviceFields.LightSettingsMetadataJson, string.Empty).ToList();
            Assert.Contains("zz_invented_light_key", lightLeaves);
            Assert.DoesNotContain("brightness", lightLeaves);
        }
    }
}
