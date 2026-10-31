using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Client.Hosting.Remote;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Hosting.Tests.Remote;

/// <summary>The remote flag repository calls /api/v1/media-flags, builds the filter query and maps 409/404/400 back to the domain exceptions.</summary>
public class RemoteMediaFlagRepositoryTests
{
    private static readonly DateTime From = new(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid FlagId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid DeviceId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Seen { get; } = [];
        public List<string?> Bodies { get; } = [];
        public List<CancellationToken> Tokens { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Seen.Add(request);
            Tokens.Add(cancellationToken);
            Bodies.Add(request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken));
            return respond(request);
        }
    }

    private static (RemoteMediaFlagRepository Repo, FakeHandler Handler) Create(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new FakeHandler(respond);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
        return (new RemoteMediaFlagRepository(http), handler);
    }

    private static HttpResponseMessage Json<T>(T value, HttpStatusCode status = HttpStatusCode.OK)
    {
        return new(status) { Content = JsonContent.Create(value) };
    }

    private static MediaFlagDto SampleDto(Guid? id = null)
    {
        return new MediaFlagDto(
            id ?? FlagId, "BitrateCollapse", DeviceId, null, From, From.AddMinutes(5), From.AddMinutes(6),
            "Warn", "Unreviewed", null, null, null, null, 0);
    }

    private static MediaFlagFilter Filter()
    {
        return new MediaFlagFilter
        {
            Types = [MediaFlagType.BitrateCollapse],
            Statuses = [MediaFlagStatus.Unreviewed],
            DeviceIds = [DeviceId],
            FromUtc = From,
            ToUtc = From.AddDays(7),
        };
    }

    [Fact]
    public async Task ListPageAsync_BuildsFilterAndPagingQueryAndMapsPage()
    {
        (RemoteMediaFlagRepository repo, FakeHandler handler) = Create(_ => Json(new MediaFlagPageDto([SampleDto()], 9)));

        MediaFlagPage page = await repo.ListPageAsync(Filter(), 20, 10, CancellationToken.None);

        HttpRequestMessage req = Assert.Single(handler.Seen);
        Assert.Equal(HttpMethod.Get, req.Method);
        Assert.Equal("/api/v1/media-flags", req.RequestUri!.AbsolutePath);
        string query = req.RequestUri.Query;
        Assert.Contains("types=BitrateCollapse", query);
        Assert.Contains("statuses=Unreviewed", query);
        Assert.Contains("deviceIds=" + DeviceId.ToString("D"), query);
        Assert.Contains("fromUtc=" + Uri.EscapeDataString(From.ToString("O")), query);
        Assert.Contains("toUtc=" + Uri.EscapeDataString(From.AddDays(7).ToString("O")), query);
        Assert.Contains("skip=20", query);
        Assert.Contains("take=10", query);
        Assert.Equal(9, page.TotalCount);
        Assert.Equal(FlagId, Assert.Single(page.Items).Id);
    }

    [Fact]
    public async Task ListPageAsync_ForwardsCancellationToken()
    {
        (RemoteMediaFlagRepository repo, FakeHandler handler) = Create(_ => Json(new MediaFlagPageDto([], 0)));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repo.ListPageAsync(new MediaFlagFilter(), 0, 5, cts.Token));
        Assert.All(handler.Tokens, t => Assert.True(t.IsCancellationRequested));
    }

    [Fact]
    public async Task CountAsync_BuildsCountRouteWithFilterAndReturnsCount()
    {
        (RemoteMediaFlagRepository repo, FakeHandler handler) = Create(_ => Json(7));

        int count = await repo.CountAsync(Filter(), CancellationToken.None);

        HttpRequestMessage req = Assert.Single(handler.Seen);
        Assert.Equal("/api/v1/media-flags/count", req.RequestUri!.AbsolutePath);
        Assert.Contains("types=BitrateCollapse", req.RequestUri.Query);
        Assert.Equal(7, count);
    }

    [Fact]
    public async Task GetAsync_NotFound_ReturnsNull()
    {
        (RemoteMediaFlagRepository repo, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        MediaFlag? flag = await repo.GetAsync(FlagId, CancellationToken.None);

        Assert.Null(flag);
    }

    [Fact]
    public async Task GetAsync_Found_ReturnsMappedFlag()
    {
        (RemoteMediaFlagRepository repo, FakeHandler handler) = Create(_ => Json(SampleDto()));

        MediaFlag? flag = await repo.GetAsync(FlagId, CancellationToken.None);

        Assert.Equal("/api/v1/media-flags/" + FlagId.ToString("D"), Assert.Single(handler.Seen).RequestUri!.AbsolutePath);
        Assert.Equal(MediaFlagType.BitrateCollapse, flag!.FlagType);
        Assert.Equal(MediaFlagStatus.Unreviewed, flag.Status);
    }

    [Fact]
    public async Task GetNeighbourAsync_SendsDirectionAndFilter()
    {
        (RemoteMediaFlagRepository repo, FakeHandler handler) = Create(_ => Json(SampleDto(Guid.NewGuid())));

        MediaFlag? next = await repo.GetNeighbourAsync(FlagId, Filter(), MediaFlagNeighbour.Previous, CancellationToken.None);

        HttpRequestMessage req = Assert.Single(handler.Seen);
        Assert.Equal("/api/v1/media-flags/" + FlagId.ToString("D") + "/neighbour", req.RequestUri!.AbsolutePath);
        Assert.Contains("direction=Previous", req.RequestUri.Query);
        Assert.Contains("statuses=Unreviewed", req.RequestUri.Query);
        Assert.NotNull(next);
    }

    [Fact]
    public async Task GetNeighbourAsync_NoContent_ReturnsNullAtEndOfList()
    {
        (RemoteMediaFlagRepository repo, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.NoContent));

        MediaFlag? next = await repo.GetNeighbourAsync(FlagId, Filter(), MediaFlagNeighbour.Next, CancellationToken.None);

        Assert.Null(next);
    }

    [Fact]
    public async Task UpdateStatusAsync_PostsStatusesAndReasonWithoutOperatorName()
    {
        (RemoteMediaFlagRepository repo, FakeHandler handler) = Create(_ => Json(SampleDto()));

        _ = await repo.UpdateStatusAsync(FlagId, MediaFlagStatus.Unreviewed, MediaFlagStatus.Dismissed, "false positive", "operator-name-xyz", CancellationToken.None);

        HttpRequestMessage req = Assert.Single(handler.Seen);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Equal("/api/v1/media-flags/" + FlagId.ToString("D") + "/status", req.RequestUri!.AbsolutePath);
        string body = Assert.Single(handler.Bodies)!;
        using JsonDocument doc = JsonDocument.Parse(body);
        Assert.Equal("Unreviewed", doc.RootElement.GetProperty("expectedStatus").GetString());
        Assert.Equal("Dismissed", doc.RootElement.GetProperty("newStatus").GetString());
        Assert.Equal("false positive", doc.RootElement.GetProperty("reason").GetString());
        Assert.DoesNotContain("operator-name-xyz", body);
    }

    [Fact]
    public async Task UpdateStatusAsync_Conflict_ThrowsFlagStatusConflictException()
    {
        var reviewedAt = From.AddHours(3);
        (RemoteMediaFlagRepository repo, _) = Create(_ => Json(
            new FlagStatusConflictDto("Unreviewed", "Kept", "reviewer-7", reviewedAt), HttpStatusCode.Conflict));

        FlagStatusConflictException ex = await Assert.ThrowsAsync<FlagStatusConflictException>(() =>
            repo.UpdateStatusAsync(FlagId, MediaFlagStatus.Unreviewed, MediaFlagStatus.Dismissed, "reason", "op", CancellationToken.None));

        Assert.Equal(FlagId, ex.FlagId);
        Assert.Equal(MediaFlagStatus.Unreviewed, ex.ExpectedStatus);
        Assert.Equal(MediaFlagStatus.Kept, ex.ActualStatus);
        Assert.Equal("reviewer-7", ex.ReviewedBy);
        Assert.Equal(reviewedAt, ex.ReviewedAtUtc);
    }

    [Fact]
    public async Task UpdateStatusAsync_NotFound_ThrowsKeyNotFoundException()
    {
        (RemoteMediaFlagRepository repo, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        _ = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            repo.UpdateStatusAsync(FlagId, MediaFlagStatus.Unreviewed, MediaFlagStatus.Kept, null, "op", CancellationToken.None));
    }

    [Fact]
    public async Task UpdateStatusAsync_BadRequest_ThrowsArgumentException()
    {
        (RemoteMediaFlagRepository repo, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));

        _ = await Assert.ThrowsAsync<ArgumentException>(() =>
            repo.UpdateStatusAsync(FlagId, MediaFlagStatus.Unreviewed, MediaFlagStatus.Dismissed, null, "op", CancellationToken.None));
    }

    [Fact]
    public async Task ReopenAsync_PostsReasonToReopenRoute()
    {
        (RemoteMediaFlagRepository repo, FakeHandler handler) = Create(_ => Json(SampleDto()));

        _ = await repo.ReopenAsync(FlagId, "op", "new evidence", CancellationToken.None);

        HttpRequestMessage req = Assert.Single(handler.Seen);
        Assert.Equal("/api/v1/media-flags/" + FlagId.ToString("D") + "/reopen", req.RequestUri!.AbsolutePath);
        using JsonDocument doc = JsonDocument.Parse(Assert.Single(handler.Bodies)!);
        Assert.Equal("new evidence", doc.RootElement.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task ReopenAsync_Conflict_ThrowsFlagStatusConflictException()
    {
        (RemoteMediaFlagRepository repo, _) = Create(_ => Json(
            new FlagStatusConflictDto("Dismissed", "Unreviewed", null, null), HttpStatusCode.Conflict));

        FlagStatusConflictException ex = await Assert.ThrowsAsync<FlagStatusConflictException>(() =>
            repo.ReopenAsync(FlagId, "op", "reason", CancellationToken.None));

        Assert.Equal(MediaFlagStatus.Dismissed, ex.ExpectedStatus);
        Assert.Equal(MediaFlagStatus.Unreviewed, ex.ActualStatus);
        Assert.Null(ex.ReviewedBy);
    }

    [Fact]
    public async Task UpsertRaisedAsync_IsServerOnly_ThrowsNotSupported()
    {
        (RemoteMediaFlagRepository repo, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.OK));

        _ = await Assert.ThrowsAsync<NotSupportedException>(() =>
            repo.UpsertRaisedAsync(new MediaFlag { DeviceId = DeviceId }, CancellationToken.None));
    }

    [Fact]
    public async Task ReconcileForMediaAsync_IsServerOnly_ThrowsNotSupported()
    {
        (RemoteMediaFlagRepository repo, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.OK));

        _ = await Assert.ThrowsAsync<NotSupportedException>(() =>
            repo.ReconcileForMediaAsync(Guid.NewGuid(), new HashSet<MediaFlagType>(), CancellationToken.None));
    }
}
