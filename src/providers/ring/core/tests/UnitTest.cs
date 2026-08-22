using System;

using VideoForensics.Providers.Ring;

namespace VideoForensics.Providers.Ring.Tests
{
    [TestClass]
    public class UnitTest
    {
        /// <summary>
        /// Credentials auto-discovered from the RingVideos app's saved config (if App.config doesn't
        /// already provide a refresh token or username/password of its own).
        /// </summary>
        private static readonly Lazy<(string? UserName, string? Password, string? RefreshToken)> AutoDiscoveredCredentials = new(() =>
        {
            var hasAppConfigCredentials = !string.IsNullOrEmpty(ConfigurationManager.AppSettings["RingRefreshToken"])
                || (!string.IsNullOrEmpty(ConfigurationManager.AppSettings["RingUsername"]) && !string.IsNullOrEmpty(ConfigurationManager.AppSettings["RingPassword"]));

            if (!hasAppConfigCredentials && RingVideosCredentialLocator.TryLoad(out var userName, out var password, out var refreshToken))
            {
                return (userName, password, refreshToken);
            }

            return (null, null, null);
        });

        /// <summary>
        /// Username to use to connect to the Ring API
        /// </summary>
#pragma warning disable CS8603 // Possible null reference return.
        public static string Username => string.IsNullOrEmpty(ConfigurationManager.AppSettings["RingUsername"])
            ? AutoDiscoveredCredentials.Value.UserName
            : ConfigurationManager.AppSettings["RingUsername"];
#pragma warning restore CS8603 // Possible null reference return.

        /// <summary>
        /// Password to use to connect to the Ring API
        /// </summary>
#pragma warning disable CS8603 // Possible null reference return.
        public static string Password => string.IsNullOrEmpty(ConfigurationManager.AppSettings["RingPassword"])
            ? AutoDiscoveredCredentials.Value.Password
            : ConfigurationManager.AppSettings["RingPassword"];
#pragma warning restore CS8603 // Possible null reference return.

        /// <summary>
        /// Two factor authentication token to use to connect to the Ring API
        /// </summary>
        public static string TwoFactorAuthenticationToken
        {
#pragma warning disable CS8603 // Possible null reference return.
            get { return ConfigurationManager.AppSettings["TwoFactorAuthenticationToken"]; }
#pragma warning restore CS8603 // Possible null reference return.
            set
            {
                var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                if (configFile.AppSettings.Settings["TwoFactorAuthenticationToken"] != null)
                {
                    configFile.AppSettings.Settings["TwoFactorAuthenticationToken"].Value = value;
                }
                else
                {
                    configFile.AppSettings.Settings.Add("TwoFactorAuthenticationToken", value);
                }
                configFile.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection(configFile.AppSettings.SectionInformation.Name);
            }
        }
        /// <summary>
        /// Refresh token used to connect to the Ring API
        /// </summary>
        public static string RefreshToken
        {
#pragma warning disable CS8603 // Possible null reference return.
            get
            {
                var configured = ConfigurationManager.AppSettings["RingRefreshToken"];
                return string.IsNullOrEmpty(configured) ? AutoDiscoveredCredentials.Value.RefreshToken : configured;
            }
#pragma warning restore CS8603 // Possible null reference return.
            set
            {
                var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                if (configFile.AppSettings.Settings["RingRefreshToken"] != null)
                {
                    configFile.AppSettings.Settings["RingRefreshToken"].Value = value;
                }
                else
                {
                    configFile.AppSettings.Settings.Add("RingRefreshToken", value);
                }
                configFile.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection(configFile.AppSettings.SectionInformation.Name);
            }
        }

        /// <summary>
        /// Session set up by the initializer and used by the Unit Tests to perform actions against the Ring API
        /// </summary>
        public static Session? session;

