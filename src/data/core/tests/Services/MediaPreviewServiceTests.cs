using System.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using VideoForensics.Core.Providers.Core;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Services;

using Xunit;

namespace VideoForensics.Data.Core.Tests;

public class MediaPreviewServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vf-preview-" + Guid.NewGuid());
    private readonly Mock<IMediaItemRepository> _items = new();
    private readonly FakePreviewRunner _runner = new();

    public MediaPreviewServiceTests()
    {
        _ = Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private MediaPreviewService CreateService()
    {
        return new(_items.Object, new FfmpegToolLocation(), NullLogger<MediaPreviewService>.Instance, _runner);
    }

    [Fact]
    public async Task EnsurePreview_ExistingNonEmptyPreview_ReturnsTrueWithoutRunningFfmpeg()
    {
        string media = WriteMedia("clip.mp4");
        string preview = MediaPreviewService.PreviewPathFor(media);
        File.WriteAllBytes(preview, new byte[] { 0x00, 0x00, 0x00, 0x18 });
        MediaItem row = MediaFor(media);

        bool ok = await CreateService().EnsurePreviewAsync(row, CancellationToken.None);

        Assert.True(ok);
        Assert.Empty(_runner.Calls);
        Assert.Equal(preview, row.PreviewPath);
        _items.Verify(r => r.UpdateAsync(row, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnsurePreview_FfmpegSucceeds_WritesPreviewAndSetsPath()
    {
        string media = WriteMedia("clip.mp4");
        string preview = MediaPreviewService.PreviewPathFor(media);
        _runner.Behavior = (startInfo, _) =>
        {
            File.WriteAllBytes(startInfo.ArgumentList.Last(), new byte[] { 0x00, 0x00, 0x00, 0x18 });
            return Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
        };
        MediaItem row = MediaFor(media);

        bool ok = await CreateService().EnsurePreviewAsync(row, CancellationToken.None);

        Assert.True(ok);
        Assert.True(File.Exists(preview));
        Assert.Equal(preview, row.PreviewPath);
        Assert.False(File.Exists(preview + ".tmp"));
        _items.Verify(r => r.UpdateAsync(row, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnsurePreview_FfmpegFails_ReturnsFalseAndLeavesNoFile()
    {
        string media = WriteMedia("clip.mp4");
        string preview = MediaPreviewService.PreviewPathFor(media);
        _runner.Behavior = (startInfo, _) =>
        {
            // A partial temp file is left behind on failure; the service must remove it.
            File.WriteAllBytes(startInfo.ArgumentList.Last(), new byte[] { 0x01 });
            return Task.FromResult(new ProcessResult(1, string.Empty, "boom"));
        };
        MediaItem row = MediaFor(media);

        bool ok = await CreateService().EnsurePreviewAsync(row, CancellationToken.None);

        Assert.False(ok);
        Assert.False(File.Exists(preview));
        Assert.False(File.Exists(preview + ".tmp"));
        Assert.Null(row.PreviewPath);
    }

    [Fact]
    public async Task EnsurePreview_FfmpegFailsWithStderr_LogsTheReasonFfmpegGave()
    {
        // ffmpeg's error text names the file it could not open, and it is the only clue in the log.
        string media = WriteMedia("clip.mp4");
        _runner.Behavior = (_, _) => Task.FromResult(new ProcessResult(-13, string.Empty, "banner line\nFailed to open output: Permission denied"));
        MediaItem row = MediaFor(media);
        var logger = new Mock<ILogger<MediaPreviewService>>();
        var service = new MediaPreviewService(_items.Object, new FfmpegToolLocation(), logger.Object, _runner);

        _ = await service.EnsurePreviewAsync(row, CancellationToken.None);

        logger.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("Permission denied", StringComparison.Ordinal)),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task EnsurePreview_Cancelled_Throws()
    {
        string media = WriteMedia("clip.mp4");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        MediaItem row = MediaFor(media);

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() => CreateService().EnsurePreviewAsync(row, cts.Token));

        Assert.Empty(_runner.Calls);
    }

    private string WriteMedia(string name)
    {
        string path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, new byte[] { 0x00, 0x01 });
        return path;
    }

    private static MediaItem MediaFor(string path)
    {
        return new()
        {
            Id = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            FileName = Path.GetFileName(path),
            FilePath = path,
            MediaFormat = "video/mp4",
            Sha256Hash = "hash",
        };
    }

    /// <summary>Records each call and runs a scripted behavior; honours the token the way <see cref="ProcessRunner"/> does.</summary>
    private sealed class FakePreviewRunner : IPreviewProcessRunner
    {
        public List<ProcessStartInfo> Calls { get; } = new();

        public Func<ProcessStartInfo, CancellationToken, Task<ProcessResult>> Behavior { get; set; } =
            (_, _) => Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));

        public Task<ProcessResult> RunAsync(ProcessStartInfo startInfo, TimeSpan timeout, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Calls.Add(startInfo);
            return Behavior(startInfo, ct);
        }
    }
}
