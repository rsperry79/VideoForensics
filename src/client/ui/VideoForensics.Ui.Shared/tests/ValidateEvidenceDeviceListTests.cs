
using Bunit;
using Bunit.Rendering;

using Microsoft.Extensions.DependencyInjection;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Ui.Shared.Services;

using Xunit;

using Device = VideoForensics.Data.Common.Entities.Device;
using ValidateEvidencePage = VideoForensics.Client.Ui.Shared.Pages.ValidateEvidence;

namespace VideoForensics.Client.Ui.Shared.Tests;
/// <summary>Verify starts one integrity job over every device the filter leaves visible.</summary>
public class ValidateEvidence_DeviceList_Tests : OperatorSessionGateTestBase
{
    private readonly Mock<IEvidenceValidationJobs> _jobs = new(MockBehavior.Loose);

    public ValidateEvidence_DeviceList_Tests()
    {
        _ = Services.AddScoped(_ => _jobs.Object);
    }

    [Fact]
    public async Task EmbeddedOnAccount_Verify_StartsJobOverOnlyThatAccountsDevices()
    {
        var account = new ProviderAccount { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), ProviderName = "Ring", LinkedUtc = DateTime.UtcNow };
        var ownLocation = new Location { Id = Guid.NewGuid(), ProviderLocationId = "O", Name = "Own" };
        var ownDevice = new Device { Id = Guid.NewGuid(), LocationId = ownLocation.Id, ProviderDeviceId = "o", Name = "Own", Type = "camera" };
        var otherDevice = new Device { Id = Guid.NewGuid(), LocationId = Guid.NewGuid(), ProviderDeviceId = "x", Name = "Other", Type = "camera" };

        var deviceRepository = new Mock<IDeviceRepository>();
        _ = deviceRepository.Setup(m => m.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([ownDevice, otherDevice]);
        _ = deviceRepository.Setup(m => m.GetByLocationIdAsync(ownLocation.Id, It.IsAny<CancellationToken>())).ReturnsAsync([ownDevice]);
        _ = Services.AddScoped(_ => deviceRepository.Object);
        ScopeOwnershipTestDoubles.Register(
            Services,
            new[] { account },
            new Dictionary<Guid, IReadOnlyList<Location>> { [account.Id] = new[] { ownLocation } });
        var locationRepository = new Mock<ILocationRepository>();
        _ = locationRepository.Setup(m => m.ListByProviderAccountIdAsync(account.Id, It.IsAny<CancellationToken>())).ReturnsAsync([ownLocation]);
        _ = Services.AddScoped(_ => locationRepository.Object);
        await SignInOperatorAsync();

        IRenderedComponent<ValidateEvidencePage> page = Render<ValidateEvidencePage>(p => p.Add(c => c.AccountId, account.Id));
        page.FindAll("button.e-btn").Single(b => b.TextContent.Trim() == "Run local validation").Click();

        // The account's devices are passed explicitly: a null target would mean every device in the system.
        _jobs.Verify(
            m => m.StartIntegrityForDevicesAsync(
                It.Is<IReadOnlyList<Guid>>(ids => ids.SequenceEqual(new[] { ownDevice.Id })),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _jobs.Verify(m => m.StartIntegrityAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Verify_WithProviderFilter_StartsOneJobOverTheVisibleDevices()
    {
        var account = new ProviderAccount { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), ProviderName = "Ring", LinkedUtc = DateTime.UtcNow };
        var frontLocation = new Location { Id = Guid.NewGuid(), ProviderLocationId = "F", Name = "Front" };
        var backLocation = new Location { Id = Guid.NewGuid(), ProviderLocationId = "B", Name = "Back" };
        var front = new Device { Id = Guid.NewGuid(), LocationId = frontLocation.Id, ProviderDeviceId = "f", Name = "Front", Type = "camera" };
        var back = new Device { Id = Guid.NewGuid(), LocationId = backLocation.Id, ProviderDeviceId = "b", Name = "Back", Type = "camera" };

        var deviceRepository = new Mock<IDeviceRepository>();
        _ = deviceRepository.Setup(m => m.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([front, back]);
        _ = Services.AddScoped(_ => deviceRepository.Object);
        ScopeOwnershipTestDoubles.Register(
            Services,
            new[] { account },
            new Dictionary<Guid, IReadOnlyList<Location>> { [account.Id] = new[] { frontLocation, backLocation } });
        await SignInOperatorAsync();

        IRenderedComponent<ValidateEvidencePage> page = Render<ValidateEvidencePage>();
        IRenderedComponent<ContainerFragment> panel = Render(Services.GetRequiredService<RightPanelContentService>().Content!);
        panel.Find("[data-testid='provider-filter']").Change("Ring");

        // The integrity tab is also labelled "Validate locally"; the action button is the one labelled exactly "Verify".
        AngleSharp.Dom.IElement verify = page.FindAll("button.e-btn").Single(b => b.TextContent.Trim() == "Run local validation");
        verify.Click();

        _jobs.Verify(
            m => m.StartIntegrityForDevicesAsync(
                It.Is<IReadOnlyList<Guid>>(ids => ids.SequenceEqual(new[] { front.Id, back.Id })),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _jobs.Verify(m => m.StartIntegrityAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SwitchingTabs_ResetsTheProviderFilter_SoVerifyCoversEveryDevice()
    {
        var account = new ProviderAccount { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), ProviderName = "Ring", LinkedUtc = DateTime.UtcNow };
        var frontLocation = new Location { Id = Guid.NewGuid(), ProviderLocationId = "F", Name = "Front" };
        var backLocation = new Location { Id = Guid.NewGuid(), ProviderLocationId = "B", Name = "Back" };
        var front = new Device { Id = Guid.NewGuid(), LocationId = frontLocation.Id, ProviderDeviceId = "f", Name = "Front", Type = "camera" };
        var back = new Device { Id = Guid.NewGuid(), LocationId = backLocation.Id, ProviderDeviceId = "b", Name = "Back", Type = "camera" };

        var deviceRepository = new Mock<IDeviceRepository>();
        _ = deviceRepository.Setup(m => m.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([front, back]);
        _ = Services.AddScoped(_ => deviceRepository.Object);
        // The filter only reaches Front, so a stale filter would verify one device; a reset verifies both.
        ScopeOwnershipTestDoubles.Register(
            Services,
            new[] { account },
            new Dictionary<Guid, IReadOnlyList<Location>> { [account.Id] = new[] { frontLocation } });
        await SignInOperatorAsync();

        IRenderedComponent<ValidateEvidencePage> page = Render<ValidateEvidencePage>();
        IRenderedComponent<ContainerFragment> panel = Render(Services.GetRequiredService<RightPanelContentService>().Content!);
        panel.Find("[data-testid='provider-filter']").Change("Ring");

        // Leave the integrity tab and come back: the controls are rebuilt at "all", and the page must agree.
        page.FindAll("button.e-btn").Single(b => b.TextContent.Trim() == "Validate remote").Click();
        page.FindAll("button.e-btn").Single(b => b.TextContent.Trim() == "Validate locally").Click();
        page.FindAll("button.e-btn").Single(b => b.TextContent.Trim() == "Run local validation").Click();

        _jobs.Verify(m => m.StartIntegrityAsync(null, It.IsAny<CancellationToken>()), Times.Once);
        _jobs.Verify(m => m.StartIntegrityForDevicesAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