        /// <summary>
        /// Prepares the Unit Test by setting up a session to Ring
        /// </summary>
        /// <param name="testContext"></param>
        [ClassInitialize]
        public static async Task TestInitialize(TestContext testContext)
        {
            // Check if we have a refresh token to authenticate to Ring with
            if (string.IsNullOrEmpty(RefreshToken))
            {
                // No refresh token available, try to authenticate with the credentials from the config file
                session = new Session(Username, Password);

                Ring.Api.Entities.Session? authResult = null;
                try
                {
                    authResult = await session.Authenticate(twoFactorAuthCode: TwoFactorAuthenticationToken);

                    if (!string.IsNullOrEmpty(TwoFactorAuthenticationToken))
                    {
                        // Clear the configured two factor authentication code in the configuration file after we've used it once as it won't be valid anymore next time
                        TwoFactorAuthenticationToken = string.Empty;
                    }
                }
                catch (Ring.Api.Exceptions.TwoFactorAuthenticationRequiredException)
                {
                    Assert.Fail("Ring account requires two factor authentication. Add the token received through text message to the config file as 'TwoFactorAuthenticationToken' and run the test again.");
                }
                catch (Ring.Api.Exceptions.TwoFactorAuthenticationIncorrectException)
                {
                    Assert.Fail("The two factor authentication token provided in the config file as 'TwoFactorAuthenticationToken' is invalid or has expired.");
                }
                Assert.IsFalse(authResult == null, "Failed to authenticate");

                // Store the refresh token for subsequent runs
                RefreshToken = session.OAuthToken.RefreshToken;
            }
            else
            {
                // Use the refresh token to set up a new session with Ring so we don't have to deal with the two factor authentication anymore
                session = await Session.GetSessionByRefreshToken(RefreshToken);

                Assert.IsFalse(session == null || session.OAuthToken == null || string.IsNullOrEmpty(session.OAuthToken.AccessToken), "Failed to authenticate using refresh token");
            }
        }

        /// <summary>
        /// Test the scenario where the authentication would fail
        /// </summary>
        [TestMethod]
        public async Task AuthenticateFailTest()
        {
            try
            {
                var session = new Session("test@test.com", "someinvalidpassword");
                await session.Authenticate();
                Assert.Fail("Should have thrown AuthenticationFailedException");
            }
            catch (Ring.Api.Exceptions.AuthenticationFailedException)
            {
            }
        }

        /// <summary>
        /// Test the scenario where a refresh token is used to successfully set up an authenticated session
        /// </summary>
        [TestMethod]
        public async Task AuthenticateWithRefreshTokenSuccessTest()
        {
            if (!IsSessionActive()) return;

            Assert.IsNotNull(session, "No active session available");

            // Request a new authenticated session based on the RefreshToken
            var refreshedSession = await Session.GetSessionByRefreshToken(session.OAuthToken.RefreshToken);
            Assert.IsTrue(refreshedSession.IsAuthenticated, "Failed to authenticate using refresh token");
        }

        /// <summary>
        /// Test the scenario where a refresh token is used to set up an authenticated session which fails
        /// </summary>
        [TestMethod]
        public async Task AuthenticateWithRefreshTokenFailTest()
        {
            try
            {
                // Request a new authenticated session based on a non existing RefreshToken
                await Session.GetSessionByRefreshToken("abcdefghijklmnopqrstuvwxyz");
                Assert.Fail("Should have thrown AuthenticationFailedException");
            }
            catch (Ring.Api.Exceptions.AuthenticationFailedException)
            {
            }
        }

        /// <summary>
        /// Test if the devices can be retrieved
        /// </summary>
        [TestMethod]
        public async Task GetDevicesTest()
        {
            if (!IsSessionActive()) return;

            Assert.IsNotNull(session, "No active session available");

            var devices = await session.GetRingDevices();
            Assert.IsTrue(devices.Chimes.Count > 0 || devices.Doorbots.Count > 0 || devices.AuthorizedDoorbots.Count > 0 || devices.StickupCams.Count > 0, "No doorbots, stickup cams and/or chimes returned");
        }

        /// <summary>
        /// Test if the an SessionNotAuthenticatedException gets thrown when trying to retrieve the Ring devices without authenticating first
        /// </summary>
        [TestMethod]
        public async Task GetDevicesUnauthenticatedTest()
        {
            try
            {
                var session = new Session("", "");
                await session.GetRingDevices();
                Assert.Fail("Should have thrown SessionNotAuthenticatedException");
            }
            catch (Ring.Api.Exceptions.SessionNotAuthenticatedException)
            {
            }
        }

