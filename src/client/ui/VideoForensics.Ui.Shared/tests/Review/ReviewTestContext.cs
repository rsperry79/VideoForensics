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
using VideoForensics.Client.Ui.Shared.Services.Cases;
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

    /// <summary>The device health change feed the detail tab re-queries jamming and anomalies from.</summary>
    protected FakeDeviceHealthChangedSource HealthSource { get; } = new();

    /// <summary>The detail loader double the detail tab reads through.</summary>
    protected FakeReviewDetailLoader DetailLoader { get; } = new();

    /// <summary>The detail tab's logger, so a test can check what was logged.</summary>
    protected Mock<ILogger<ReviewDetailTab>> DetailLogger { get; } = new();

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

    /// <summary>The Actions menu's logger, so a test can check what was logged.</summary>
    protected Mock<ILogger<ReviewActionsMenu>> ActionsLogger { get; } = new();

    /// <summary>The legal hold repository double the Actions menu places and releases holds through.</summary>
    protected Mock<ILegalHoldRepository> LegalHoldRepositoryMock { get; } = new();

    /// <summary>The integrity verification double the Actions menu runs through.</summary>
    protected Mock<IEvidenceValidationService> EvidenceValidationMock { get; } = new();

    /// <summary>The case repository the case state and the tagger read and pin through. Set before the first render that uses them.</summary>
    protected ICaseRepository CaseRepositoryDouble { get; set; } = new Mock<ICaseRepository>().Object;

    /// <summary>The still-capture service the timeline tab's Grab still button calls. Succeeds by default.</summary>
    protected Mock<IMediaStillCaptureService> StillCaptureMock { get; } = new();

    /// <summary>The page's logger, so a test can check what was logged.</summary>
    protected Mock<ILogger<ReviewPage>> PageLogger { get; } = new();

    /// <summary>The media item repository double the case view reads pinned clips through.</summary>
    protected Mock<IMediaItemRepository> MediaItemRepositoryMock { get; } = new();

    /// <summary>The event repository double the case view reads pinned events through.</summary>
    protected Mock<IEventRepository> EventRepositoryMock { get; } = new();

    private readonly Dictionary<Guid, MediaItem> _mediaById = [];
    private readonly Dictionary<Guid, Event> _eventsById = [];

    /// <summary>The case view service, resolved from the same scope the page uses.</summary>
    protected ReviewCaseMode CaseMode => Services.GetRequiredService<ReviewCaseMode>();

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
        _ = Services.AddScoped<ReviewPopupState>();
        _ = Services.AddScoped<PendingEvidenceTags>();
        _ = Services.AddScoped(_ => TimelineModel);
        _ = Services.AddScoped<RightPanelContentService>();
        _ = Services.AddSingleton<IReviewClipLoader>(ClipLoader);
        _ = Services.AddSingleton<IMediaContentUrlProvider>(UrlProvider);
        _ = Services.AddScoped(sp => new ReviewUrlCache(sp.GetRequiredService<IMediaContentUrlProvider>(), sp.GetRequiredService<TimeProvider>()));
        _ = Services.AddSingleton<IEvidenceChangedSource>(EvidenceSource);
        _ = Services.AddSingleton<IReviewEventLoader>(EventLoader);
        _ = Services.AddSingleton(EventsLogger.Object);
        _ = Services.AddSingleton<IDeviceHealthChangedSource>(HealthSource);
        _ = Services.AddSingleton<IReviewDetailLoader>(DetailLoader);
        _ = Services.AddSingleton(DetailLogger.Object);
        _ = Services.AddSingleton(TabLogger.Object);
        _ = Services.AddSingleton(FeederLogger.Object);
        _ = Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));
        _ = Services.AddSingleton(PageLogger.Object);
        _ = Services.AddSingleton(ActionsLogger.Object);
        _ = Services.AddScoped(_ => LegalHoldRepositoryMock.Object);
        _ = Services.AddScoped(_ => EvidenceValidationMock.Object);
        _ = Services.AddScoped<ICaseRepository>(_ => CaseRepositoryDouble);
        _ = Services.AddScoped<CaseState>();
        _ = Services.AddScoped<ReviewEvidenceTagger>();
        _ = Services.AddScoped<ReviewSelection>();
        _ = Services.AddScoped(_ => MediaItemRepositoryMock.Object);
        _ = Services.AddScoped(_ => EventRepositoryMock.Object);
        _ = Services.AddScoped(sp => new ReviewCaseMode(
            sp.GetRequiredService<ICaseRepository>(),
            sp.GetRequiredService<IMediaItemRepository>(),
            sp.GetRequiredService<IEventRepository>()));
        _ = MediaItemRepositoryMock
            .Setup(m => m.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid id, CancellationToken _) => Task.FromResult<MediaItem?>(_mediaById.GetValueOrDefault(id)));
        _ = EventRepositoryMock
            .Setup(m => m.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns((Guid id, CancellationToken _) => Task.FromResult<Event?>(_eventsById.GetValueOrDefault(id)));
        _ = Services.AddScoped(_ => StillCaptureMock.Object);
        _ = StillCaptureMock
            .Setup(m => m.CaptureStillAsync(It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => MediaStillCaptureResult.Ok(new MediaStillInfo
            {
                Id = Guid.NewGuid(),
                SourceMediaItemId = Guid.NewGuid(),
                FrameOffsetMs = 0,
                Sha256Hash = "0000000000000000",
                SourceSha256AtCapture = "source",
                CapturedByOperator = "operator",
                CaptureMethod = "Server",
            }));
        _ = LegalHoldRepositoryMock
            .Setup(m => m.GetActiveByMediaItemIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LegalHold>());
        _ = EvidenceValidationMock
            .Setup(m => m.VerifyLocalIntegrityAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>(), It.IsAny<IProgress<string>?>()))
            .ReturnsAsync(new List<MediaVerificationResult>());
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

    /// <summary>
    /// Creates a case whose pins are the given media items and events, wires the case, pin, and row doubles, and
    /// returns the case id. Set the case repository double before rendering anything that resolves the case view.
    /// </summary>
    protected Guid ArrangePinnedCase(string title, IReadOnlyList<MediaItem> media, IReadOnlyList<Event>? events = null)
    {
        Guid caseId = Guid.NewGuid();
        var pins = new List<CaseItem>();
        foreach (MediaItem item in media)
        {
            _mediaById[item.Id] = item;
            pins.Add(new CaseItem { Id = Guid.NewGuid(), CaseId = caseId, Kind = CaseItemKind.Media, MediaItemId = item.Id, Reason = "r", AddedBy = "op", AddedAtUtc = FixedNowUtc });
        }

        foreach (Event evt in events ?? [])
        {
            _eventsById[evt.Id] = evt;
            pins.Add(new CaseItem { Id = Guid.NewGuid(), CaseId = caseId, Kind = CaseItemKind.Event, EventId = evt.Id, Reason = "r", AddedBy = "op", AddedAtUtc = FixedNowUtc });
        }

        var caseRepository = new Mock<ICaseRepository>();
        _ = caseRepository
            .Setup(r => r.GetAsync(caseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ForensicCase { Id = caseId, CaseNumber = "CASE-1", Title = title, CreatedBy = "op" });
        _ = caseRepository
            .Setup(r => r.ListItemsAsync(caseId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pins);
        CaseRepositoryDouble = caseRepository.Object;
        return caseId;
    }

    /// <summary>A clip on <paramref name="device"/> recorded at <paramref name="recordedUtc"/>, lasting one minute.</summary>
    protected static MediaItem PinnableClip(Guid device, DateTime recordedUtc)
    {
        return new MediaItem
        {
            Id = Guid.NewGuid(),
            DeviceId = device,
            FileName = "clip.mp4",
            FilePath = "clip.mp4",
            MediaFormat = "mp4",
            RecordedAtUtc = recordedUtc,
            DurationSeconds = 60,
            Sha256Hash = "hash",
        };
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
        return SignInOperatorAsync("Review");
    }

    /// <summary>Signs in a paired operator with the given role and returns the operator's id.</summary>
    protected async Task<Guid> SignInOperatorAsAsync(string role)
    {
        Guid operatorId = Guid.NewGuid();
        await Services.GetRequiredService<PairedSessionState>().SetAsync("test-token", operatorId, role);
        return operatorId;
    }

    /// <summary>Signs in a paired operator with the given role.</summary>
    protected Task SignInOperatorAsync(string role)
    {
        return SignInOperatorAsAsync(role);
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
