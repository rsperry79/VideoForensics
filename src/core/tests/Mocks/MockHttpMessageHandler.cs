using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Ring.Api.Tests.Mocks
{
    /// <summary>
    /// Mock HTTP message handler that returns predefined responses instead of making real HTTP calls
    /// </summary>
    public class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (HttpStatusCode statusCode, string content)> _responses = new();

        /// <summary>
        /// Records every request made through this handler (method + full url) so tests can assert
        /// which endpoint and HTTP verb a Session method actually called.
        /// </summary>
        public List<(HttpMethod Method, string Url)> RequestLog { get; } = new();

        public MockHttpMessageHandler()
        {
            // Setup default mock responses for authentication
            SetupDefaultResponses();
        }

        public void SetupResponse(string url, HttpStatusCode statusCode, string content)
        {
            _responses[url.ToLower()] = (statusCode, content);
        }

        public void SetupResponse(string url, string content)
        {
            SetupResponse(url, HttpStatusCode.OK, content);
        }

        private void SetupDefaultResponses()
        {
            // Mock OAuth authentication response
            var authResponse = @"{
                ""access_token"": ""mock_access_token_12345"",
                ""refresh_token"": ""mock_refresh_token_12345"",
                ""expires_in"": 3600,
                ""token_type"": ""Bearer""
            }";

            SetupResponse("https://oauth.ring.com/oauth/token", HttpStatusCode.OK, authResponse);

            // Mock devices response
            var devicesResponse = @"{
                ""doorbots"": [
                    {
                        ""id"": 123456,
                        ""description"": ""Front Door"",
                        ""device_kind"": ""doorbot"",
                        ""subscribed"": true,
                        ""subscribed_buttons"": [""motion"", ""snapshot""]
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
                        ""subscribed"": true
                    }
                ]
            }";

            SetupResponse("https://api.ring.com/clients_api/v1/user/devices", HttpStatusCode.OK, devicesResponse);

            // Mock history response
            var historyResponse = @"{
                ""events"": [
                    {
                        ""id"": ""aabbccdd"",
                        ""id_str"": ""aabbccdd"",
                        ""created_at"": ""2024-01-15T10:30:00.000Z"",
                        ""motion"": false,
                        ""snapshot_url"": ""https://example.com/snapshot.jpg"",
                        ""kind"": ""motion"",
                        ""favorite"": false,
                        ""snapshot"": {
                            ""status"": ""ready""
                        }
                    }
                ]
            }";

            SetupResponse("https://api.ring.com/clients_api/v1/user/activity", HttpStatusCode.OK, historyResponse);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var requestUrl = request.RequestUri?.ToString().ToLower() ?? string.Empty;
            RequestLog.Add((request.Method, request.RequestUri?.ToString() ?? string.Empty));

            // Find matching response
            var matchingKey = _responses.Keys.FirstOrDefault(k => requestUrl.Contains(k.Replace("https://", "").Replace("http://", "")));

            if (matchingKey != null && _responses.TryGetValue(matchingKey, out var response))
            {
                return await Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = response.statusCode,
                    Content = new StringContent(response.content),
                    RequestMessage = request
                });
            }

            // Default 404 for unknown URLs
            return await Task.FromResult(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound,
                Content = new StringContent("{\"error\": \"Not found\"}"),
                RequestMessage = request
            });
        }
    }
}
