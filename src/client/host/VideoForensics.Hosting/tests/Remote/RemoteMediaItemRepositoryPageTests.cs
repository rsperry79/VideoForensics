using System.Net;
using System.Text.Json;

using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Hosting.Remote;

namespace VideoForensics.Client.Hosting.Tests.Remote;

public class RemoteMediaItemRepositoryPageTests
{
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handleRequest;

        public MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handleRequest)
        {
            _handleRequest = handleRequest;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handleRequest(request);
        }
    }

    private static MediaItemDto CreateDto(Guid id)
    {
        return new MediaItemDto(
            Id: id,
            DeviceId: Guid.NewGuid(),
            DownloadEventId: null,
            FileName: "2024-01-15_120000.mp4",
            FilePath: "/media/2024-01-15_120000.mp4",
            MediaFormat: "mp4",
            FileSizeBytes: 1024,
            RecordedAtUtc: new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc),
            DownloadedAtUtc: new DateTime(2024, 1, 15, 12, 5, 0, DateTimeKind.Utc),
            Sha256Hash: "abc123",
            VideoCodec: "h264",
            AudioCodec: "aac",
            Resolution: "1920x1080",
            FrameRate: 30.0m,
            IntegrityVerified: true,
            LastVerifiedAtUtc: null,
            IsPurged: false,
            PurgedAtUtc: null,
            PurgeReason: null,
            MetadataJson: null,
            ApiSourceHash: null
        );
    }

    private static Dictionary<string, List<string>> ParseQuery(Uri uri)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        string query = uri.Query.TrimStart('?');
        foreach (string pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = pair.IndexOf('=');
            string key = Uri.UnescapeDataString(eq < 0 ? pair : pair[..eq]);
            string value = eq < 0 ? string.Empty : Uri.UnescapeDataString(pair[(eq + 1)..]);
            if (!result.TryGetValue(key, out List<string>? values))
            {
                values = new List<string>();
                result[key] = values;
            }

            values.Add(value);
        }

        return result;
    }

    [Fact]
    public async Task RemoteMediaItemRepository_GetPage_SendsDevicesRangeAndPaging_InQueryString()
    {
        Guid deviceA = Guid.NewGuid();
        Guid deviceB = Guid.NewGuid();
        var fromUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2024, 1, 31, 23, 59, 59, DateTimeKind.Utc);
        HttpRequestMessage? capturedRequest = null;

        var handler = new MockHttpMessageHandler(async request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
        var repo = new RemoteMediaItemRepository(httpClient);

        _ = await repo.GetPageByDevicesAndDateRangeAsync(
            new[] { deviceA, deviceB }, fromUtc, toUtc, skip: 5, take: 10, CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Uri uri = capturedRequest!.RequestUri!;
        Assert.Equal("/api/v1/media-items/page", uri.AbsolutePath);

        Dictionary<string, List<string>> query = ParseQuery(uri);
        Assert.Equal(
            new[] { deviceA.ToString("D"), deviceB.ToString("D") },
            query["deviceIds"]);
        Assert.Equal(new[] { fromUtc.ToString("o") }, query["from"]);
        Assert.Equal(new[] { toUtc.ToString("o") }, query["to"]);
        Assert.Equal(new[] { "5" }, query["skip"]);
        Assert.Equal(new[] { "10" }, query["take"]);
    }

    [Fact]
    public async Task RemoteMediaItemRepository_GetPage_ResponseItemsAndTotal_MapToMediaItemPage()
    {
        Guid idOne = Guid.NewGuid();
        Guid idTwo = Guid.NewGuid();
        string jsonContent = JsonSerializer.Serialize(new
        {
            items = new[] { CreateDto(idOne), CreateDto(idTwo) },
            totalCount = 42
        });

        var handler = new MockHttpMessageHandler(async request =>
        {
            return new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
        var repo = new RemoteMediaItemRepository(httpClient);

        MediaItemPage page = await repo.GetPageByDevicesAndDateRangeAsync(
            new[] { Guid.NewGuid() },
            new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2024, 1, 31, 0, 0, 0, DateTimeKind.Utc),
            skip: 0, take: 2, CancellationToken.None);

        Assert.Equal(2, page.Items.Count);
        Assert.Equal(idOne, page.Items[0].Id);
        Assert.Equal(idTwo, page.Items[1].Id);
        Assert.Equal(42, page.TotalCount);
    }

    [Fact]
    public async Task RemoteMediaItemRepository_GetPage_EmptyBody_ReturnsZeroTotalAndNoItems()
    {
        var handler = new MockHttpMessageHandler(async request =>
        {
            return new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
        var repo = new RemoteMediaItemRepository(httpClient);

        MediaItemPage page = await repo.GetPageByDevicesAndDateRangeAsync(
            new[] { Guid.NewGuid() },
            new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2024, 1, 31, 0, 0, 0, DateTimeKind.Utc),
            skip: 0, take: 10, CancellationToken.None);

        Assert.Equal(0, page.TotalCount);
        Assert.Empty(page.Items);
    }
}
