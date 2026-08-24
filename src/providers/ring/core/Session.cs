using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using System.Text.Json;
using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Common;
using VideoForensics.Providers.Ring.Common.Interfaces;
using System.Collections.Specialized;
using System.Reflection.Metadata.Ecma335;

namespace VideoForensics.Providers.Ring
{
    public partial class Session
    {
        #region Properties

        /// <summary>
        /// Stable hardware ID generated for this machine to avoid duplicate auth entries.
        /// Persisted to disk so it remains consistent across sessions.
        /// </summary>
        private static readonly string _hardwareId = GetOrCreateHardwareId();

        private static string GetOrCreateHardwareId()
        {
            var directoryService = new PlatformDirectoryService();
            var folder = directoryService.GetApplicationDataDirectory();
            var filePath = Path.Combine(folder, "hardware_id.txt");
            try
            {
                if (File.Exists(filePath))
                {
                    var id = File.ReadAllText(filePath).Trim();
                    if (!string.IsNullOrEmpty(id)) return id;
                }
            }
            catch { }

            var newId = Guid.NewGuid().ToString();
            try
            {
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                File.WriteAllText(filePath, newId);
            }
            catch { }
            return newId;
        }

        /// <summary>
        /// Username to use to connect to the Ring API. Set by providing it in the constructor.
        /// </summary>
        public string Username { get; private set; }

        /// <summary>
        /// Password to use to connect to the Ring API. Set by providing it in the constructor.
        /// </summary>
        public string Password { get; private set; }

        /// <summary>
        /// Uri on which OAuth tokens can be requested from Ring
        /// </summary>
        public Uri OAuthUrl => new Uri("https://oauth.ring.com/oauth/token");

        /// <summary>
        /// Base Uri with which all Ring API requests start
        /// </summary>
        public Uri BaseUrl => new Uri("https://api.ring.com/clients_api/");

        /// <summary>
        /// Boolean indicating if the current session is authenticated
        /// </summary>
        public bool IsAuthenticated => OAuthToken != null;

        /// <summary>
        /// Authentication Token that will be used to communicate with the Ring API
        /// </summary>
        public string AuthenticationToken
        {
            get { return OAuthToken?.AccessToken; }
        }

        /// <summary>
        /// OAuth Token for communicating with the Ring API
        /// </summary>
        public OAutToken OAuthToken { get; private set; }

        #endregion

        #region Fields

        /// <summary>
        /// HttpUtility instance to make HTTP requests
        /// </summary>
        private readonly HttpUtility _httpUtility;

        /// <summary>
        /// Downloads recording bytes from an already-resolved URL. Doesn't need authentication -
        /// recording URLs from GetDoorbotHistoryRecordingInfo are pre-signed.
        /// </summary>
        private readonly IVideoDownloader _videoDownloader;

        #endregion

        #region Constructors

        /// <summary>
        /// Initiates a new session to the Ring API
        /// </summary>
        /// <param name="username">Username for the Ring account</param>
        /// <param name="password">Password for the Ring account</param>
        /// <param name="messageHandler">Optional custom HttpMessageHandler for testing purposes</param>
        /// <param name="videoDownloader">Optional custom IVideoDownloader for testing purposes</param>
        public Session(string username, string password, System.Net.Http.HttpMessageHandler messageHandler = null, IVideoDownloader videoDownloader = null)
        {
            Username = username;
            Password = password;
            _httpUtility = new HttpUtility(messageHandler: messageHandler);
            _videoDownloader = videoDownloader ?? new VideoDownloader();
        }

        /// <summary>
        /// Initiates a new session without username/password. Only to be used with the static method to create a session based on a RefreshToken.
        /// </summary>
        /// <param name="messageHandler">Optional custom HttpMessageHandler for testing purposes</param>
        /// <param name="videoDownloader">Optional custom IVideoDownloader for testing purposes</param>
        private Session(System.Net.Http.HttpMessageHandler messageHandler = null, IVideoDownloader videoDownloader = null)
        {
            _httpUtility = new HttpUtility(messageHandler: messageHandler);
            _videoDownloader = videoDownloader ?? new VideoDownloader();
        }

        #endregion

        #region Static methods

