using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Ui.Shared.Components.Review;
using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Client.Ui.Shared.Pages;
using VideoForensics.Client.Ui.Shared.Services;
using VideoForensics.Client.Ui.Shared.Services.Review;
using VideoForensics.Client.Ui.Shared.Services.Scope;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Utils.Testing;

using Device = VideoForensics.Data.Common.Entities.Device;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>
/// Shared harness for the review page and its components: the real <see cref="ReviewPage"/> resource strings,
/// a real <see cref="ScopeState"/> and <see cref="ReviewPageState"/> on one fake clock, a paired operator session,
/// and a device repository double that returns <see cref="Devices"/>.
/// </summary>
public abstract class ReviewTestContext : BunitContext
{
    /// <summary>The fixed "now" every review test runs at.</summary>
    protected static readonly DateTime FixedNowUtc = new(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>The devices the repository double returns, in display order.</summary>
    protected List<Device> Devices { get; } = [];

    /// <summary>The global scope the camera selector writes to.</summary>
    protected ScopeState ScopeState { get; }

    /// <summary>The review page state under test.</summary>
    protected ReviewPageState ReviewState { get; }

    /// <summary>The in-memory timeline the timeline tab publishes to and the right-panel timeline reads.</summary>
    protected ReviewTimelineModel TimelineModel { get; } = new();

    /// <summary>The device repository double.</summary>
    protected Mock<IDeviceRepository> DeviceRepositoryMock { get; } = new();

    /// <summary>The clip loader double the timeline tab reads through.</summary>
    protected FakeReviewClipLoader ClipLoader { get; } = new();

    /// <summary>The event loader double the events tab reads through.</summary>
    protected FakeReviewEventLoader EventLoader { get; } = new();

    /// <summary>The events tab's logger, so a test can check what was logged.</summary>
    protected Mock<ILogger<ReviewEventsTab>> EventsLogger { get; } = new();

    /// <summary>The ticketed-URL double behind the review URL cache.</summary>
    protected FakeMediaContentUrlProvider UrlProvider { get; } = new();

    /// <summary>The evidence change feed the timeline tab reloads from.</summary>
    protected FakeEvidenceChangedSource EvidenceSource { get; } = new();

    /// <summary>The timeline tab's logger, so a test can check what was logged.</summary>
    protected Mock<ILogger<ReviewTimelineTab>> TabLogger { get; } = new();

    /// <summary>The page-level timeline feeder's logger, so a test can check what was logged.</summary>
    protected Mock<ILogger<ReviewTimelineFeeder>> FeederLogger { get; } = new();

    /// <summary>The provider accounts the ownership repositories report. Tests add accounts here before rendering.</summary>
    protected List<ProviderAccount> Accounts { get; } = [];

    /// <summary>The locations linked to each provider account. A device belongs to an account through its location.</summary>
    protected Dictionary<Guid, List<Location>> LocationsByAccount { get; } = [];

    /// <summary>When true, the provider-account repository throws, so the ownership load fails.</summary>
    protected bool OwnershipFails { get; set; }

    /// <summary>The page's logger, so a test can check what was logged.</summary>
    protected Mock<ILogger<ReviewPage>> PageLogger { get; } = new();

    /// <summary>The page's localizer, resolved the way the page resolves it.</summary>
    protected IStringLocalizer<ReviewPage> L => Services.GetRequiredService<IStringLocalizer<ReviewPage>>();

    protected ReviewTestContext()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var timeProvider = new FakeTimeProvider(FixedNowUtc);
        ScopeState = new ScopeState(timeProvider);
        ReviewState = new ReviewPageState(timeProvider);

        _ = Services.AddLocalization();
        _ = Services.AddSingleton<TimeProvider>(timeProvider);
        _ = Services.AddScoped(_ => DeviceRepositoryMock.Object);
        _ = Services.AddScoped(_ => ScopeState);
        _ = Services.AddScoped(_ => ReviewState);
        _ = Services.AddScoped(_ => TimelineModel);
        _ = Services.AddScoped<RightPanelContentService>();
        _ = Services.AddSingleton<IReviewClipLoader>(ClipLoader);
        _ = Services.AddSingleton<IMediaContentUrlProvider>(UrlProvider);
        _ = Services.AddScoped(sp => new ReviewUrlCache(sp.GetRequiredService<IMediaContentUrlProvider>(), sp.GetRequiredService<TimeProvider>()));
        _ = Services.AddSingleton<IEvidenceChangedSource>(EvidenceSource);
        _ = Services.AddSingleton<IReviewEventLoader>(EventLoader);
        _ = Services.AddSingleton(EventsLogger.Object);
        _ = Services.AddSingleton(TabLogger.Object);
        _ = Services.AddSingleton(FeederLogger.Object);
        _ = Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));
        _ = Services.AddSingleton(PageLogger.Object);
        Mock<IProviderAccountRepository> accountRepository = ConfigureAccountRepository();
        Mock<ILocationRepository> locationRepository = ConfigureLocationRepository();
        Mock<IUserRepository> userRepository = ConfigureUserRepository();
        _ = Services.AddScoped(_ => accountRepository.Object);
        _ = Services.AddScoped(_ => locationRepository.Object);
        _ = Services.AddScoped(_ => userRepository.Object);
        _ = Services.AddScoped<DeviceOwnershipService>();

        _ = DeviceRepositoryMock
            .Setup(m => m.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Devices.ToList());

        SignInOperatorAsync().GetAwaiter().GetResult();
    }

    /// <summary>Adds a provider account with the given provider name and returns it.</summary>
    protected ProviderAccount AddAccount(string providerName)
    {
        var account = new ProviderAccount
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            ProviderName = providerName,
            LinkedUtc = FixedNowUtc,
        };
        Accounts.Add(account);
        LocationsByAccount[account.Id] = [];
        return account;
    }

    /// <summary>Links a location to an account, so devices at that location belong to the account.</summary>
    protected Location LinkLocation(ProviderAccount account)
    {
        var location = new Location { Id = Guid.NewGuid(), ProviderLocationId = "loc-" + Guid.NewGuid().ToString("N"), Name = "Location" };
        LocationsByAccount[account.Id].Add(location);
        return location;
    }

    private Mock<IProviderAccountRepository> ConfigureAccountRepository()
    {
        var repository = new Mock<IProviderAccountRepository>();
        _ = repository
            .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                if (OwnershipFails)
                {
                    throw new InvalidOperationException("ownership unavailable");
                }

                return (IReadOnlyList<ProviderAccount>)Accounts.ToList();
            });
        return repository;
    }

    private Mock<ILocationRepository> ConfigureLocationRepository()
    {
        var repository = new Mock<ILocationRepository>();
        _ = repository
            .Setup(r => r.ListByProviderAccountIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid accountId, CancellationToken _) => LocationsFor(accountId));
        return repository;
    }

    private IReadOnlyList<Location> LocationsFor(Guid accountId)
    {
        if (LocationsByAccount.TryGetValue(accountId, out List<Location>? locations))
        {
            return locations.ToList();
        }

        return Array.Empty<Location>();
    }

    private static Mock<IUserRepository> ConfigureUserRepository()
    {
        // No user rows: the ownership service labels each account with its provider name.
        var repository = new Mock<IUserRepository>();
        _ = repository
            .Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        return repository;
    }

    /// <summary>Adds <paramref name="count"/> devices named "Camera 1..n" to the repository double.</summary>
    protected void AddDevices(int count)
    {
        for (int i = 0; i < count; i++)
        {
            int number = Devices.Count + 1;
            Devices.Add(new Device
            {
                Id = Guid.NewGuid(),
                ProviderDeviceId = "provider-" + number,
                Name = "Camera " + number,
                Type = "camera",
            });
        }
    }

    /// <summary>The display name the page shows for a device, built the way the page builds it.</summary>
    protected string NameOf(Device device)
    {
        return VideoForensics.Client.Ui.Shared.Services.DeviceLabel.For(device.Name, device.ProviderDeviceId);
    }

    /// <summary>Signs in a paired operator so the page's sign-in gate passes.</summary>
    protected Task SignInOperatorAsync()
    {
        return Services.GetRequiredService<PairedSessionState>().SetAsync("test-token", Guid.NewGuid(), "Review");
    }

    /// <summary>Clears the operator session, so the page shows its sign-in prompt.</summary>
    protected Task SignOutOperatorAsync()
    {
        return Services.GetRequiredService<PairedSessionState>().ClearAsync();
    }

    /// <summary>Navigates the fake browser to the review page with the given query (no leading '?').</summary>
    protected void NavigateToReview(string query = "")
    {
        string uri = query.Length == 0 ? "http://localhost/media/review" : "http://localhost/media/review?" + query;
        Services.GetRequiredService<NavigationManager>().NavigateTo(uri);
    }

    /// <summary>The fake browser's current absolute URI.</summary>
    protected string CurrentUri => Services.GetRequiredService<NavigationManager>().Uri;
}
