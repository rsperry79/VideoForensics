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

        private static string ListJson(params string[] releaseJsons) => "[" + string.Join(",", releaseJsons) + "]";

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
                        name = "VideoForensicsSetup.exe",
                        browser_download_url = $"https://github.com/rsperry79/VideoForensics/releases/download/{tagName}/VideoForensicsSetup.exe",
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
                    Content = new StringContent(ListJson(GetRealisticReleaseJson("v1.5.0", draft: false, prerelease: false)))
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
            Assert.Equal("VideoForensicsSetup.exe", result.Assets[0].Name);
            Assert.Equal("https://github.com/rsperry79/VideoForensics/releases/download/v1.5.0/VideoForensicsSetup.exe", result.Assets[0].BrowserDownloadUrl);
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
        public async Task GetLatestTestingReleaseAsync_SuccessfulResponse_ReturnsParsedReleaseInfo()
        {
            HttpClient httpClient = CreateHttpClient(request =>
            {
                Assert.EndsWith("releases?per_page=30", request.RequestUri?.ToString() ?? "");
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(ListJson(GetRealisticReleaseJson("v1.2.3-dev.5", draft: false, prerelease: true)))
                };
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                return Task.FromResult(response);
            });

            var client = new GitHubReleaseClient(httpClient);
            var result = await client.GetLatestTestingReleaseAsync(CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("v1.2.3-dev.5", result!.TagName);
            Assert.True(result.Prerelease);
            Assert.Equal(2, result.Assets.Count);
        }

        [Fact]
        public async Task GetLatestTestingReleaseAsync_NotFound_ReturnsNull()
        {
            HttpClient httpClient = CreateHttpClient(request =>
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            });

            var client = new GitHubReleaseClient(httpClient);
            var result = await client.GetLatestTestingReleaseAsync(CancellationToken.None);

            Assert.Null(result);
        }

        private const string VersionJsonUrl = "https://github.com/rsperry79/VideoForensics/releases/download/Release/version.json";

        private static string GetReleaseJsonWithVersionJsonAsset(string tagName)
        {
            var release = new
            {
                tag_name = tagName,
                html_url = $"https://github.com/rsperry79/VideoForensics/releases/tag/{tagName}",
                draft = false,
                prerelease = false,
                assets = new[]
                {
                    new
                    {
                        name = "VideoForensicsSetup.exe",
                        browser_download_url = $"https://github.com/rsperry79/VideoForensics/releases/download/{tagName}/VideoForensicsSetup.exe",
                        size = 5242880L
                    },
                    new
                    {
                        name = "version.json",
                        browser_download_url = VersionJsonUrl,
                        size = 64L
                    }
                }
            };
            return JsonSerializer.Serialize(release);
        }

        private static HttpResponseMessage JsonResponse(string json)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            return response;
        }

        [Fact]
        public async Task GetLatestReleaseAsync_VersionJsonAssetPresent_SetsVersionFromAsset()
        {
            HttpClient httpClient = CreateHttpClient(request =>
            {
                if (request.RequestUri?.ToString() == VersionJsonUrl)
                {
                    return Task.FromResult(JsonResponse("{\"version\":\"1.0.9.44078\",\"channel\":\"Release\"}"));
                }
                return Task.FromResult(JsonResponse(ListJson(GetReleaseJsonWithVersionJsonAsset("v1.0.9"))));
            });

            var client = new GitHubReleaseClient(httpClient);
            var result = await client.GetLatestReleaseAsync(CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("v1.0.9", result!.TagName);
            Assert.Equal("1.0.9.44078", result.Version);
        }

        [Fact]
        public async Task GetLatestReleaseAsync_VersionJsonAssetAbsent_ReturnsReleaseWithNullVersion()
        {
            var requestedUrls = new List<string>();
            HttpClient httpClient = CreateHttpClient(request =>
            {
                requestedUrls.Add(request.RequestUri?.ToString() ?? "");
                return Task.FromResult(JsonResponse(ListJson(GetRealisticReleaseJson("v1.0.9"))));
            });

            var client = new GitHubReleaseClient(httpClient);
            var result = await client.GetLatestReleaseAsync(CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("v1.0.9", result!.TagName);
            Assert.Null(result.Version);
            Assert.DoesNotContain(requestedUrls, u => u.EndsWith("version.json", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task GetLatestReleaseAsync_VersionJsonDownloadFails_ReturnsReleaseWithNullVersion()
        {
            HttpClient httpClient = CreateHttpClient(request =>
            {
                if (request.RequestUri?.ToString() == VersionJsonUrl)
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
                }
                return Task.FromResult(JsonResponse(ListJson(GetReleaseJsonWithVersionJsonAsset("v1.0.9"))));
            });

            var client = new GitHubReleaseClient(httpClient);
            var result = await client.GetLatestReleaseAsync(CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("v1.0.9", result!.TagName);
            Assert.Null(result.Version);
        }

        [Fact]
        public async Task GetLatestReleaseAsync_VersionJsonMalformed_ReturnsReleaseWithNullVersion()
        {
            HttpClient httpClient = CreateHttpClient(request =>
            {
                if (request.RequestUri?.ToString() == VersionJsonUrl)
                {
                    return Task.FromResult(JsonResponse("{not valid json"));
                }
                return Task.FromResult(JsonResponse(ListJson(GetReleaseJsonWithVersionJsonAsset("v1.0.9"))));
            });

            var client = new GitHubReleaseClient(httpClient);
            var result = await client.GetLatestReleaseAsync(CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("v1.0.9", result!.TagName);
            Assert.Null(result.Version);
        }

        private static string GetReleaseJsonWithAssets(string tagName, bool prerelease, params string[] assetNames)
        {
            var release = new
            {
                tag_name = tagName,
                html_url = $"https://github.com/rsperry79/VideoForensics/releases/tag/{tagName}",
                draft = false,
                prerelease,
                assets = assetNames.Select(n => new
                {
                    name = n,
                    browser_download_url = n == "version.json"
                        ? VersionJsonUrl
                        : $"https://github.com/rsperry79/VideoForensics/releases/download/{tagName}/{n}",
                    size = 1024L
                }).ToArray()
            };
            return JsonSerializer.Serialize(release);
        }

        private static async Task<GitHubReleaseInfo?> FetchWithAssetsAsync(string versionJsonBody, int versionJsonStatus, params string[] assetNames)
        {
            HttpClient httpClient = new GitHubReleaseClientTests().CreateHttpClient(request =>
            {
                if (request.RequestUri?.ToString() == VersionJsonUrl)
                {
                    if (versionJsonStatus != 200)
                    {
                        return Task.FromResult(new HttpResponseMessage((HttpStatusCode)versionJsonStatus));
                    }
                    return Task.FromResult(JsonResponse(versionJsonBody));
                }
                return Task.FromResult(JsonResponse(ListJson(GetReleaseJsonWithAssets("v1.0.191-dev.1", true, assetNames))));
            });

            return await new GitHubReleaseClient(httpClient).GetLatestTestingReleaseAsync(CancellationToken.None);
        }

        [Fact]
        public async Task GetLatestTestingReleaseAsync_VersionJsonAndDebBothPresent_PrefersVersionJson()
        {
            var result = await FetchWithAssetsAsync("{\"version\":\"9.9.9\"}", 200, "version.json", "videoforensics_1.0.191_amd64.deb");

            Assert.NotNull(result);
            Assert.Equal("9.9.9", result!.Version);
        }

        [Fact]
        public async Task GetLatestTestingReleaseAsync_NoVersionJsonThreePartDeb_DerivesVersionFromDebName()
        {
            var result = await FetchWithAssetsAsync("", 200, "VideoForensicsSetup.exe", "videoforensics_1.0.191_amd64.deb");

            Assert.NotNull(result);
            Assert.Equal("1.0.191", result!.Version);
        }

        [Fact]
        public async Task GetLatestReleaseAsync_NoVersionJsonFourPartDeb_DerivesVersionFromDebName()
        {
            HttpClient httpClient = CreateHttpClient(_ => Task.FromResult(JsonResponse(
                ListJson(GetReleaseJsonWithAssets("v1.0.33", false, "videoforensics_1.0.33.20390_amd64.deb")))));

            var result = await new GitHubReleaseClient(httpClient).GetLatestReleaseAsync(CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("1.0.33.20390", result!.Version);
        }

        [Fact]
        public async Task GetLatestTestingReleaseAsync_NoVersionJsonAndNoMatchingAsset_VersionIsNull()
        {
            var result = await FetchWithAssetsAsync("", 200, "VideoForensicsSetup.exe", "VideoForensics.deb", "notvideoforensics_1.0.1_amd64.deb");

            Assert.NotNull(result);
            Assert.Null(result!.Version);
        }

        [Fact]
        public async Task GetLatestTestingReleaseAsync_VersionJsonUnparseableAndDebPresent_FallsBackToDebName()
        {
            var result = await FetchWithAssetsAsync("{not valid json", 200, "version.json", "videoforensics_1.0.191_amd64.deb");

            Assert.NotNull(result);
            Assert.Equal("1.0.191", result!.Version);
        }

        [Fact]
        public async Task GetLatestTestingReleaseAsync_VersionJsonDownloadFailsAndDebPresent_FallsBackToDebName()
        {
            var result = await FetchWithAssetsAsync("", 404, "version.json", "videoforensics_1.0.191_amd64.deb");

            Assert.NotNull(result);
            Assert.Equal("1.0.191", result!.Version);
        }

        private const string ListEndpointSuffix = "releases?per_page=30";

        private static GitHubReleaseClient ClientReturning(string listJson, Func<HttpRequestMessage, HttpResponseMessage?>? extra = null)
        {
            HttpClient httpClient = new GitHubReleaseClientTests().CreateHttpClient(request =>
            {
                HttpResponseMessage? special = extra?.Invoke(request);
                if (special != null)
                {
                    return Task.FromResult(special);
                }
                Assert.EndsWith(ListEndpointSuffix, request.RequestUri?.ToString() ?? "");
                return Task.FromResult(JsonResponse(listJson));
            });
            return new GitHubReleaseClient(httpClient);
        }

        [Fact]
        public async Task GetLatestTestingReleaseAsync_StaleTestingLabelListedFirst_PicksNewestVersionedPrerelease()
        {
            string list = ListJson(
                GetRealisticReleaseJson("Testing", prerelease: true),
                GetRealisticReleaseJson("v1.1.21-dev.513", prerelease: true),
                GetRealisticReleaseJson("v1.1.21-dev.512", prerelease: true));

            var result = await ClientReturning(list).GetLatestTestingReleaseAsync(CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("v1.1.21-dev.513", result!.TagName);
        }

        [Fact]
        public async Task GetLatestReleaseAsync_MixedList_PicksNewestNonPrereleaseVersionedRelease()
        {
            string list = ListJson(
                GetRealisticReleaseJson("Release", prerelease: false),
                GetRealisticReleaseJson("v1.1.21-dev.513", prerelease: true),
                GetRealisticReleaseJson("v1.1.20-main.7", prerelease: false),
                GetRealisticReleaseJson("v1.1.19-main.6", prerelease: false));

            var result = await ClientReturning(list).GetLatestReleaseAsync(CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("v1.1.20-main.7", result!.TagName);
        }

        [Fact]
        public async Task GetLatestTestingReleaseAsync_DraftListedFirst_SkipsDraft()
        {
            string list = ListJson(
                GetRealisticReleaseJson("v1.1.22-dev.514", draft: true, prerelease: true),
                GetRealisticReleaseJson("v1.1.21-dev.513", prerelease: true));

            var result = await ClientReturning(list).GetLatestTestingReleaseAsync(CancellationToken.None);

            Assert.Equal("v1.1.21-dev.513", result!.TagName);
        }

        [Fact]
        public async Task GetLatestReleaseAsync_DraftListedFirst_SkipsDraft()
        {
            string list = ListJson(
                GetRealisticReleaseJson("v1.1.22-main.9", draft: true, prerelease: false),
                GetRealisticReleaseJson("v1.1.21-main.8", prerelease: false));

            var result = await ClientReturning(list).GetLatestReleaseAsync(CancellationToken.None);

            Assert.Equal("v1.1.21-main.8", result!.TagName);
        }

        [Fact]
        public async Task GetLatestTestingReleaseAsync_OnlyStableReleasesListed_ReturnsNull()
        {
            string list = ListJson(GetRealisticReleaseJson("v1.1.20-main.7", prerelease: false));

            var result = await ClientReturning(list).GetLatestTestingReleaseAsync(CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetLatestReleaseAsync_OnlyPrereleasesListed_ReturnsNull()
        {
            string list = ListJson(GetRealisticReleaseJson("v1.1.21-dev.513", prerelease: true));

            var result = await ClientReturning(list).GetLatestReleaseAsync(CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetLatestReleaseAsync_OnlyLabelReleasesListed_ReturnsNull()
        {
            string list = ListJson(
                GetRealisticReleaseJson("Release", prerelease: false),
                GetRealisticReleaseJson("Testing", prerelease: true));

            Assert.Null(await ClientReturning(list).GetLatestReleaseAsync(CancellationToken.None));
            Assert.Null(await ClientReturning(list).GetLatestTestingReleaseAsync(CancellationToken.None));
        }

        [Fact]
        public async Task GetLatestTestingReleaseAsync_EmptyList_ReturnsNull()
        {
            var result = await ClientReturning("[]").GetLatestTestingReleaseAsync(CancellationToken.None);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetLatestTestingReleaseAsync_ChosenReleaseHasVersionJson_UsesItsVersion()
        {
            string list = ListJson(
                GetRealisticReleaseJson("Testing", prerelease: true),
                GetReleaseJsonWithAssets("v1.1.21-dev.513", true, "version.json"));

            var client = ClientReturning(list, request =>
                request.RequestUri?.ToString() == VersionJsonUrl
                    ? JsonResponse("{\"version\":\"1.1.21.513\"}")
                    : null);

            var result = await client.GetLatestTestingReleaseAsync(CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal("v1.1.21-dev.513", result!.TagName);
            Assert.Equal("1.1.21.513", result.Version);
        }
    }
}
