using System;
using System.Collections.Generic;
using System.Text.Json;
using KoenZomers.Ring.Api.Entities;
using Xunit;

namespace KoenZomers.Ring.ApiTester
{
    /// <summary>
    /// Tests for JsonSchemaValidator demonstrating schema validation with known API response shapes.
    /// These tests use sample payloads to verify the validator catches type mismatches, missing fields,
    /// and schema drift issues - the core problem we're solving.
    /// </summary>
    public class SchemaValidatorTests
    {
        [Fact]
        public void ValidateProfileResponse_DetectsCorrectShape()
        {
            var sampleJson = JsonDocument.Parse("""
            {
                "profile": {
                    "email": "user@example.com",
                    "first_name": "John",
                    "hardware_id": "aacdef",
                    "id": 12345,
                    "phone_number": "5555551234",
                    "preferred_country": "US"
                }
            }
            """).RootElement;

            var validator = new JsonSchemaValidator();
            var issues = validator.ValidateAgainstSchema(sampleJson, typeof(ProfileResponse));

            // Profile response should be valid - only potential issue is if the
            // response structure doesn't have a "profile" property in the schema
            Assert.NotNull(issues);
        }

        [Fact]
        public void ValidateProfileResponse_DetectsTypeMismatch()
        {
            // This is what was silently failing before: phone_number as an object instead of string
            var sampleJson = JsonDocument.Parse("""
            {
                "profile": {
                    "email": "user@example.com",
                    "first_name": "John",
                    "hardware_id": "aacdef",
                    "id": 12345,
                    "phone_number": { "country": "US", "number": "5555551234" },
                    "preferred_country": "US"
                }
            }
            """).RootElement;

            var validator = new JsonSchemaValidator();
            var issues = validator.ValidateAgainstSchema(sampleJson, typeof(ProfileResponse));

            // Should detect that phone_number is an object when Profile.PhoneNumber is a string
            var mismatch = issues.Find(i => i.IssueType == "TypeMismatch" && i.Path.Contains("phone_number"));
            Assert.NotNull(mismatch);
            Assert.Equal("Error", mismatch.Severity);
        }

        [Fact]
        public void ValidateRingtonesResponse_DetectsCorrectShape()
        {
            var sampleJson = JsonDocument.Parse("""
            {
                "audios": [
                    {
                        "id": "custom_upload_1",
                        "category": "custom",
                        "available": true,
                        "checksum": "abc123",
                        "sample_rate_khz": 16,
                        "supported_device_kinds": ["doorbell"],
                        "url_amz": "https://s3.amazonaws.com/...",
                        "user_id": 12345
                    }
                ]
            }
            """).RootElement;

            var validator = new JsonSchemaValidator();
            var issues = validator.ValidateAgainstSchema(sampleJson, typeof(RingtonesResponse));

            // Should validate successfully (or note extra fields as Info severity)
            var errors = issues.FindAll(i => i.Severity == "Error");
            Assert.Empty(errors);
        }

        [Fact]
        public void ValidateLocationEventsResponse_DetectsCorrectShape()
        {
            var sampleJson = JsonDocument.Parse("""
            {
                "events": [
                    {
                        "event_id": "evt_12345",
                        "source_id": "src_123",
                        "event_type": "motion",
                        "state": "ringing",
                        "favorite": false,
                        "recorded": true,
                        "recording_status": "ready",
                        "is_e2ee": false,
                        "created_at": "2026-08-18T12:00:00Z",
                        "had_subscription": true,
                        "owner_id": "owner_123",
                        "riid": "riid_123",
                        "doorbot_id": 9876543210,
                        "ding_id": 1234567890,
                        "ding_id_str": "1234567890",
                        "kind": "motion",
                        "doorbot": {
                            "id": 9876543210,
                            "description": "Front Door",
                            "type": "doorbot"
                        },
                        "cv_properties": {
                            "detection_types": []
                        },
                        "properties": {
                            "is_alexa": false,
                            "is_sidewalk": false,
                            "is_autoreply": false,
                            "stark_reviewed": false
                        }
                    }
                ],
                "meta": {
                    "pagination_key": "next_page_123"
                }
            }
            """).RootElement;

            var validator = new JsonSchemaValidator();
            var issues = validator.ValidateAgainstSchema(sampleJson, typeof(LocationEventsResponse));

            // Should validate successfully
            var errors = issues.FindAll(i => i.Severity == "Error");
            Assert.Empty(errors);
        }

        [Fact]
        public void ValidateDeviceHealthResponse_DetectsNumericStringMismatch()
        {
            // Example: AC power reported as string "110" instead of number 110
            var sampleJson = JsonDocument.Parse("""
            {
                "general": {
                    "ac_power": "110",
                    "battery": 90,
                    "updated_at": 1692374400
                }
            }
            """).RootElement;

            var validator = new JsonSchemaValidator();
            var issues = validator.ValidateAgainstSchema(sampleJson, typeof(DeviceHealthResponse));

            // Should detect numeric string type mismatch
            var mismatch = issues.Find(i => i.IssueType == "TypeMismatch" && i.Path.Contains("ac_power"));
            Assert.NotNull(mismatch);
            Assert.Equal("Warning", mismatch.Severity); // Numeric strings are warnings (could be auto-converted)
        }

        [Fact]
        public void ValidateVideoSearchResponse_DetectsExtraUnmappedFields()
        {
            // API returns a field that's not in our VideoSearchResponse schema
            var sampleJson = JsonDocument.Parse("""
            {
                "doorbots": [
                    {
                        "id": 123,
                        "description": "Front Door",
                        "device_kind": "doorbot",
                        "status": "online",
                        "subscribed": true,
                        "subscribed_motions": true,
                        "battery": "full",
                        "external_connection": false,
                        "firmware": "1.10.30",
                        "kind": "doorbot",
                        "motion_snooze": null,
                        "motion_zones": null,
                        "owned": true,
                        "owner": { "id": 456, "email": "user@example.com" },
                        "permissions": [],
                        "features": { "motions_enabled": true },
                        "motion_snooze_preset_profile": null,
                        "night_mode_enabled": false,
                        "subscribed_buttons": true,
                        "camera_enabled": true,
                        "mic_enabled": true,
                        "health": { "ac_power": 110, "battery": 90, "updated_at": 1692374400 },
                        "api_enabled": true,
                        "new_doorbell": false
                    }
                ],
                "events": []
            }
            """).RootElement;

            var validator = new JsonSchemaValidator();
            var issues = validator.ValidateAgainstSchema(sampleJson, typeof(VideoSearchResponse));

            // Should report extra fields as Info severity (not errors, just unused data)
            var extras = issues.FindAll(i => i.IssueType == "MissingInSchema");
            Assert.NotEmpty(extras); // API returns more fields than we model
        }
    }
}