        /// <summary>
        /// Creates a new session to the Ring API using a RefreshToken received from a previous session
        /// </summary>
        /// <param name="refreshToken">RefreshToken received from the prior authentication</param>
        /// <param name="messageHandler">Optional custom HttpMessageHandler for testing purposes</param>
        /// <returns>Authenticated session based on the RefreshToken or NULL if the session could not be authenticated</returns>
        /// <exception cref="Exceptions.AuthenticationFailedException">Thrown when the refresh token is invalid.</exception>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationIncorrectException">Thrown when the web server indicates the two-factor code was incorrect (HTTP 400).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationRequiredException">Thrown when the web server indicates two-factor authentication is required (HTTP 412).</exception>
        public static async Task<Session> GetSessionByRefreshToken(string refreshToken, System.Net.Http.HttpMessageHandler messageHandler = null)
        {
            var session = new Session(messageHandler);
            await session.RefreshSession(refreshToken);
            return session;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Authenticates to the Ring API
        /// </summary>
        /// <param name="operatingSystem">Operating system from which this API is accessed. Defaults to 'windows'. Required field.</param>
        /// <param name="hardwareId">Hardware identifier of the device for which this API is accessed. Defaults to 'unspecified'. Required field.</param>
        /// <param name="appBrand">Device brand for which this API is accessed. Defaults to 'ring'. Optional field.</param>
        /// <param name="deviceModel">Device model for which this API is accessed. Defaults to 'unspecified'. Optional field.</param>
        /// <param name="deviceName">Name of the device from which this API is being used. Defaults to 'unspecified'. Optional field.</param>
        /// <param name="resolution">Screen resolution on which this API is being used. Defaults to '800x600'. Optional field.</param>
        /// <param name="appVersion">Version of the app from which this API is being used. Defaults to '1.3.810'. Optional field.</param>
        /// <param name="appInstallationDate">Date and time at which the app was installed from which this API is being used. By default not specified. Optional field.</param>
        /// <param name="manufacturer">Name of the manufacturer of the product for which this API is being accessed. Defaults to 'unspecified'. Optional field.</param>
        /// <param name="deviceType">Type of device from which this API is being used. Defaults to 'tablet'. Optional field.</param>
        /// <param name="architecture">Architecture of the system from which this API is being used. Defaults to 'x64'. Optional field.</param>
        /// <param name="language">Language of the app from which this API is being used. Defaults to 'en'. Optional field.</param>
        /// <param name="twoFactorAuthCode">The two factor authentication code retrieved through a text message to authenticate to two factor authentication enabled accounts. Leave this NULL at first to retrieve the text message. Then use this method again specifying the proper number received in the text message to finalize authentication.</param>
        /// <returns>Session object if the authentication was successful</returns>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationIncorrectException">Thrown when the web server indicates the two-factor code was incorrect (HTTP 400).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationRequiredException">Thrown when the web server indicates two-factor authentication is required (HTTP 412).</exception>
        public async Task<Entities.Session> Authenticate(string operatingSystem = "windows",
                                                            string hardwareId = null,
                                                            string appBrand = "ring",
                                                            string deviceModel = "unspecified",
                                                            string deviceName = "unspecified",
                                                            string resolution = "800x600",
                                                            string appVersion = "2.1.8",
                                                            DateTime? appInstallationDate = null,
                                                            string manufacturer = "unspecified",
                                                            string deviceType = "tablet",
                                                            string architecture = "x64",
                                                            string language = "en",
                                                            string twoFactorAuthCode = null)
        {
            // Check for mandatory parameters
            if (string.IsNullOrEmpty(operatingSystem))
            {
                throw new ArgumentNullException("operatingSystem", "Operating system is mandatory");
            }

            if (string.IsNullOrEmpty(hardwareId))
            {
                hardwareId = _hardwareId;
            }

            // Construct the Form POST fields to send along with the authentication request
            var oAuthformFields = new Dictionary<string, string>
            {
                { "grant_type", "password" },
                { "username", Username },
                { "password", Password},
                { "client_id", "ring_official_android" },
                { "scope", "client" }
            };

            // If a two factor auth code has been provided, add the code through the HTTP POST header
            var headerFields = new NameValueCollection(capacity: 3)
            {
                { "hardware_id", hardwareId },
                { "2fa-support", "true" }
            };
            if (twoFactorAuthCode != null)
            {
                headerFields["2fa-code"] = twoFactorAuthCode;
            }

            ApiRawLogger.LogEvent("Auth", twoFactorAuthCode != null
                ? "Authenticating with username/password + two-factor code"
                : "Authenticating with username/password");

            // Make the OAuth POST request to request an OAuth Token
            var oAuthResponse = await _httpUtility.OAuthPost(OAuthUrl,
                                                            oAuthformFields,
                                                            headerFields);


            // Deserialize the JSON result into a typed object
            OAuthToken = JsonSerializer.Deserialize<OAutToken>(oAuthResponse);

            // Create an API session using JSON (matching Ring's current API requirements)
            await CreateSession();

            ApiRawLogger.LogEvent("Auth", "Authenticated successfully");

            return null;
        }

        /// <summary>
        /// Creates an API session with Ring using the current OAuth token
        /// </summary>
        private async Task CreateSession()
        {
            var sessionData = new
            {
                device = new
                {
                    hardware_id = _hardwareId,
                    metadata = new
                    {
                        api_version = "11",
                        device_model = "ring-doorbell"
                    },
                    os = "android"
                }
            };

            var json = JsonSerializer.Serialize(sessionData);
            var headerFields = new NameValueCollection
            {
                { "Accept-Encoding", "gzip, deflate" },
                { "X-API-LANG", "en" },
                { "Authorization", $"Bearer {OAuthToken.AccessToken}" },
                { "hardware_id", _hardwareId }
            };

            var response = await _httpUtility.JsonPostRaw(new Uri(BaseUrl, "session"), json, headerFields);
        }

        /// <summary>
        /// Authenticates to the Ring API using the refresh token in the current session
        /// </summary>
        /// <exception cref="Exceptions.AuthenticationFailedException">Thrown when the refresh token is invalid.</exception>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationIncorrectException">Thrown when the web server indicates the two-factor code was incorrect (HTTP 400).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationRequiredException">Thrown when the web server indicates two-factor authentication is required (HTTP 412).</exception>
        public async Task RefreshSession() => await RefreshSession(OAuthToken.RefreshToken);

        /// <summary>
        /// Authenticates to the Ring API using the provided refresh token
        /// </summary>
        /// <param name="refreshToken">RefreshToken to set up a new authenticated session</param>
        /// <exception cref="Exceptions.AuthenticationFailedException">Thrown when the refresh token is invalid.</exception>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationIncorrectException">Thrown when the web server indicates the two-factor code was incorrect (HTTP 400).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationRequiredException">Thrown when the web server indicates two-factor authentication is required (HTTP 412).</exception>
        public async Task RefreshSession(string refreshToken)
        {
            // Check for mandatory parameters
            if (string.IsNullOrEmpty(refreshToken))
            {
                throw new ArgumentNullException(nameof(refreshToken), "refreshToken is mandatory");
            }

            // Construct the Form POST fields to send along with the authentication request
            var oAuthformFields = new Dictionary<string, string>
            {
                { "grant_type", "refresh_token" },
                { "refresh_token", refreshToken },
                { "client_id", "ring_official_android" },
                { "scope", "client" }
            };

            var headerFields = new NameValueCollection()
            {
                { "hardware_id", _hardwareId },
                { "2fa-support", "true" }
            };
            ApiRawLogger.LogEvent("Auth", "Refreshing session via refresh token");

            // Make the OAuth POST request to request an OAuth Token
            try
            {
                var oAuthResponse = await _httpUtility.OAuthPost(OAuthUrl,
                                                                oAuthformFields,
                                                                headerFields);


                // Deserialize the JSON result into a typed object
                OAuthToken = JsonSerializer.Deserialize<OAutToken>(oAuthResponse);

                // Create an API session after refreshing the OAuth token
                await CreateSession();

                ApiRawLogger.LogEvent("Auth", "Session refreshed successfully");
            }
            catch (System.Net.WebException e)
            {
                // If a WebException gets thrown with Unauthorized it means that the refresh token was not valid, throw a custom exception to indicate this
                if (e.Message.Contains("Unauthorized"))
                {
                    ApiRawLogger.LogEvent("Auth", "Refresh token rejected (unauthorized)");
                    throw new Exceptions.AuthenticationFailedException(e);
                }

                ApiRawLogger.LogEvent("Auth", $"Session refresh failed: {e.Message}");
                throw;
            }
        }

        /// <summary>
        /// Ensure that the current session is authenticated and check if the access token is still valid. If not, it will try to renew the session using the refresh token.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the refresh token is null or empty.</exception>
        /// <exception cref="Exceptions.AuthenticationFailedException">Thrown when the refresh token is invalid.</exception>
        /// <exception cref="Exceptions.SessionNotAuthenticatedException">Thrown when there's no OAuth token, or the OAuth token has expired and there is no valid refresh token.</exception>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationIncorrectException">Thrown when the web server indicates the two-factor code was incorrect (HTTP 400).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationRequiredException">Thrown when the web server indicates two-factor authentication is required (HTTP 412).</exception>
        /// 
        public async Task EnsureSessionValid(CancellationToken cancellationToken = default)
        {
            // Ensure the session is authenticated
            if (!IsAuthenticated)
            {
                // Session is not authenticated
                throw new Exceptions.SessionNotAuthenticatedException();
            }

            // Ensure the access token in the session is still valid
            if (OAuthToken.ExpiresAt < DateTime.Now)
            {
                // Access token is no longer valid, check if we have a refresh token available to refresh the session
                if (string.IsNullOrEmpty(OAuthToken.RefreshToken))
                {
                    // No refresh token available so can't renew the session
                    throw new Exceptions.SessionNotAuthenticatedException();
                }

                // Refresh token available, try refreshing the session
                await RefreshSession();
            }

            // All good
        }

        /// <summary>
        /// Returns all devices registered with Ring under the current account being used
        /// </summary>
        /// <returns>Devices registered with Ring under the current account</returns>
        /// <exception cref="Exceptions.AuthenticationFailedException">Thrown when the refresh token is invalid.</exception>
        /// <exception cref="Exceptions.SessionNotAuthenticatedException">Thrown when there's no OAuth token, or the OAuth token has expired and there is no valid refresh token.</exception>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationIncorrectException">Thrown when the web server indicates the two-factor code was incorrect (HTTP 400).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationRequiredException">Thrown when the web server indicates two-factor authentication is required (HTTP 412).</exception>
        public async Task<Devices> GetRingDevices(Guid? locationId = null)
        {
            await EnsureSessionValid();

            string query = locationId.HasValue
                ? $"ring_devices?api_version=11&location_id={locationId:D}"
                : "ring_devices?api_version=11";

            var response = await _httpUtility.GetContents(new Uri(BaseUrl, query), AuthenticationToken, _hardwareId);

            var devices = JsonSerializer.Deserialize<Devices>(response) ?? new Devices();
            ApiRawLogger.LogEvent("Devices", $"Retrieved {devices.Doorbots?.Count ?? 0} doorbots, {devices.AuthorizedDoorbots?.Count ?? 0} authorized doorbots, {devices.Chimes?.Count ?? 0} chimes, {devices.StickupCams?.Count ?? 0} stickup cams" + (locationId.HasValue ? $" (location {locationId})" : ""));
            return devices;
        }

        /// <summary>
        /// Base Uri for the newer Ring devices API, which (unlike the legacy clients_api) exposes
        /// a working locations endpoint.
        /// </summary>
        public Uri RingDevicesApiBaseUrl => new Uri("https://api.ring.com/devices/v1/");

        /// <summary>
        /// Returns the locations (with friendly names) visible to the current account.
        /// </summary>
        public async Task<List<Entities.Location>> GetLocations()
        {
            await EnsureSessionValid();

            var response = await _httpUtility.GetContents(new Uri(RingDevicesApiBaseUrl, "locations"), AuthenticationToken, _hardwareId);

            var parsed = JsonSerializer.Deserialize<Entities.UserLocationsResponse>(response);
            return parsed?.UserLocations ?? new List<Entities.Location>();
        }

        /// <summary>
        /// Returns all events registered for the doorbots
        /// </summary>
        /// <param name="doorbotId">Id of the doorbot to retrieve the history for. Provide NULL to retrieve the history for all available doorbots.</param>
        /// <param name="limit">Amount of history items to retrieve. If you don't provide this value, Ring will default to returning only the most recent 20 items.</param>
        /// <returns>All events triggered by registered doorbots under the current account</returns>
        /// <exception cref="Exceptions.AuthenticationFailedException">Thrown when the refresh token is invalid.</exception>
        /// <exception cref="Exceptions.SessionNotAuthenticatedException">Thrown when there's no OAuth token, or the OAuth token has expired and there is no valid refresh token.</exception>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationIncorrectException">Thrown when the web server indicates the two-factor code was incorrect (HTTP 400).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationRequiredException">Thrown when the web server indicates two-factor authentication is required (HTTP 412).</exception>
        /// <exception cref="Exceptions.DeviceUnknownException">Thrown when the web server indicates the requested Ring device was not found (HTTP 404).</exception>
        public async Task<List<DoorbotHistoryEvent>> GetDoorbotsHistory(long? doorbotId, int? limit = null)
        {
            await EnsureSessionValid();

            // Receive the first batch
            var response = await _httpUtility.GetContents(new Uri(BaseUrl, $"doorbots/{(doorbotId.HasValue ? $"{doorbotId.Value}/" : "")}history{(limit.HasValue ? $"?limit={limit}" : "")}"), AuthenticationToken, _hardwareId);

            // Parse the result
            var doorbotHistory = JsonSerializer.Deserialize<List<DoorbotHistoryEvent>>(response);

            // If no limit has been specified or the amount of items requested have been returned already, just return whatever has been returned by the API
            if (!limit.HasValue || doorbotHistory.Count >= limit.Value) return doorbotHistory;

            // Calculate how many items we still need to retrieve after this first batch
            var remainingItems = limit.Value - doorbotHistory.Count;

            // Create a list to hold all the results
            var allHistory = new List<DoorbotHistoryEvent>();

            // Add the first batch to the list with all the results
            allHistory.AddRange(doorbotHistory);

            do
            {
                // Retrieve the next batch
                response = await _httpUtility.GetContents(new Uri(BaseUrl, $"doorbots/{(doorbotId.HasValue ? $"{doorbotId.Value}/" : "")}history?limit={remainingItems}&older_than={allHistory.Last().Id}"), AuthenticationToken, _hardwareId);

                // Parse the result
                doorbotHistory = JsonSerializer.Deserialize<List<DoorbotHistoryEvent>>(response);

                // Add this next batch to the list with all the results
                allHistory.AddRange(doorbotHistory);

                // Calculate how many items we still need to retrieve after this next batch
                remainingItems = limit.Value - allHistory.Count;
            }
            // Keep retrieving next batches until nothing is being returned anymore or we have retrieved the amount of items that were requested through the limit
            while (doorbotHistory.Count > 0 && remainingItems > 0);

            return allHistory;
        }

        /// <summary>
        /// Returns all events registered for all the available doorbots
        /// </summary>
        /// <param name="limit">Amount of history items to retrieve. If you don't provide this value, Ring will default to returning only the most recent 20 items.</param>
        /// <returns>All events triggered by registered doorbots under the current account</returns>
        /// <exception cref="Exceptions.AuthenticationFailedException">Thrown when the refresh token is invalid.</exception>
        /// <exception cref="Exceptions.SessionNotAuthenticatedException">Thrown when there's no OAuth token, or the OAuth token has expired and there is no valid refresh token.</exception>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationIncorrectException">Thrown when the web server indicates the two-factor code was incorrect (HTTP 400).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationRequiredException">Thrown when the web server indicates two-factor authentication is required (HTTP 412).</exception>
        public async Task<List<DoorbotHistoryEvent>> GetDoorbotsHistory(int? limit = null)
        {
            return await GetDoorbotsHistory(null, limit);
        }

        /// <summary>
        /// Returns all events registered for the doorbots that happened between the provided dates. Notice: Ring does not provide an API which allows for retrieving items between two specific dates. This means that this code will just keep retriving historical items until it has all items that occurred in the provided date span. This is not super efficient, but unfortunately the only way.
        /// </summary>
        /// <param name="startDate">Date and time in the past from where to start collecting history</param>
        /// <param name="endDate">Date and time in the past until where to start collecting history. Provide NULL to get everything up till now.</param>
        /// <param name="doorbotId">Id of the doorbot to retrieve the history for. Provide NULL to retrieve the history for all available doorbots.</param>
        /// <returns>All events triggered by registered doorbots under the current account between the provided dates</returns>
        /// <exception cref="Exceptions.AuthenticationFailedException">Thrown when the refresh token is invalid.</exception>
        /// <exception cref="Exceptions.SessionNotAuthenticatedException">Thrown when there's no OAuth token, or the OAuth token has expired and there is no valid refresh token.</exception>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationIncorrectException">Thrown when the web server indicates the two-factor code was incorrect (HTTP 400).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationRequiredException">Thrown when the web server indicates two-factor authentication is required (HTTP 412).</exception>
        /// <exception cref="Exceptions.DeviceUnknownException">Thrown when the web server indicates the requested Ring device was not found (HTTP 404).</exception>
        public async Task<List<Entities.DoorbotHistoryEvent>> GetDoorbotsHistory(DateTime startDate, DateTime? endDate, long? doorbotId = null)
        {
            await EnsureSessionValid();

            // Amount of items to retrieve in each request
            const short batchWithItems = 200;

            // Create a list to hold all the results
            var allHistory = new List<DoorbotHistoryEvent>();
            var doorbotHistory = new List<DoorbotHistoryEvent>();
            DateTime? lastItemDateTime = null;

            do
            {
                // Retrieve a batch with historical items
                var response = await _httpUtility.GetContents(new Uri(BaseUrl, $"doorbots/{(doorbotId.HasValue ? $"{doorbotId.Value}/" : "")}history?limit={batchWithItems}{(doorbotHistory.Count == 0 ? "" : "&older_than=" + doorbotHistory.Last().Id)}"), AuthenticationToken, _hardwareId);

                // Parse the result
                doorbotHistory = JsonSerializer.Deserialize<List<DoorbotHistoryEvent>>(response);

                // Add this next batch to the list with all the results which fit within the provided date span
                allHistory.AddRange(doorbotHistory.Where(h => h.CreatedAtDateTime.HasValue && h.CreatedAtDateTime.Value >= startDate && (!endDate.HasValue || h.CreatedAtDateTime.Value <= endDate.Value)));

                if (doorbotHistory.Count > 0)
                {
                    lastItemDateTime = doorbotHistory[doorbotHistory.Count - 1]?.CreatedAtDateTime ?? DateTime.MinValue;
                }
            }
            // Keep retrieving next batches until the last item in the retrieved batch does not fit within the request date span anymore
            while (doorbotHistory.Count > 0 && lastItemDateTime.HasValue && lastItemDateTime.Value > startDate);

            ApiRawLogger.LogEvent("History", $"Retrieved {allHistory.Count} events" + (doorbotId.HasValue ? $" for doorbot {doorbotId}" : " for all doorbots") + $" between {startDate:o} and {(endDate.HasValue ? endDate.Value.ToString("o") : "now")}");
            ApiRawLogger.LogRingEvents("History", $"{allHistory.Count} events" + (doorbotId.HasValue ? $" for doorbot {doorbotId}" : ""), JsonSerializer.Serialize(allHistory));

            return allHistory;
        }

        /// <summary>
        /// Returns a stream with the recording of the provided Ding Id of a doorbot
        /// </summary>
        /// <param name="doorbotHistoryEvent">The doorbot history event to retrieve the recording for</param>
        /// <returns>Stream containing contents of the recording</returns>
        /// <exception cref="Exceptions.AuthenticationFailedException">Thrown when the refresh token is invalid.</exception>
        /// <exception cref="Exceptions.DownloadFailedException">Thrown when a download URL could not be created.</exception>
        /// <exception cref="Exceptions.SessionNotAuthenticatedException">Thrown when there's no OAuth token, or the OAuth token has expired and there is no valid refresh token.</exception>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationIncorrectException">Thrown when the web server indicates the two-factor code was incorrect (HTTP 400).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationRequiredException">Thrown when the web server indicates two-factor authentication is required (HTTP 412).</exception>
        /// <exception cref="ArgumentNullException">Thrown when no historyEvent provided or one provided without an Id</exception>
        public async Task<Stream> GetDoorbotHistoryRecording(DoorbotHistoryEvent doorbotHistoryEvent)
        {
            if (doorbotHistoryEvent == null || !doorbotHistoryEvent.Id.HasValue)
            {
                throw new ArgumentNullException(nameof(doorbotHistoryEvent));
            }

            return await GetDoorbotHistoryRecording(doorbotHistoryEvent.Id.Value.ToString());
        }

        /// <summary>
        /// Returns a stream with the recording of the provided Ding Id of a doorbot
        /// </summary>
        /// <param name="dingId">Id of the doorbot history event to retrieve the recording for</param>
        /// <returns>Stream containing contents of the recording</returns>
        /// <exception cref="Exceptions.AuthenticationFailedException">Thrown when the refresh token is invalid.</exception>
        /// <exception cref="Exceptions.DownloadFailedException">Thrown when a download URL could not be created.</exception>
        /// <exception cref="Exceptions.SessionNotAuthenticatedException">Thrown when there's no OAuth token, or the OAuth token has expired and there is no valid refresh token.</exception>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationIncorrectException">Thrown when the web server indicates the two-factor code was incorrect (HTTP 400).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationRequiredException">Thrown when the web server indicates two-factor authentication is required (HTTP 412).</exception>
        public async Task<Stream> GetDoorbotHistoryRecording(string dingId)
        {
            var downloadResult = await GetDoorbotHistoryRecordingInfo(dingId);
            return await _videoDownloader.OpenStreamAsync(downloadResult.Url);
        }


        /// <summary>
        /// Saves the recording of the provided Ding Id of a doorbot to the provided location
        /// </summary>
        /// <param name="doorbotHistoryEvent">The doorbot history event to retrieve the recording for</param>
        /// <param name="saveAs">Full path including the filename where to save the recording</param>
        /// <exception cref="Exceptions.AuthenticationFailedException">Thrown when the refresh token is invalid.</exception>
        /// <exception cref="Exceptions.DownloadFailedException">Thrown when a download URL could not be created.</exception>
        /// <exception cref="Exceptions.SessionNotAuthenticatedException">Thrown when there's no OAuth token, or the OAuth token has expired and there is no valid refresh token.</exception>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationIncorrectException">Thrown when the web server indicates the two-factor code was incorrect (HTTP 400).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationRequiredException">Thrown when the web server indicates two-factor authentication is required (HTTP 412).</exception>
        /// <exception cref="ArgumentNullException">Thrown when no historyEvent provided or one provided without an Id</exception>
        public async Task GetDoorbotHistoryRecording(DoorbotHistoryEvent doorbotHistoryEvent, string saveAs)
        {
            if (doorbotHistoryEvent == null || !doorbotHistoryEvent.Id.HasValue)
            {
                throw new ArgumentNullException(nameof(doorbotHistoryEvent));
            }

            await GetDoorbotHistoryRecording(doorbotHistoryEvent.Id.Value.ToString(), saveAs);
        }

        /// <summary>
        /// Saves the recording of the provided Ding Id of a doorbot to the provided location
        /// </summary>
        /// <param name="dingId">Id of the doorbot history event to retrieve the recording for</param>
        /// <param name="saveAs">Full path including the filename where to save the recording</param>
        /// <exception cref="Exceptions.AuthenticationFailedException">Thrown when the refresh token is invalid.</exception>
        /// <exception cref="Exceptions.DownloadFailedException">Thrown when a download URL could not be created.</exception>
        /// <exception cref="Exceptions.SessionNotAuthenticatedException">Thrown when there's no OAuth token, or the OAuth token has expired and there is no valid refresh token.</exception>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationIncorrectException">Thrown when the web server indicates the two-factor code was incorrect (HTTP 400).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationRequiredException">Thrown when the web server indicates two-factor authentication is required (HTTP 412).</exception>
        public async Task GetDoorbotHistoryRecording(string dingId, string saveAs)
        {
            var downloadResult = await GetDoorbotHistoryRecordingInfo(dingId);
            await GetDoorbotHistoryRecording(downloadResult, saveAs);
        }

        /// <summary>
        /// Requests the download URL (and any metadata, such as file size, the Ring service provides
        /// alongside it) for the recording of the provided doorbot history event, without downloading
        /// the file itself. Callers can use this to decide whether the file is worth downloading (e.g.
        /// comparing against an already-downloaded file) before spending bandwidth on the actual bytes.
        /// </summary>
        /// <param name="doorbotHistoryEvent">The doorbot history event to retrieve the recording info for</param>
        /// <exception cref="Exceptions.AuthenticationFailedException">Thrown when the refresh token is invalid.</exception>
        /// <exception cref="Exceptions.DownloadFailedException">Thrown when a download URL could not be created.</exception>
        /// <exception cref="Exceptions.SessionNotAuthenticatedException">Thrown when there's no OAuth token, or the OAuth token has expired and there is no valid refresh token.</exception>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationIncorrectException">Thrown when the web server indicates the two-factor code was incorrect (HTTP 400).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationRequiredException">Thrown when the web server indicates two-factor authentication is required (HTTP 412).</exception>
        /// <exception cref="ArgumentNullException">Thrown when no historyEvent provided or one provided without an Id</exception>
        public async Task<Entities.DownloadRecording> GetDoorbotHistoryRecordingInfo(DoorbotHistoryEvent doorbotHistoryEvent)
        {
            if (doorbotHistoryEvent == null || !doorbotHistoryEvent.Id.HasValue)
            {
                throw new ArgumentNullException(nameof(doorbotHistoryEvent));
            }

            return await GetDoorbotHistoryRecordingInfo(doorbotHistoryEvent.Id.Value.ToString());
        }

        /// <summary>
        /// Requests the download URL (and any metadata, such as file size, the Ring service provides
        /// alongside it) for the recording of the provided Ding Id, without downloading the file itself.
        /// </summary>
        /// <param name="dingId">Id of the doorbot history event to retrieve the recording info for</param>
        /// <exception cref="Exceptions.AuthenticationFailedException">Thrown when the refresh token is invalid.</exception>
        /// <exception cref="Exceptions.DownloadFailedException">Thrown when a download URL could not be created.</exception>
        /// <exception cref="Exceptions.SessionNotAuthenticatedException">Thrown when there's no OAuth token, or the OAuth token has expired and there is no valid refresh token.</exception>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationIncorrectException">Thrown when the web server indicates the two-factor code was incorrect (HTTP 400).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationRequiredException">Thrown when the web server indicates two-factor authentication is required (HTTP 412).</exception>
        public async Task<Entities.DownloadRecording> GetDoorbotHistoryRecordingInfo(string dingId)
        {
            await EnsureSessionValid();

            // Construct the URL where to request downloading of a recording
            var downloadRequestUri = new Uri(BaseUrl, $"dings/{dingId}/share/download?disable_redirect=true");

            Entities.DownloadRecording downloadResult = null;
            // Ring returns an empty URL while it prepares the download server-side, but also returns
            // an empty URL indefinitely for recordings that have expired/aren't retained anymore —
            // those two cases are indistinguishable from the response alone. Capped at 5 attempts
            // (~8s worst case) rather than the original 59 (~118s) so that a bulk download across many
            // events doesn't stall for minutes per expired recording; a genuinely-preparing recording
            // that isn't ready within ~8s is rare enough that this is the better tradeoff for callers
            // downloading many events at once.
            for (var downloadAttempt = 1; downloadAttempt < 6; downloadAttempt++)
            {
                // Request to download the recording
                var response = await _httpUtility.GetContents(downloadRequestUri, AuthenticationToken, _hardwareId);

                // Parse the result
                downloadResult = JsonSerializer.Deserialize<DownloadRecording>(response);

                // If the Ring API returns an empty URL property, it means it's still preparing the download on the server side. Just keep requesting the recording until it returns a URL.
                if (!string.IsNullOrWhiteSpace(downloadResult.Url))
                {
                    // URL returned is not empty, start the download from the returned URL
                    break;
                }

                // Wait two seconds before requesting the recording again
                await Task.Delay(TimeSpan.FromSeconds(2));
            }

            // Ensure we ended with a valid URL to download the recording from
            if (downloadResult == null || string.IsNullOrWhiteSpace(downloadResult.Url) || !Uri.TryCreate(downloadResult.Url, UriKind.Absolute, out _))
            {
                throw new Exceptions.DownloadFailedException(downloadResult?.Url ?? "(no URL was created)");
            }

            return downloadResult;
        }

        /// <summary>
        /// Downloads the recording bytes from a previously-obtained <see cref="Entities.DownloadRecording"/>
        /// (see <see cref="GetDoorbotHistoryRecordingInfo(string)"/>) and saves them to the provided location.
        /// </summary>
        /// <param name="downloadInfo">The download info (URL, and any metadata) previously obtained for this recording</param>
        /// <param name="saveAs">Full path including the filename where to save the recording</param>
        /// <exception cref="Exceptions.DownloadFailedException">Thrown when the download info does not contain a valid URL.</exception>
        public async Task GetDoorbotHistoryRecording(Entities.DownloadRecording downloadInfo, string saveAs)
        {
            if (downloadInfo == null || string.IsNullOrWhiteSpace(downloadInfo.Url) || !Uri.TryCreate(downloadInfo.Url, UriKind.Absolute, out _))
            {
                throw new Exceptions.DownloadFailedException(downloadInfo?.Url ?? "(no URL was created)");
            }

            await _videoDownloader.DownloadToFileAsync(downloadInfo.Url, saveAs);
        }

        /// <summary>
        /// Shares the Ring recording with the provided identifier and returns the shared URL to it
        /// </summary>
        /// <param name="historyEvent">The doorbot history event to share the recording of</param>
        /// <returns>Uri to the shared recording</returns>
        /// <exception cref="Exceptions.AuthenticationFailedException">Thrown when the refresh token is invalid.</exception>
        /// <exception cref="Exceptions.DownloadFailedException">Thrown when a download URL could not be created.</exception>
        /// <exception cref="Exceptions.SessionNotAuthenticatedException">Thrown when there's no OAuth token, or the OAuth token has expired and there is no valid refresh token.</exception>
        /// <exception cref="Exceptions.SharingFailedException">Thrown when a share URL could not be created.</exception>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationIncorrectException">Thrown when the web server indicates the two-factor code was incorrect (HTTP 400).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationRequiredException">Thrown when the web server indicates two-factor authentication is required (HTTP 412).</exception>
        /// <exception cref="ArgumentNullException">Thrown when no historyEvent provided or one provided without an Id</exception>
        public async Task<Uri> ShareRecording(DoorbotHistoryEvent historyEvent)
        {
            if (historyEvent == null || !historyEvent.Id.HasValue)
            {
                throw new ArgumentNullException(nameof(historyEvent));
            }

            return await ShareRecording(historyEvent.Id.Value.ToString());
        }

        /// <summary>
        /// Shares the Ring recording with the provided identifier and returns the shared URL to it
        /// </summary>
        /// <param name="recordingId">Id of the recording</param>
        /// <returns>Uri to the shared recording</returns>
        /// <exception cref="Exceptions.AuthenticationFailedException">Thrown when the refresh token is invalid.</exception>
        /// <exception cref="Exceptions.SessionNotAuthenticatedException">Thrown when there's no OAuth token, or the OAuth token has expired and there is no valid refresh token.</exception>
        /// <exception cref="Exceptions.SharingFailedException">Thrown when a share URL could not be created.</exception>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationIncorrectException">Thrown when the web server indicates the two-factor code was incorrect (HTTP 400).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationRequiredException">Thrown when the web server indicates two-factor authentication is required (HTTP 412).</exception>
        public async Task<Uri> ShareRecording(string recordingId)
        {
            await EnsureSessionValid();

            // Construct the URL where to request sharing of a recording
            var downloadRequestUri = new Uri(BaseUrl, $"dings/{recordingId}/share/share?disable_redirect=true");

            Entities.SharedRecording shareResult = null;
            for (var downloadAttempt = 1; downloadAttempt < 60; downloadAttempt++)
            {
                // Request to share the recording
                var response = await _httpUtility.GetContents(downloadRequestUri, AuthenticationToken, _hardwareId);

                // Parse the result
                shareResult = JsonSerializer.Deserialize<SharedRecording>(response);

                // If the Ring API returns an empty URL property, it means its still preparing the sharing on the server side. Just keep requesting the recording until it returns an URL.
                if (!string.IsNullOrWhiteSpace(shareResult.WrapperUrl))
                {
                    // URL returned is not empty, return the URL
                    break;
                }

                // Wait 2 seconds before requesting the sharing again
                await Task.Delay(TimeSpan.FromSeconds(2));
            }

            // Ensure we ended with a valid URL to the shared recording
            if (shareResult == null || string.IsNullOrWhiteSpace(shareResult.WrapperUrl) || !Uri.TryCreate(shareResult.WrapperUrl, UriKind.Absolute, out Uri shareUri))
            {
                throw new Exceptions.SharingFailedException(recordingId);
            }

            return shareUri;
        }

        /// <summary>
        /// Saves the latest available snapshot from the provided doorbot to the provided location
        /// </summary>
        /// <param name="doorbot">The doorbot to retrieve the latest available snapshot from</param>
        /// <param name="saveAs">Full path including the filename where to save the snapshot</param>
        /// <exception cref="Exceptions.AuthenticationFailedException">Thrown when the refresh token is invalid.</exception>
        /// <exception cref="Exceptions.DownloadFailedException">Thrown when a download URL could not be created.</exception>
        /// <exception cref="Exceptions.SessionNotAuthenticatedException">Thrown when there's no OAuth token, or the OAuth token has expired and there is no valid refresh token.</exception>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationIncorrectException">Thrown when the web server indicates the two-factor code was incorrect (HTTP 400).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationRequiredException">Thrown when the web server indicates two-factor authentication is required (HTTP 412).</exception>
        public async Task GetLatestSnapshot(Doorbot doorbot, string saveAs)
        {
            await GetLatestSnapshot(doorbot.Id, saveAs);
        }


        /// <summary>
        /// Saves the latest available snapshot from the provided doorbot to the provided location
        /// </summary>
        /// <param name="doorbotId">ID of the doorbot to retrieve the latest available snapshot from</param>
        /// <param name="saveAs">Full path including the filename where to save the snapshot</param>
        /// <exception cref="Exceptions.AuthenticationFailedException">Thrown when the refresh token is invalid.</exception>
        /// <exception cref="Exceptions.DownloadFailedException">Thrown when a download URL could not be created.</exception>
        /// <exception cref="Exceptions.SessionNotAuthenticatedException">Thrown when there's no OAuth token, or the OAuth token has expired and there is no valid refresh token.</exception>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationIncorrectException">Thrown when the web server indicates the two-factor code was incorrect (HTTP 400).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationRequiredException">Thrown when the web server indicates two-factor authentication is required (HTTP 412).</exception>
        public async Task GetLatestSnapshot(int doorbotId, string saveAs)
        {
            using var stream = await GetLatestSnapshot(doorbotId);
            using var fileStream = File.Create(saveAs);

            stream.Seek(0, SeekOrigin.Begin);
            await stream.CopyToAsync(fileStream);
        }

        /// <summary>
        /// Returns the latest available snapshot from the provided doorbot
        /// </summary>
        /// <param name="doorbot">The doorbot to retrieve the latest available snapshot from</param>
        /// <returns>Stream with the latest snapshot from the doorbot</returns>
        /// <exception cref="Exceptions.AuthenticationFailedException">Thrown when the refresh token is invalid.</exception>
        /// <exception cref="Exceptions.DownloadFailedException">Thrown when a download URL could not be created.</exception>
        /// <exception cref="Exceptions.SessionNotAuthenticatedException">Thrown when there's no OAuth token, or the OAuth token has expired and there is no valid refresh token.</exception>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationIncorrectException">Thrown when the web server indicates the two-factor code was incorrect (HTTP 400).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationRequiredException">Thrown when the web server indicates two-factor authentication is required (HTTP 412).</exception>
        public async Task<Stream> GetLatestSnapshot(Doorbot doorbot)
        {
            return await GetLatestSnapshot(doorbot.Id);
        }

        /// <summary>
        /// Returns the latest available snapshot from the provided doorbot
        /// </summary>
        /// <param name="doorbotId">ID of the doorbot to retrieve the latest available snapshot from</param>
        /// <returns>Stream with the latest snapshot from the doorbot</returns>
        /// <exception cref="Exceptions.AuthenticationFailedException">Thrown when the refresh token is invalid.</exception>
        /// <exception cref="Exceptions.DownloadFailedException">Thrown when a download URL could not be created.</exception>
        /// <exception cref="Exceptions.SessionNotAuthenticatedException">Thrown when there's no OAuth token, or the OAuth token has expired and there is no valid refresh token.</exception>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationIncorrectException">Thrown when the web server indicates the two-factor code was incorrect (HTTP 400).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationRequiredException">Thrown when the web server indicates two-factor authentication is required (HTTP 412).</exception>
        public async Task<Stream> GetLatestSnapshot(int doorbotId)
        {
            await EnsureSessionValid();

            // Construct the URL where to download the latest doorbot snapshot from
            var downloadSnapshotUri = new Uri(BaseUrl, $"snapshots/image/{doorbotId}");

            // Request the snapshot
            var bytes = await _httpUtility.DownloadFile(downloadSnapshotUri, AuthenticationToken);
            return new MemoryStream(bytes);
        }

        /// <summary>
        /// Requests the Ring API to get a fresh snapshot from the provided doorbot
        /// </summary>
        /// <param name="doorbot">The doorbot to request a fresh snapshot from</param>
        /// <exception cref="Exceptions.AuthenticationFailedException">Thrown when the refresh token is invalid.</exception>
        /// <exception cref="Exceptions.DownloadFailedException">Thrown when a download URL could not be created.</exception>
        /// <exception cref="Exceptions.SessionNotAuthenticatedException">Thrown when there's no OAuth token, or the OAuth token has expired and there is no valid refresh token.</exception>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationIncorrectException">Thrown when the web server indicates the two-factor code was incorrect (HTTP 400).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationRequiredException">Thrown when the web server indicates two-factor authentication is required (HTTP 412).</exception>
        /// <exception cref="Exceptions.UnexpectedOutcomeException">Thrown if the actual HTTP response is different from what was expected</exception>
        public async Task UpdateSnapshot(Doorbot doorbot)
        {
            await UpdateSnapshot(doorbot.Id);
        }

        /// <summary>
        /// Requests the Ring API to get a fresh snapshot from the provided doorbot
        /// </summary>
        /// <param name="doorbotId">ID of the doorbot to request a fresh snapshot from</param>
        /// <exception cref="Exceptions.AuthenticationFailedException">Thrown when the refresh token is invalid.</exception>
        /// <exception cref="Exceptions.DownloadFailedException">Thrown when a download URL could not be created.</exception>
        /// <exception cref="Exceptions.SessionNotAuthenticatedException">Thrown when there's no OAuth token, or the OAuth token has expired and there is no valid refresh token.</exception>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationIncorrectException">Thrown when the web server indicates the two-factor code was incorrect (HTTP 400).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationRequiredException">Thrown when the web server indicates two-factor authentication is required (HTTP 412).</exception>
        /// <exception cref="Exceptions.UnexpectedOutcomeException">Thrown if the actual HTTP response is different from what was expected</exception>
        public async Task UpdateSnapshot(int doorbotId)
        {
            await EnsureSessionValid();

            // Construct the URL which will trigger the Ring API to refresh the snapshots
            var updateSnapshotUri = new Uri(BaseUrl, "snapshots/update_all");

            // Construct the body of the message
            var bodyContent = string.Concat(@"{ ""doorbot_ids"": [", doorbotId, @"], ""refresh"": true }");

            // Send the request
            await _httpUtility.SendRequestWithExpectedStatusOutcome(updateSnapshotUri, System.Net.Http.HttpMethod.Put, System.Net.HttpStatusCode.NoContent, bodyContent, AuthenticationToken);
        }

        /// <summary>
        /// Request the date and time when the last snapshot was taken from the provided doorbot
        /// </summary>
        /// <param name="doorbot">The doorbot to request when the last snapshot was taken from</param>
        /// <returns>Entity with information regarding the last taken snapshot</returns>
        /// <exception cref="Exceptions.SessionNotAuthenticatedException">Thrown when there's no OAuth token, or the OAuth token has expired and there is no valid refresh token.</exception>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationIncorrectException">Thrown when the web server indicates the two-factor code was incorrect (HTTP 400).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationRequiredException">Thrown when the web server indicates two-factor authentication is required (HTTP 412).</exception>
        public async Task<DoorbotTimestamps> GetDoorbotSnapshotTimestamp(Doorbot doorbot)
        {
            return await GetDoorbotSnapshotTimestamp(doorbot.Id);
        }

        /// <summary>
        /// Request the date and time when the last snapshot was taken from the provided doorbot
        /// </summary>
        /// <param name="doorbotId">ID of the doorbot to request when the last snapshot was taken from</param>
        /// <returns>Entity with information regarding the last taken snapshot</returns>
        /// <exception cref="Exceptions.AuthenticationFailedException">Thrown when the refresh token is invalid.</exception>
        /// <exception cref="Exceptions.SessionNotAuthenticatedException">Thrown when there's no OAuth token, or the OAuth token has expired and there is no valid refresh token.</exception>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationIncorrectException">Thrown when the web server indicates the two-factor code was incorrect (HTTP 400).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationRequiredException">Thrown when the web server indicates two-factor authentication is required (HTTP 412).</exception>
        public async Task<DoorbotTimestamps> GetDoorbotSnapshotTimestamp(int doorbotId)
        {
            await EnsureSessionValid();

            // Construct the URL which will request the timestamps of the latest snapshots
            var updateSnapshotUri = new Uri(BaseUrl, "snapshots/timestamps");

            // Construct the body of the message
            var bodyContent = string.Concat(@"{ ""doorbot_ids"": [", doorbotId, @"]}");

            // Send the request
            var doorbotTimestamps = await _httpUtility.SendRequest<DoorbotTimestamps>(updateSnapshotUri, System.Net.Http.HttpMethod.Post, bodyContent, AuthenticationToken);
            return doorbotTimestamps;
        }

        #endregion
    }
}
