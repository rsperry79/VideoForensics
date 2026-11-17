using Bunit;

using Microsoft.Extensions.DependencyInjection;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Ui.Shared.Services;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Pages;

public class ImportExportPage_Tests : Bunit.BunitContext
{
    private readonly Mock<IBackupExportService> _export = new();
    private readonly Mock<IFileDialogService> _dialog = new();
    private readonly Mock<IBackupStagingLocator> _staging = new();
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "vf-export-page-" + Guid.NewGuid());

    public ImportExportPage_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddLocalization();
        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        _ = Services.AddScoped(_ => _export.Object);
        _ = Services.AddScoped(_ => _dialog.Object);
        _ = Services.AddScoped(_ => _staging.Object);
        _ = Services.AddScoped(_ => Mock.Of<IBackupImportService>());
        SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", true));
    }

    protected override void Dispose(bool disposing)
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }

        base.Dispose(disposing);
    }

    [Fact]
    public async Task Export_DownloadsOnlyTheArchive_AndShowsItsHash()
    {
        _ = Directory.CreateDirectory(_folder);
        string archive = Path.Combine(_folder, "VideoForensics_Backup_20261115_000000.zip");
        File.WriteAllText(archive, "zip");
        const string hash = "abc123def456";

        _ = _staging.Setup(s => s.GetExportDirectoryAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_folder);
        _ = _export.Setup(e => e.PrepareForExportAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new PrepareExportResult());
        _ = _export.Setup(e => e.ExportBackupAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BackupExportResult { Success = true, ArchivePath = archive, ArchiveSha256Hash = hash });
        _ = _dialog.Setup(d => d.SaveExportedArchiveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        IRenderedComponent<Client.Ui.Shared.Pages.ImportExport> cut = Render<Client.Ui.Shared.Pages.ImportExport>();
        AngleSharp.Dom.IElement button = cut.Find("button.e-primary");
        await cut.InvokeAsync(() => button.Click());

        cut.WaitForAssertion(() => Assert.Contains(hash, cut.Markup));
        _dialog.Verify(d => d.SaveExportedArchiveAsync(archive, Path.GetFileName(archive), It.IsAny<CancellationToken>()), Times.Once);
        _dialog.Verify(d => d.SaveExportedArchiveAsync(It.Is<string>(p => p.EndsWith(".sha256.txt", StringComparison.Ordinal)), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.False(File.Exists(archive + ".sha256.txt"));
    }
}
