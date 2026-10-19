namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;
using Xunit;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Data.Core.Models;
using VideoForensics.Ui.Shared.Pages;
using VideoForensics.Ui.Shared.Services;
using VideoForensics.Ui.Shared.Services.Scope;
using Device = VideoForensics.Data.Common.Entities.Device;

/// <summary>Filtering to several devices yields one combined audit list and one verdict covering all of them.</summary>
public class ChainOfCustody_Combined_Tests : BunitContext
{
    private readonly Mock<IDeviceRepository> _deviceRepository = new();
    private readonly Mock<IReportGenerationService> _reportService = new();

    public ChainOfCustody_Combined_Tests()
    {
        // Registrations happen here, before anything resolves a service: bUnit rejects late registrations.
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLocalization();
        Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        Services.AddScoped(_ => _deviceRepository.Object);
        Services.AddScoped(_ => _reportService.Object);
        Services.AddScoped<RightPanelContentService>();
        Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));
    }

    [Fact]
    public async Task FilteredToTwoDevices_ShowsOneVerdictNamingTheFailingDevice()
    {
        var account = new ProviderAccount { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), ProviderName = "Ring", LinkedUtc = DateTime.UtcNow };
        var frontLocation = new Location { Id = Guid.NewGuid(), ProviderLocationId = "F", Name = "Front" };
        var backLocation = new Location { Id = Guid.NewGuid(), ProviderLocationId = "B", Name = "Back" };
        var front = new Device { Id = Guid.NewGuid(), LocationId = frontLocation.Id, ProviderDeviceId = "f", Name = "Front", Type = "camera" };
        var back = new Device { Id = Guid.NewGuid(), LocationId = backLocation.Id, ProviderDeviceId = "b", Name = "Back", Type = "camera" };

        _deviceRepository
            .Setup(m => m.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Device> { front, back });
        // The page first loads the unfiltered whole-database report before the filter is applied.
        _reportService
            .Setup(m => m.BuildChainOfCustodyReportAsync(null, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChainOfCustodyReport { ChainIntegrityVerified = true });
        _reportService
            .Setup(m => m.BuildChainOfCustodyReportAsync(front.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChainOfCustodyReport { ChainIntegrityVerified = true });
        _reportService
            .Setup(m => m.BuildChainOfCustodyReportAsync(back.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChainOfCustodyReport { ChainIntegrityVerified = false, ChainVerificationStatus = "hash mismatch" });

        ScopeOwnershipTestDoubles.Register(
            Services,
            new[] { account },
            new Dictionary<Guid, IReadOnlyList<Location>> { [account.Id] = new[] { frontLocation, backLocation } });
        await Services.GetRequiredService<PairedSessionState>().SetAsync("test-token", Guid.NewGuid(), "Review");

        var page = Render<ChainOfCustody>();

        // The filter lives in the right panel, which the page hands to RightPanelContentService. Rendering that
        // fragment keeps its callbacks bound to the page under test.
        var panelContent = Services.GetRequiredService<RightPanelContentService>().Content;
        var panel = Render(panelContent!);
        panel.Find("[data-testid='provider-filter']").Change("Ring");
        var component = page;

        // One verdict for the whole selection, not one per device, and it names the device that failed.
        var alert = Assert.Single(component.FindAll(".alert-danger"));
        Assert.Contains("Back: hash mismatch", alert.TextContent);
        Assert.Empty(component.FindAll(".alert-success"));
        _reportService.Verify(
            m => m.BuildChainOfCustodyReportAsync(front.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _reportService.Verify(
            m => m.BuildChainOfCustodyReportAsync(back.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
