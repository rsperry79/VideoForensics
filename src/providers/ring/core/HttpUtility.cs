using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Collections.Specialized;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using VideoForensics.Providers.Common.Helpers.Platform;

namespace VideoForensics.Providers.Ring
{
    /// <summary>
    /// Internal utility class for Http communication with the Ring API
    /// </summary>
    internal class HttpUtility
    {
        #region Fields

        /// <summary>
        /// Keep one reusable instance of a HttpClient to avoid port exhaustion
        /// </summary>
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Cookiecontainer to keep the cookies needed for the requests
        /// </summary>
        private readonly CookieContainer _cookieContainer;

        /// <summary>
        /// HttpClientHandler to use for the HttpClient requests
        /// </summary>
        private readonly HttpClientHandler _httpClientHandler;

        // Shared across every HttpUtility instance/Session in the process: once Ring returns a 429,
        // every caller (across all devices being pre-scanned/downloaded) needs to stop hitting the
        // API until the ban clears, not just the one call that got throttled. Without this, each
        // device's own independent per-call retry loop kept probing every few seconds, which - based
        // on observed behavior - looks like it resets/extends Ring's real punishment window rather
        // than waiting it out, so the account stayed throttled indefinitely across an entire batch.
        private static readonly object _throttleLock = new();
        private static DateTime? _throttledUntilUtc;
        private static int _consecutiveThrottles;
        private static readonly TimeSpan BaseThrottleCooldown = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan MaxThrottleCooldown = TimeSpan.FromMinutes(5);

        // If Ring is still 429ing after we've already waited out several escalating cooldowns, the
        // account is under a real ban well beyond what a short client-side backoff can wait through -
        // observed in practice as 429s recurring every 5 minutes for 30+ minutes straight. Continuing
        // to probe every few minutes at that point doesn't recover faster and may be exactly what's
        // resetting Ring's own punishment timer. Past this many consecutive throttles, stop sending
        // requests entirely for a long fixed cooldown instead of retrying - every call fails
        // immediately (no network round-trip at all) until it elapses.
        private const int HardBanThreshold = 3;
        private static readonly TimeSpan HardBanCooldown = TimeSpan.FromHours(1);
        private static DateTime? _hardBanUntilUtc;

        // The hard-ban timestamp is persisted to disk (not just kept in memory) because the actual
        // incident that motivated this was the user repeatedly closing and relaunching the app while
        // throttled: a fresh process has no memory of an in-progress ban, so it started probing Ring
        // again on every relaunch - almost certainly what kept the account locked out for 30+ minutes
        // straight instead of the ban ever getting a real, uninterrupted chance to expire.
        private static bool _hardBanStateLoaded;
        private static string HardBanStateFilePath => Path.Combine(new PlatformDirectoryService().GetApplicationDataDirectory(), "ring_hard_ban.txt");

        private static void EnsureHardBanStateLoaded()
        {
            if (_hardBanStateLoaded)
            {
                return;
            }

            lock (_throttleLock)
            {
                if (_hardBanStateLoaded)
                {
                    return;
                }

                try
                {
                    if (File.Exists(HardBanStateFilePath) &&
                        long.TryParse(File.ReadAllText(HardBanStateFilePath).Trim(), out var ticks))
                    {
                        var persisted = new DateTime(ticks, DateTimeKind.Utc);
                        if (persisted > DateTime.UtcNow)
                        {
                            _hardBanUntilUtc = persisted;
                        }
                    }
                }
                catch { }

                _hardBanStateLoaded = true;
            }
        }