        /// <summary>
        /// Test if the doorbot history events can be retrieved with the default amount of items
        /// </summary>
        [TestMethod]
        public async Task GetDoorbotsHistoryTest()
        {
            if (!IsSessionActive()) return;

            Assert.IsNotNull(session, "No active session available");

            var doorbotHistory = await session.GetDoorbotsHistory();
            Assert.IsTrue(doorbotHistory.Count > 0, "No doorbot history items returned");
            Assert.IsTrue(doorbotHistory.Count == 20, $"{doorbotHistory.Count} doorbot history items returned while 20 were expected");
        }

        /// <summary>
        /// Test if the doorbot history events can be retrieved only for a specific doorbot with the default amount of items
        /// </summary>
        [TestMethod]
        public async Task GetDoorbotsHistoryForSpecificDoorbotTest()
        {
            if (!IsSessionActive()) return;

            Assert.IsNotNull(session, "No active session available");

            // Get the available Ring devices
            var devices = await session.GetRingDevices();

            // Ensure there's at least one doorbot available
            if (devices.Doorbots.Count == 0 && devices.AuthorizedDoorbots.Count == 0)
            {
                Assert.Inconclusive("There are no Ring doorbots available under this account to perform this test with");
                return;
            }

            // Take the first doorbot to retrieve the historical items for
            var doorbot = devices.Doorbots.Count > 0 ? devices.Doorbots[0] : devices.AuthorizedDoorbots[0];

            // Get the historical items for the specific doorbot
            var doorbotHistory = await session.GetDoorbotsHistory(doorbotId: doorbot.Id);

            Assert.IsFalse(doorbotHistory.Count == 0, "No doorbot history items returned");
        }

        /// <summary>
        /// Test if the result if doorbot history events are tried to be retrieved only for a specific doorbot which does not exist
        /// </summary>
        [TestMethod]
        public async Task GetDoorbotsHistoryForSpecificNonExistingDoorbotTest()
        {
            if (!IsSessionActive()) return;

            Assert.IsNotNull(session, "No active session available");

            try
            {
                // Try getting the historical items for the a doorbot that does not exist
                await session.GetDoorbotsHistory(doorbotId: 1234567);
                Assert.Fail("Should have thrown DeviceUnknownException");
            }
            catch (Ring.Api.Exceptions.DeviceUnknownException)
            {
            }
        }

        /// <summary>
        /// Test if the doorbot history events can be retrieved with a specific amount of items
        /// </summary>
        [TestMethod]
        public async Task GetDoorbotsHistoryWithLimitTest()
        {
            if (!IsSessionActive()) return;

            Assert.IsNotNull(session, "No active session available");

            var limit = 250;

            var doorbotHistory = await session.GetDoorbotsHistory(limit);
            Assert.IsTrue(doorbotHistory.Count > 0, "No doorbot history items returned");
            Assert.IsTrue(doorbotHistory.Count == limit, $"{doorbotHistory.Count} doorbot history items returned while {limit} were expected");
        }

        /// <summary>
        /// Test if the doorbot history events can be retrieved within a specific timeframe
        /// </summary>
        [TestMethod]
        public async Task GetDoorbotsHistoryByDateSpanTest()
        {
            if (!IsSessionActive()) return;

            Assert.IsNotNull(session, "No active session available");

            var startDate = DateTime.Now.AddDays(-2);
            var endDate = DateTime.Now.AddDays(-1);

            var doorbotHistory = await session.GetDoorbotsHistory(startDate, endDate);
            Assert.IsTrue(doorbotHistory.Count > 0, "No doorbot history items returned");
            Assert.AreEqual(0, doorbotHistory.Count(h => !h.CreatedAtDateTime.HasValue || (h.CreatedAtDateTime.Value > endDate && h.CreatedAtDateTime.Value < startDate)), "Doorbot history items have been returned which don't fall within the provided period");
        }

        /// <summary>
        /// Test if the recording for a doorbot history event can be retrieved
        /// </summary>
        [TestMethod]
        public async Task GetDoorbotsHistoryRecordingByIdTest()
        {
            if (!IsSessionActive()) return;

            Assert.IsNotNull(session, "No active session available");

            var doorbotHistory = await session.GetDoorbotsHistory();

            Assert.IsTrue(doorbotHistory.Count > 0, "No doorbot history events were found");

            var tempFilePath = Path.GetTempFileName();

            await session.GetDoorbotHistoryRecording(doorbotHistory[0].Id.ToString(), tempFilePath);

            File.Delete(tempFilePath);
        }

