using System.Text.Json;

using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>
    /// A wrong JSON type for one of the typed settings paths that are removed from the settings remainder is noted (key and JSON type only, never
    /// the value), the same way the other typed paths are noted.
    /// </summary>
    public class RingDeviceWrongTypedSettingNotesTests
    {
        private const string DoorbotWrongTyped = """
            {"id": 6001, "description": "Fake Door",
             "settings": {"pir_settings": {"sensitivity_1": "SECRET-PIR-1", "zone_mask": true},
                          "volume_settings": {"doorbell_volume": "SECRET-VOLUME"},
                          "motion_settings": {"motion_detection_enabled": "SECRET-MOTION"}}}
            """;

        private const string StickupWrongTyped = """
            {"id": 7001, "description": "Fake Cam",
             "settings": {"chime_settings": {"type": "SECRET-CHIME", "enable": 1, "duration": "SECRET-DURATION"},
                          "motion_settings": {"motion_detection_enabled": 1},
                          "motion_detection_enabled": "SECRET-FLAT"}}
            """;

        private static List<string> NotesFor(Doorbot doorbot)
        {
            var notes = new List<string>();
            _ = RingDeviceDetailsMapper.ToDeviceFields(doorbot, notes);
            return notes;
        }

        private static List<string> NotesFor(StickupCam cam)
        {
            var notes = new List<string>();
            _ = RingDeviceDetailsMapper.ToDeviceFields(cam, notes);
            return notes;
        }

        [Fact]
        public void ToDeviceFields_DoorbotWrongTypedPathsReadAsNestedValues_NoteKeyAndType()
        {
            List<string> notes = NotesFor(JsonSerializer.Deserialize<Doorbot>(DoorbotWrongTyped)!);

            Assert.Contains("pir_settings.sensitivity_1 not stored: unexpected shape (String)", notes);
            Assert.Contains("pir_settings.zone_mask not stored: unexpected shape (True)", notes);
            Assert.Contains("volume_settings.doorbell_volume not stored: unexpected shape (String)", notes);
            Assert.Contains("motion_settings.motion_detection_enabled not stored: unexpected shape (String)", notes);
        }

        [Fact]
        public void ToDeviceFields_StickupWrongTypedPaths_NoteKeyAndType()
        {
            List<string> notes = NotesFor(JsonSerializer.Deserialize<StickupCam>(StickupWrongTyped)!);

            Assert.Contains("chime_settings.type not stored: unexpected shape (String)", notes);
            Assert.Contains("chime_settings.enable not stored: unexpected shape (Number)", notes);
            Assert.Contains("chime_settings.duration not stored: unexpected shape (String)", notes);
            Assert.Contains("motion_settings.motion_detection_enabled not stored: unexpected shape (Number)", notes);
            Assert.Contains("motion_detection_enabled not stored: unexpected shape (String)", notes);
        }

        [Fact]
        public void ToDeviceFields_WrongTypedPaths_NoteNeverContainsTheValue()
        {
            List<string> notes = NotesFor(JsonSerializer.Deserialize<Doorbot>(DoorbotWrongTyped)!);
            notes.AddRange(NotesFor(JsonSerializer.Deserialize<StickupCam>(StickupWrongTyped)!));

            Assert.NotEmpty(notes);
            Assert.DoesNotContain(notes, n => n.Contains("SECRET", StringComparison.Ordinal));
        }

        [Fact]
        public void ToDeviceFields_GroupThatIsNotAnObject_NotedOnceWithTypeOnly()
        {
            List<string> notes = NotesFor(JsonSerializer.Deserialize<Doorbot>("""{"id": 6002, "settings": {"pir_settings": "SECRET-GROUP"}}""")!);

            Assert.Single(notes, n => n.StartsWith("pir_settings", StringComparison.Ordinal));
            Assert.Contains("pir_settings not stored: expected an object (String)", notes);
            Assert.DoesNotContain(notes, n => n.Contains("SECRET", StringComparison.Ordinal));
        }

        [Fact]
        public void ToDeviceFields_TypedNullAndRightType_NoNote()
        {
            List<string> notes = NotesFor(JsonSerializer.Deserialize<Doorbot>("""
                {"id": 6003, "settings": {"pir_settings": {"sensitivity_1": null, "sensitivity_2": 4},
                 "volume_settings": {"doorbell_volume": 7}, "motion_settings": {"motion_detection_enabled": false}}}
                """)!);

            Assert.DoesNotContain(notes, n => n.Contains("pir_settings", StringComparison.Ordinal) || n.Contains("volume_settings", StringComparison.Ordinal) || n.Contains("motion_detection", StringComparison.Ordinal));
        }
    }
}