        private static void PersistHardBanState()
        {
            try
            {
                var folder = Path.GetDirectoryName(HardBanStateFilePath);
                if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                if (_hardBanUntilUtc.HasValue)
                {
                    File.WriteAllText(HardBanStateFilePath, _hardBanUntilUtc.Value.Ticks.ToString());
                }
                else if (File.Exists(HardBanStateFilePath))
                {
                    File.Delete(HardBanStateFilePath);
                }
            }
            catch { }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new HttpUtility helper. Cookies will be shared among all requests done in this instance.
        /// </summary>
        /// <param name="timeout">Default timeout in milliseconds to apply to the HTTP requests</param>
        /// <param name="messageHandler">Optional custom HttpMessageHandler for testing. If not provided, HttpClientHandler will be used.</param>
        public HttpUtility(int timeout = 60000, HttpMessageHandler messageHandler = null)
        {
            if (messageHandler != null)
            {
                _httpClientHandler = messageHandler as HttpClientHandler;
                _cookieContainer = _httpClientHandler?.CookieContainer ?? new CookieContainer();
            }
            else
            {
                _cookieContainer = new CookieContainer();
                _httpClientHandler = new HttpClientHandler { CookieContainer = _cookieContainer };
                messageHandler = _httpClientHandler;
            }

            _httpClient = new(messageHandler);
            _httpClient.Timeout = TimeSpan.FromMilliseconds(timeout);
        }

        #endregion

        #region Descructors

        /// <summary>
        /// Clean up resources
        /// </summary>
        ~HttpUtility()
        {
            _httpClientHandler?.Dispose();
            _httpClient?.Dispose();
        }

        #endregion

        /// <summary>
        /// Performs a GET request to the provided url to return the contents
        /// </summary>
        /// <param name="url">Url of the request to make</param>
        /// <param name="bearerToken">Bearer token to authenticate the request with. Leave out to not authenticate the session.</param>
        /// <returns>Contents of the result returned by the webserver</returns>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        public async Task<string> GetContents(Uri url, string bearerToken = null, string hardwareId = null, CancellationToken cancellationToken = default)
        {
            ThrowIfHardBanned();
            await WaitOutActiveThrottleAsync(cancellationToken);

            // Construct the request
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = url
            };

            // Check if the OAuth Bearer Authorization token should be added to the request
            if (!string.IsNullOrEmpty(bearerToken))
            {
                request.Headers.Add(HttpRequestHeader.Authorization.ToString(), $"Bearer {bearerToken}");
            }

            request.Headers.TryAddWithoutValidation("User-Agent", "android:com.ringapp");

            if (!string.IsNullOrEmpty(hardwareId))
            {
                request.Headers.Add("hardware_id", hardwareId);
            }

            // Send the request to the webserver
            var response = await _httpClient.SendAsync(request, cancellationToken);

            // Read the body up front (even on error responses) so it can be captured for diagnostics
            // before we potentially throw below.
            var responseFromServer = await response.Content.ReadAsStringAsync(cancellationToken);
            ApiRawLogger.Raise("GET", url.ToString(), (int)response.StatusCode, responseFromServer);

            switch (response.StatusCode)
            {
                case HttpStatusCode.TooManyRequests:
                    RecordThrottled();
                    throw new Exceptions.ThrottledException();

                case HttpStatusCode.NotFound:
                    throw new Exceptions.DeviceUnknownException(url);
            }

            // A non-2xx response here (e.g. 401 from an expired token, or 403 from a scope the
            // legacy auth flow doesn't grant on newer endpoints like devices/v1/locations) was
            // previously deserialized as if it were a successful empty payload, silently turning
            // into "no devices/locations found" instead of a surfaced auth error.
            if (!response.IsSuccessStatusCode)
            {
                throw new Exceptions.UnexpectedOutcomeException(response.StatusCode);
            }

            ClearThrottle();
            return responseFromServer;
        }