        /// <summary>
        /// Test if the recording for a doorbot history event can be retrieved
        /// </summary>
        [TestMethod]
        public async Task GetDoorbotsHistoryRecordingByInstanceTest()
        {
            if (!IsSessionActive()) return;

            Assert.IsNotNull(session, "No active session available");

            var doorbotHistory = await session.GetDoorbotsHistory(limit: 1);

            Assert.IsTrue(doorbotHistory.Count > 0, "No doorbot history events were found");

            var tempFilePath = Path.GetTempFileName();

            await session.GetDoorbotHistoryRecording(doorbotHistory[0], tempFilePath);

            File.Delete(tempFilePath);
        }

        /// <summary>
        /// Test if the recording for a doorbot history event can be shared
        /// </summary>
        [TestMethod]
        public async Task ShareRecordingTest()
        {
            if (!IsSessionActive()) return;

            Assert.IsNotNull(session, "No active session available");

            var doorbotHistory = await session.GetDoorbotsHistory(limit: 1);

            Assert.IsTrue(doorbotHistory.Count > 0, "No doorbot history events were found");

            await session.ShareRecording(doorbotHistory[0]);
        }

        /// <summary>
        /// Test if the latest snapshot from a doorbot can be downloaded
        /// </summary>
        [TestMethod]
        public async Task DownloadLatestSnapshotTest()
        {
            if (!IsSessionActive()) return;

            Assert.IsNotNull(session, "No active session available");

            var devices = await session.GetRingDevices();
            Assert.IsTrue(devices != null, "Unable to retrieve Ring devices");
            Assert.IsTrue((devices.AuthorizedDoorbots != null && devices.AuthorizedDoorbots.Count > 0) || (devices.Doorbots != null && devices.Doorbots.Count > 0), "Retrieved Ring devices do not contain any doorbots");

            var tempFilePath = Path.GetTempFileName();

            await session.GetLatestSnapshot(devices.AuthorizedDoorbots?.Count > 0 ? devices.AuthorizedDoorbots[0] : devices.Doorbots[0], tempFilePath);

            File.Delete(tempFilePath);
        }

        /// <summary>
        /// Test if requesting snapshots to be refreshed succeeds
        /// </summary>
        [TestMethod]
        public async Task UpdateSnapshotTest()
        {
            if (!IsSessionActive()) return;

            Assert.IsNotNull(session, "No active session available");

            var devices = await session.GetRingDevices();
            Assert.IsTrue(devices != null, "Unable to retrieve Ring devices");
            Assert.IsTrue((devices.AuthorizedDoorbots != null && devices.AuthorizedDoorbots.Count > 0) || (devices.Doorbots != null && devices.Doorbots.Count > 0), "Retrieved Ring devices do not contain any doorbots");

            await session.UpdateSnapshot((devices.AuthorizedDoorbots?.Count > 0 ? devices.AuthorizedDoorbots : devices.Doorbots)[0]);
        }

        /// <summary>
        /// Test if we can retrieve the date and time at which a snapshot was last taken from a Ring doorbot device
        /// </summary>
        [TestMethod]
        public async Task GetSnapshotTimestampTest()
        {
            if (!IsSessionActive()) return;

            Assert.IsNotNull(session, "No active session available");

            var devices = await session.GetRingDevices();
            Assert.IsTrue(devices != null, "Unable to retrieve Ring devices");
            Assert.IsTrue((devices.AuthorizedDoorbots != null && devices.AuthorizedDoorbots.Count > 0) || (devices.Doorbots != null && devices.Doorbots.Count > 0), "Retrieved Ring devices do not contain any doorbots");

            var doorbotSnapshotTimestamps = await session.GetDoorbotSnapshotTimestamp((devices.AuthorizedDoorbots?.Count > 0 ? devices.AuthorizedDoorbots : devices.Doorbots)[0]);

            Assert.IsTrue(doorbotSnapshotTimestamps.Timestamp.Count > 0, "No timestamps were returned for the doorbot");
            Assert.IsTrue(doorbotSnapshotTimestamps.Timestamp[0].Timestamp.HasValue, "Unable to define the date and time for the last snapshot of the doorbot");
        }

