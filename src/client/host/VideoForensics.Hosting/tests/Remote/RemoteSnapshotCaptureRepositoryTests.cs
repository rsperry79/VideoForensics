using System.Net;
using System.Net.Http.Json;

using VideoForensics.Client.Hosting.Remote;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Hosting.Tests.Remote;

/// <summary>The remote snapshot-capture repository calls /api/v1/snapshot-captures and maps the rows back to SnapshotCapture.</summary>
public class RemoteSnapshotCaptureRepositoryTests
{
    private static readonly Guid CaptureId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid DeviceId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset From = new(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Seen { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Seen.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    private static (RemoteSnapshotCaptureRepository Repo, FakeHandler Handler) Create(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new FakeHandler(respond);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
        return (new RemoteSnapshotCaptureRepository(http), handler);
    }

    private static HttpResponseMessage Json<T>(T value, HttpStatusCode status = HttpStatusCode.OK)
    {
        return new(status) { Content = JsonContent.Create(value) };
    }

    [Fact]
    public async Task RemoteSnapshotCaptureRepository_GetCurrent_MapsResponse()
    {
        var dto = new SnapshotCaptureDto(CaptureId, DeviceId, From, "fresh", 200, true, "snapshots/still.jpg", null);
        (RemoteSnapshotCaptureRepository repo, FakeHandler handler) = Create(_ => Json(new[] { dto }));

        IReadOnlyList<SnapshotCapture> captures = await repo.GetCurrentForDeviceAsync(DeviceId, From, From.AddHours(1), CancellationToken.None);

        HttpRequestMessage req = Assert.Single(handler.Seen);
        Assert.Equal(HttpMethod.Get, req.Method);
        Assert.Equal("/api/v1/snapshot-captures", req.RequestUri!.AbsolutePath);
        Assert.Contains("deviceId=" + DeviceId.ToString("D"), req.RequestUri.Query);
        Assert.Contains("attemptsOnly=false", req.RequestUri.Query);
        SnapshotCapture capture = Assert.Single(captures);
        Assert.Equal(CaptureId, capture.Id);
        Assert.Equal(DeviceId, capture.DeviceId);
        Assert.Equal(From, capture.CapturedAtUtc);
        Assert.True(capture.IsCurrent);
        Assert.Equal("snapshots/still.jpg", capture.MediaFilePath);
    }

    [Fact]
    public async Task RemoteSnapshotCaptureRepository_GetAttempts_SendsAttemptsOnlyTrue()
    {
        (RemoteSnapshotCaptureRepository repo, FakeHandler handler) = Create(_ => Json(Array.Empty<SnapshotCaptureDto>()));

        IReadOnlyList<SnapshotCapture> captures = await repo.GetAttemptsForDeviceAsync(DeviceId, From, From.AddHours(1), CancellationToken.None);

        HttpRequestMessage req = Assert.Single(handler.Seen);
        Assert.Contains("attemptsOnly=true", req.RequestUri!.Query);
        Assert.Empty(captures);
    }

    [Fact]
    public async Task RemoteSnapshotCaptureRepository_AddAsync_ThrowsNotSupported()
    {
        (RemoteSnapshotCaptureRepository repo, FakeHandler handler) = Create(_ => Json(0));
        SnapshotCapture capture = SnapshotCapture.Create(DeviceId, From, "fresh", 503, null, null);

        _ = await Assert.ThrowsAsync<NotSupportedException>(() => repo.AddAsync(capture, CancellationToken.None));
        Assert.Empty(handler.Seen);
    }
}