        /// <summary>
        /// Fails immediately, with no network round-trip, while a hard ban cooldown is active - see
        /// HardBanThreshold. Checked before even waiting out the regular per-throttle cooldown below.
        /// </summary>
        private static void ThrowIfHardBanned()
        {
            EnsureHardBanStateLoaded();

            DateTime? hardBanUntil;
            lock (_throttleLock)
            {
                hardBanUntil = _hardBanUntilUtc;
            }

            if (hardBanUntil.HasValue)
            {
                if (DateTime.UtcNow < hardBanUntil.Value)
                {
                    var remaining = hardBanUntil.Value - DateTime.UtcNow;
                    throw new Exceptions.ThrottledException(
                        $"Ring has rate-limited this account for an extended period (this persists across app restarts). Stopping all requests until {hardBanUntil.Value.ToLocalTime():t} local time (about {remaining.TotalMinutes:F0} more minute(s)) rather than continuing to retry.");
                }

                lock (_throttleLock)
                {
                    _hardBanUntilUtc = null;
                    _consecutiveThrottles = 0;
                }
                PersistHardBanState();
            }
        }

        /// <summary>
        /// Blocks until any in-progress process-wide throttle cooldown (set by a prior 429 from any
        /// caller) has elapsed, so a device further along in a batch doesn't immediately re-trigger
        /// the same ban a different device just tripped seconds ago.
        /// </summary>
        private static async Task WaitOutActiveThrottleAsync(CancellationToken cancellationToken)
        {
            TimeSpan remaining;
            lock (_throttleLock)
            {
                remaining = _throttledUntilUtc.HasValue ? _throttledUntilUtc.Value - DateTime.UtcNow : TimeSpan.Zero;
            }

            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining, cancellationToken);
            }
        }

        /// <summary>
        /// Extends the shared throttle cooldown with exponential backoff (30s, 60s, 120s, ... capped
        /// at 5 minutes) so repeated 429s across a batch back off progressively instead of every
        /// caller independently hammering the API every few seconds. Once HardBanThreshold
        /// consecutive throttles have happened - meaning even waiting out that escalating cooldown
        /// isn't getting past Ring's real ban - stop trying entirely for HardBanCooldown.
        /// </summary>
        private static void RecordThrottled()
        {
            var justHardBanned = false;
            lock (_throttleLock)
            {
                _consecutiveThrottles++;

                if (_consecutiveThrottles >= HardBanThreshold)
                {
                    _hardBanUntilUtc = DateTime.UtcNow + HardBanCooldown;
                    _throttledUntilUtc = null;
                    justHardBanned = true;
                }
                else
                {
                    var cooldown = TimeSpan.FromTicks(Math.Min(
                        BaseThrottleCooldown.Ticks * (1L << Math.Min(_consecutiveThrottles - 1, 4)),
                        MaxThrottleCooldown.Ticks));
                    _throttledUntilUtc = DateTime.UtcNow + cooldown;
                }
            }

            if (justHardBanned)
            {
                PersistHardBanState();
            }
        }

        private static void ClearThrottle()
        {
            bool wasHardBanned;
            lock (_throttleLock)
            {
                wasHardBanned = _hardBanUntilUtc.HasValue;
                _consecutiveThrottles = 0;
                _throttledUntilUtc = null;
                _hardBanUntilUtc = null;
            }

            if (wasHardBanned)
            {
                PersistHardBanState();
            }
        }

        /// <summary>
        /// Sends a POST request for OAuth authentication with Basic Auth credentials
        /// </summary>
        /// <param name="url">Url to POST to</param>
        /// <param name="formFields">Dictionary with key/value pairs to send as form-encoded body</param>
        /// <param name="headerFields">NameValueCollection with the fields to add to the header sent to the server with the request</param>
        /// <returns>The website contents returned by the webserver after posting the data</returns>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationIncorrectException">Thrown when the web server indicates the two-factor code was incorrect (HTTP 400).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationRequiredException">Thrown when the web server indicates two-factor authentication is required (HTTP 412).</exception>
        public async Task<string> OAuthPost(Uri url, Dictionary<string, string> formFields, NameValueCollection headerFields)
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = url
            };

            if (headerFields != null)
            {
                foreach (string headerField in headerFields)
                {
                    request.Headers.Add(headerField, headerFields[headerField]);
                }
            }

            request.Headers.TryAddWithoutValidation("User-Agent", "android:com.ringapp");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var json = JsonSerializer.Serialize(formFields);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);

            if (response == null) return null;

            var responseText = await response.Content.ReadAsStringAsync();

            switch (response.StatusCode)
            {
                case HttpStatusCode.BadRequest:
                    if (responseText.Contains("Too many requests", StringComparison.InvariantCultureIgnoreCase))
                    {
                        ApiRawLogger.LogEvent("Auth", "Throttled (HTTP 429/400 too many requests)");
                        throw new Exceptions.ThrottledException();
                    }
                    if (responseText.Contains("Verification Code is invalid or expired", StringComparison.InvariantCultureIgnoreCase))
                    {
                        ApiRawLogger.LogEvent("Auth", "Two-factor code incorrect or expired");
                        throw new Exceptions.TwoFactorAuthenticationIncorrectException();
                    }
                    break;

                case HttpStatusCode.PreconditionFailed:
                    ApiRawLogger.LogEvent("Auth", "Two-factor authentication required");
                    throw new Exceptions.TwoFactorAuthenticationRequiredException();

                case HttpStatusCode.Unauthorized:
                    ApiRawLogger.LogEvent("Auth", "Authentication failed (401 unauthorized)");
                    throw new Exceptions.AuthenticationFailedException();

                default:
                    if (!response.IsSuccessStatusCode)
                    {
                        ApiRawLogger.LogEvent("Auth", $"Authentication failed (HTTP {(int)response.StatusCode})");
                        throw new Exceptions.AuthenticationFailedException($"Ring API returned HTTP {(int)response.StatusCode} ({response.StatusCode}): {responseText}");
                    }
                    break;
            }

            if (responseText == null) return null;
            return responseText;
        }

        /// <summary>
        /// Sends a POST request with a raw JSON body string
        /// </summary>
        public async Task<string> JsonPostRaw(Uri url, string jsonBody, NameValueCollection headerFields)
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = url
            };

            if (headerFields != null)
            {
                foreach (string headerField in headerFields)
                {
                    request.Headers.TryAddWithoutValidation(headerField, headerFields[headerField]);
                }
            }

            request.Headers.TryAddWithoutValidation("User-Agent", "android:com.ringapp");

            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            if (response == null) return null;

            var responseText = await response.Content.ReadAsStringAsync();
            return responseText;
        }

        /// <summary>
        /// Sends a POST request using the url encoded form method
        /// </summary>
        /// <param name="url">Url to POST to</param>
        /// <param name="formFields">Dictonary with key/value pairs containing the forms data to POST to the webserver</param>
        /// <param name="headerFields">NameValueCollection with the fields to add to the header sent to the server with the request</param>
        /// <returns>The website contents returned by the webserver after posting the data</returns>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationIncorrectException">Thrown when the web server indicates the two-factor code was incorrect (HTTP 400).</exception>
        /// <exception cref="Exceptions.TwoFactorAuthenticationRequiredException">Thrown when the web server indicates two-factor authentication is required (HTTP 412).</exception>
        public async Task<string> FormPost(Uri url, Dictionary<string, string> formFields, NameValueCollection headerFields)
        {
            // Construct the POST request which performs the login
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = url
            };

            if (headerFields != null)
            {
                foreach (string headerField in headerFields)
                {
                    request.Headers.Add(headerField, headerFields[headerField]);
                }
            }

            // Always add the User-Agent header
            request.Headers.TryAddWithoutValidation("User-Agent", "android:com.ringapp");

            // Add Accept header for JSON responses
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Set the content for the HTTP request
            request.Content = new FormUrlEncodedContent(formFields);

            // Receive the response from the webserver
            var response = await _httpClient.SendAsync(request);

            // Make sure the webserver has sent a response
            if (response == null) return null;

            // Get the response body
            var responseText = await response.Content.ReadAsStringAsync();

            switch (response.StatusCode)
            {
                case HttpStatusCode.BadRequest:
                    // Check if the response is HTTP 429 Too Many Requests throttling
                    if (responseText.Contains("Too many requests", StringComparison.InvariantCultureIgnoreCase))
                    {
                        throw new Exceptions.ThrottledException();
                    }

                    // Check if the two factor authentication token was incorrect or has expired. HTTP 400 Bad Request.
                    if (responseText.Contains("Verification Code is invalid or expired", StringComparison.InvariantCultureIgnoreCase))
                    {
                        throw new Exceptions.TwoFactorAuthenticationIncorrectException();
                    }
                    break;

                case HttpStatusCode.PreconditionFailed:
                    // Multi factor authentication failed
                    throw new Exceptions.TwoFactorAuthenticationRequiredException();

                case HttpStatusCode.Unauthorized:
                    throw new Exceptions.AuthenticationFailedException();

                default:
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new Exceptions.AuthenticationFailedException($"Ring API returned HTTP {(int)response.StatusCode} ({response.StatusCode}): {responseText}");
                    }
                    break;
            }

            // Make sure the response content is available
            if (responseText == null) return null;
            return responseText;
        }

        /// <summary>
        /// Downloads the file from the provided Url
        /// </summary>
        /// <param name="url">Url to download the file from</param>
        /// <param name="bearerToken">Bearer token to authenticate the request with. Leave out to not authenticate the session.</param>
        /// <returns>Byte array with the file download</returns>
        public async Task<byte[]> DownloadFile(Uri url, string bearerToken = null, CancellationToken cancellationToken = default)
        {
            // Construct the request
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = url,
                Headers =
                {
                    { HttpRequestHeader.Accept.ToString(), "*/*" },
                    //{ HttpRequestHeader.Range.ToString(), "bytes 0" }
                }
            };

            request.Headers.Range = new RangeHeaderValue(0, null);

            // Check if the OAuth Bearer Authorization token should be added to the request
            if (!string.IsNullOrEmpty(bearerToken))
            {
                request.Headers.Add(HttpRequestHeader.Authorization.ToString(), $"Bearer {bearerToken}");
            }

            // Receive the response from the webserver
            using (var response = await _httpClient.SendAsync(request, cancellationToken))
            {
                var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                ApiRawLogger.Raise("GET", url.ToString(), (int)response.StatusCode, $"<binary content, {bytes.Length} bytes>");
                return bytes;
            }
        }

        /// <summary>
        /// Performs a HTTP request expecting a certain status code to be returned by the server
        /// </summary>
        /// <param name="url">Url of the request to make</param>
        /// <param name="httpMethod">The HTTP method to use to call the provided Url</param>
        /// <param name="expectedStatusCode">The expected HTTP status code to be replied by the Ring API. An exception will be thrown if the expectation was wrong. Leave NULL to just require any success (2xx) status rather than an exact match.</param>
        /// <param name="bodyContent">Content to send along with the request in the body. Leave NULL to not send along any content.</param>
        /// <param name="bearerToken">Bearer token to authenticate the request with. Leave out to not authenticate the session.</param>
        /// <exception cref="Exceptions.UnexpectedOutcomeException">Thrown if the actual HTTP response is different from what was expected (or, with no specific expectation, wasn't a success)</exception>
        public async Task SendRequestWithExpectedStatusOutcome(Uri url, HttpMethod httpMethod, HttpStatusCode? expectedStatusCode, string bodyContent = null, string bearerToken = null, CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(httpMethod, url);

            // Check if the OAuth Bearer Authorization token should be added to the request
            if (!string.IsNullOrEmpty(bearerToken))
            {
                request.Headers.Add(HttpRequestHeader.Authorization.ToString(), $"Bearer {bearerToken}");
            }

            if (bodyContent != null)
            {
                request.Content = new StringContent(bodyContent, Encoding.UTF8, "application/json");
            }

            // Send the HTTP request
            var response = await _httpClient.SendAsync(request, cancellationToken);

            // Read the body up front (even on error responses) so it can be captured for diagnostics
            // before we potentially throw below. This is the request-body counterpart of the
            // response-body captured by GetContents/SendRequest - previously this method (used by
            // every device-control/setter call: SetLight, SetSiren, SetVolume, SetMotionZones,
            // SetGroupLights, SetLocationMode, UpdateSnapshot, etc.) never surfaced anything through
            // ApiRawLogger, leaving a large blind spot in the raw traffic log.
            var responseFromServer = await response.Content.ReadAsStringAsync(cancellationToken);
            ApiRawLogger.Raise(httpMethod.Method, url.ToString(), (int)response.StatusCode,
                bodyContent == null ? responseFromServer : $"REQUEST: {bodyContent}\nRESPONSE: {responseFromServer}");

            // Validate the resulting HTTP status against the expected status. A caller that didn't
            // provide one still gets checked against "any success" - every call site using this
            // method (SetLight, SetSiren, SetVolume, SetMotionDetection, SetChimeType,
            // SetDoNotDisturb, SetNightMode, SetLocationMode) used to pass NULL and get no
            // validation at all, silently treating a 404/422/500 error response as success.
            if (expectedStatusCode.HasValue)
            {
                if (response.StatusCode != expectedStatusCode.Value)
                {
                    throw new Exceptions.UnexpectedOutcomeException(response.StatusCode, expectedStatusCode.Value);
                }
            }
            else if (!response.IsSuccessStatusCode)
            {
                throw new Exceptions.UnexpectedOutcomeException(response.StatusCode);
            }
        }

        /// <summary>
        /// Sends a HttpRequest to the Ring API server
        /// </summary>
        /// <typeparam name="T">Type of entity to try to parse the result from the Ring API in</typeparam>
        /// <param name="url">Url of the request to make</param>
        /// <param name="httpMethod">The HTTP method to use to call the provided Url</param>
        /// <param name="bodyContent">Content to send along with the request in the body. Leave NULL to not send along any content.</param>
        /// <param name="bearerToken">Bearer token to authenticate the request with. Leave out to not authenticate the session.</param>
        /// <returns>Contents of the result returned by the Ring API parsed in the type T provided</returns>
        public async Task<T> SendRequest<T>(Uri url, HttpMethod httpMethod, string bodyContent, string bearerToken = null, CancellationToken cancellationToken = default)
        {
            // Make the request and get the body contents of the response
            var response = await SendRequest(url, httpMethod, bodyContent, bearerToken, cancellationToken);

            // Try parsing the response to the type provided with this method
            T responseEntity = JsonSerializer.Deserialize<T>(response);
            return responseEntity;
        }

        /// <summary>
        /// Sends a HttpRequest to the Ring API server
        /// </summary>
        /// <param name="url">Url of the request to make</param>
        /// <param name="httpMethod">The HTTP method to use to call the provided Url</param>
        /// <param name="bodyContent">Content to send along with the request in the body. Leave NULL to not send along any content.</param>
        /// <param name="bearerToken">Bearer token to authenticate the request with. Leave out to not authenticate the session.</param>
        /// <param name="cancellationToken">Cancellation token to allow cancelling the request</param>
        /// <returns>Contents of the result returned by the Ring API</returns>
        public async Task<string> SendRequest(Uri url, HttpMethod httpMethod, string bodyContent, string bearerToken = null, CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(httpMethod, url);

            // Check if the OAuth Bearer Authorization token should be added to the request
            if (!string.IsNullOrEmpty(bearerToken))
            {
                request.Headers.Add(HttpRequestHeader.Authorization.ToString(), $"Bearer {bearerToken}");
            }

            if (bodyContent != null)
            {
                request.Content = new StringContent(bodyContent, Encoding.UTF8, "application/json");
            }

            // Send the HTTP request
            var response = await _httpClient.SendAsync(request, cancellationToken);

            // Get the response body and return it
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            ApiRawLogger.Raise(httpMethod.Method, url.ToString(), (int)response.StatusCode, responseBody);
            return responseBody;
        }
    }
}