        /// <summary>
        /// Test FlexibleStringConverter is available for use
        /// </summary>
        [TestMethod]
        public void FlexibleStringConverterTest()
        {
            // Verify the converter exists and can be instantiated
            var converter = new Ring.Api.Converters.FlexibleStringConverter();
            Assert.IsNotNull(converter, "FlexibleStringConverter should be instantiable");

            // Test that StickupCam with flexible LedStatus deserializes correctly
            var jsonWithStringLedStatus = """{"id": 1, "led_status": "on", "description": "test"}""";
            var cam1 = System.Text.Json.JsonSerializer.Deserialize<Ring.Api.Entities.StickupCam>(jsonWithStringLedStatus);
            Assert.IsNotNull(cam1, "StickupCam should deserialize with string led_status");
            Assert.AreEqual("on", cam1.LedStatus, "LedStatus should be 'on'");

            // Test with number LedStatus (the reason for FlexibleStringConverter)
            var jsonWithNumberLedStatus = """{"id": 1, "led_status": 1, "description": "test"}""";
            var cam2 = System.Text.Json.JsonSerializer.Deserialize<Ring.Api.Entities.StickupCam>(jsonWithNumberLedStatus);
            Assert.IsNotNull(cam2, "StickupCam should deserialize with number led_status");
            Assert.AreEqual("1", cam2.LedStatus, "LedStatus should convert number to string");
        }

        /// <summary>
        /// Test GetLocations() returns expected structure
        /// </summary>
        [TestMethod]
        public async Task GetLocationsTest()
        {
            if (!IsSessionActive()) return;

            Assert.IsNotNull(session, "No active session available");

            var locations = await session.GetLocations();

            Assert.IsNotNull(locations, "GetLocations() should not return null");
            Assert.IsTrue(locations is System.Collections.Generic.List<Ring.Api.Entities.Location>, "Should return a list of Location objects");
        }

        /// <summary>
        /// Test that LocationId property exists on Chime and Doorbot and deserializes correctly
        /// </summary>
        [TestMethod]
        public void LocationIdDeserializationTest()
        {
            var chimeJson = """{"id": 1, "location_id": "550e8400-e29b-41d4-a716-446655440000", "description": "test"}""";
            var chime = System.Text.Json.JsonSerializer.Deserialize<Ring.Api.Entities.Chime>(chimeJson);

            Assert.IsNotNull(chime, "Chime should deserialize");
            Assert.AreEqual(new System.Guid("550e8400-e29b-41d4-a716-446655440000"), chime.LocationId, "LocationId should deserialize from JSON");

            var doorbotJson = """{"id": 1, "location_id": "550e8400-e29b-41d4-a716-446655440001", "description": "test"}""";
            var doorbot = System.Text.Json.JsonSerializer.Deserialize<Ring.Api.Entities.Doorbot>(doorbotJson);

            Assert.IsNotNull(doorbot, "Doorbot should deserialize");
            Assert.AreEqual(new System.Guid("550e8400-e29b-41d4-a716-446655440001"), doorbot.LocationId, "LocationId should deserialize from JSON");
        }

        /// <summary>
        /// Test ApiRawLogger events are available for subscription
        /// </summary>
        [TestMethod]
        public void ApiRawLoggerTest()
        {
            // Verify that the ApiRawLogger events exist and can be subscribed to. The events are
            // raised internally by the API when making actual HTTP calls - this test only confirms
            // subscribing doesn't throw; it passes as long as it completes without an exception.
            Ring.Api.ApiRawLogger.OnRawResponse += (call) => { };
            Ring.Api.ApiRawLogger.OnEvent += (evt) => { };
            Ring.Api.ApiRawLogger.OnRingEvents += (evt) => { };
        }

        /// <summary>
        /// Check if there is an active Ring session created by the class initializer
        /// </summary>
        /// <returns>True if there's an active session, false if not</returns>
        private bool IsSessionActive()
        {
            if (session == null)
            {
                Assert.Inconclusive("Test can't be done as there's no active session");
                return false;
            }

            return true;
        }
    }
}

