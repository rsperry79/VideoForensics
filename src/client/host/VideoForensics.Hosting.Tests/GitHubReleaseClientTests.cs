using System.Net;
using System.Text.Json;

using Xunit;

namespace VideoForensics.Hosting.Tests
{
    public class GitHubReleaseClientTests
    {
        private class TestHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

            public TestHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
            {
                _handler = handler;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return _handler(request);
            }
        }

        private HttpClient CreateHttpClient(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            return new HttpClient(new TestHttpMessageHandler(handler))
            {
                BaseAddress = new Uri("https://api.github.com/")
            };
        }

        private static string GetRealisticReleaseJson(string tagName = "v1.2.3", bool draft = false, bool prerelease = false)
        {
            var release = new
            {
                tag_name = tagName,
                html_url = $"https://github.com/rsperry79/VideoForensics/releases/tag/{tagName}",
                draft,
                prerelease,
                assets = new[]
                {
                    new
                    {
                        name = "VideoForensicsBootstrapper.exe",
                        browser_download_url = $"https://github.com/rsperry79/VideoForensics/releases/download/{tagName}/VideoForensicsBootstrapper.exe",
                        size = 5242880L
                    },
                    new
                    {
                        name = "VideoForensics.deb",
                        browser_download_url = $"https://github.com/rsperry79/VideoForensics/releases/download/{tagName}/VideoForensics.deb",
                        size = 10485760L
                    }
                }
            };
            return JsonSerializer.Serialize(release);
        }

        [Fact]
        public async Task GetLatestReleaseAsync_SuccessfulResponse_ReturnsParsedReleaseInfo()
        {
            HttpClient httpClient = CreateHttpClient(request =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(GetRealisticReleaseJson("v1.5.0", draft: false, prerelease: false))
                };
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                return Task.FromResult(response);
            });

            var client = new GitHubReleaseClient(httpClient);
            var result = await client.GetLatestReleaseAsync(CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("v1.5.0", result!.TagName);
            Assert.Equal("https://github.com/rsperry79/VideoForensics/releases/tag/v1.5.0", result.HtmlUrl);
            Assert.False(result.Draft);
            Assert.False(result.Prerelease);
            Assert.Equal(2, result.Assets.Count);
            Assert.Equal("VideoForensicsBootstrapper.exe", result.Assets[0].Name);
            Assert.Equal("https://github.com/rsperry79/VideoForensics/releases/download/v1.5.0/VideoForensicsBootstrapper.exe", result.Assets[0].BrowserDownloadUrl);
            Assert.Equal(5242880L, result.Assets[0].Size);
        }

        [Fact]
        public async Task GetLatestReleaseAsync_NotFound_ReturnsNull()
        {
            HttpClient httpClient = CreateHttpClient(request =>
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            });

            var client = new GitHubReleaseClient(httpClient);
            var result = await client.GetLatestReleaseAsync(CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetLatestReleaseAsync_RateLimited_ReturnsNull()
        {
            HttpClient httpClient = CreateHttpClient(request =>
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));
            });

            var client = new GitHubReleaseClient(httpClient);
            var result = await client.GetLatestReleaseAsync(CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetLatestReleaseAsync_MalformedJson_ReturnsNull()
        {
            HttpClient httpClient = CreateHttpClient(request =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{invalid json content")
                };
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                return Task.FromResult(response);
            });

            var client = new GitHubReleaseClient(httpClient);
            var result = await client.GetLatestReleaseAsync(CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetLatestDevReleaseAsync_SuccessfulResponse_ReturnsParsedReleaseInfo()
        {
            HttpClient httpClient = CreateHttpClient(request =>
            {
                // Verify it's calling the dev endpoint
                Assert.EndsWith("releases/tags/dev", request.RequestUri?.ToString() ?? "");
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(GetRealisticReleaseJson("dev", draft: false, prerelease: true))
                };
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                return Task.FromResult(response);
            });

            var client = new GitHubReleaseClient(httpClient);
            var result = await client.GetLatestDevReleaseAsync(CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("dev", result!.TagName);
            Assert.True(result.Prerelease);
            Assert.Equal(2, result.Assets.Count);
        }

        [Fact]
        public async Task GetLatestDevReleaseAsync_NotFound_ReturnsNull()
        {
            HttpClient httpClient = CreateHttpClient(request =>
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            });

            var client = new GitHubReleaseClient(httpClient);
            var result = await client.GetLatestDevReleaseAsync(CancellationToken.None);

            Assert.Null(result);
        }
    }
}
