namespace VideoForensics.Providers.Ring.Tests.Mocks
{
    /// <summary>
    /// Test fixtures containing sample API responses for mocking
    /// </summary>
    public static class TestFixtures
    {
        public static class AuthResponses
        {
            public static string SuccessfulOAuthToken => @"{
                ""access_token"": ""eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.test"",
                ""refresh_token"": ""eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.test_refresh.test"",
                ""expires_in"": 3600,
                ""token_type"": ""Bearer"",
                ""scope"": ""profile email""
            }";

            public static string InvalidCredentialsError => @"{
                ""error"": ""invalid_request"",
                ""error_description"": ""Invalid email or password""
            }";

            public static string TwoFactorRequiredError => @"{
                ""error"": ""mfa_required"",
                ""error_description"": ""Two factor authentication is required"",
                ""mfa_token"": ""mfa_token_12345""
            }";
        }

        public static class DeviceResponses
        {
            public static string DevicesWithDoorbot => @"{
                ""doorbots"": [
                    {
                        ""id"": 123456,
                        ""description"": ""Front Door"",
                        ""device_kind"": ""doorbot"",
                        ""subscribed"": true,
                        ""subscribed_buttons"": [""motion"", ""snapshot""],
                        ""battery"": ""full"",
                        ""external_connection"": false,
                        ""firmware"": ""1.8.30"",
                        ""kind"": ""doorbot"",
                        ""motion_zones"": [],
                        ""motion_snooze"": null,
                        ""video_stats"": {
                            ""status"": ""ok""
                        }
                    }
                ],
                ""authorized_doorbots"": [],
                ""stickup_cams"": [],
                ""base_stations"": [],
                ""chimes"": [
                    {
                        ""id"": 789012,
                        ""description"": ""Chime"",
                        ""device_kind"": ""chime"",
                        ""subscribed"": true,
                        ""firmware"": ""1.4.28"",
                        ""kind"": ""chime_v3""
                    }
                ],
                ""owned"": true
            }";

            public static string DevicesEmpty => @"{
                ""doorbots"": [],
                ""authorized_doorbots"": [],
                ""stickup_cams"": [],
                ""base_stations"": [],
                ""chimes"": [],
                ""owned"": true
            }";
        }

        public static class HistoryResponses
        {
            public static string MotionEventHistory => @"{
                ""events"": [
                    {
                        ""id"": ""event123"",
                        ""id_str"": ""event123"",
                        ""created_at"": ""2024-01-15T10:30:00.000Z"",
                        ""motion"": true,
                        ""snapshot_url"": ""https://api.ring.com/api/v1/videos/event123/snapshot.jpg"",
                        ""kind"": ""motion"",
                        ""favorite"": false,
                        ""snapshot"": {
                            ""status"": ""ready""
                        },
                        ""video_status"": ""ready""
                    },
                    {
                        ""id"": ""event124"",
                        ""id_str"": ""event124"",
                        ""created_at"": ""2024-01-15T09:15:00.000Z"",
                        ""motion"": true,
                        ""snapshot_url"": ""https://api.ring.com/api/v1/videos/event124/snapshot.jpg"",
                        ""kind"": ""motion"",
                        ""favorite"": false,
                        ""snapshot"": {
                            ""status"": ""ready""
                        }
                    }
                ]
            }";

            public static string DoorbellEventHistory => @"{
                ""events"": [
                    {
                        ""id"": ""doorbell1"",
                        ""id_str"": ""doorbell1"",
                        ""created_at"": ""2024-01-15T11:00:00.000Z"",
                        ""motion"": false,
                        ""snapshot_url"": ""https://api.ring.com/api/v1/videos/doorbell1/snapshot.jpg"",
                        ""kind"": ""motion"",
                        ""favorite"": false,
                        ""snapshot"": {
                            ""status"": ""ready""
                        }
                    }
                ]
            }";
        }

        public static class LocationResponses
        {
            public static string LocationsList => @"{
                ""locations"": [
                    {
                        ""location_id"": ""home-abc123"",
                        ""name"": ""Home"",
                        ""company_id"": null,
                        ""timezone"": ""America/New_York"",
                        ""address"": {
                            ""street"": ""123 Main St"",
                            ""city"": ""Anytown"",
                            ""state"": ""NY"",
                            ""zip"": ""12345"",
                            ""country"": ""US""
                        }
                    }
                ]
            }";
        }

        public static class SnapshotResponses
        {
            public static string SnapshotTimestamp => @"{
                ""doorbot_id"": 123456,
                ""thumbnail"": ""https://api.ring.com/api/v1/videos/event123/thumbnail.jpg"",
                ""timestamp"": 1705318200000
            }";

            public static string MultipleSnapshots => @"{
                ""snapshots"": [
                    {
                        ""doorbot_id"": 123456,
                        ""thumbnail"": ""https://api.ring.com/api/v1/videos/event123/thumbnail.jpg"",
                        ""timestamp"": 1705318200000
                    },
                    {
                        ""doorbot_id"": 123456,
                        ""thumbnail"": ""https://api.ring.com/api/v1/videos/event124/thumbnail.jpg"",
                        ""timestamp"": 1705314600000
                    }
                ]
            }";
        }

        public static class RecordingResponses
        {
            public static string RecordingShareUrl => @"{
                ""url"": ""https://ring.com/share/video/xyz789abc""
            }";

            public static string RecordingMetadata => @"{
                ""id"": ""event123"",
                ""id_str"": ""event123"",
                ""created_at"": ""2024-01-15T10:30:00.000Z"",
                ""video_status"": ""ready"",
                ""kind"": ""motion""
            }";
        }

        public static class ErrorResponses
        {
            public static string NotFound => @"{
                ""error"": ""not_found"",
                ""error_description"": ""Resource not found""
            }";

            public static string Unauthorized => @"{
                ""error"": ""unauthorized"",
                ""error_description"": ""Authentication token expired or invalid""
            }";

            public static string RateLimitExceeded => @"{
                ""error"": ""rate_limit_exceeded"",
                ""error_description"": ""Too many requests. Please try again later.""
            }";

            public static string InternalServerError => @"{
                ""error"": ""internal_error"",
                ""error_description"": ""Internal server error""
            }";
        }
    }
}
