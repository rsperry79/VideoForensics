namespace VideoForensics.Ui.Shared.Tests;

using System.Net;
using System.Net.Http.Json;

using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Syncfusion.Blazor.Buttons;
using Syncfusion.Blazor.Inputs;
using Microsoft.JSInterop;

using Moq;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Pages;
using VideoForensics.Ui.Shared.Services;

using Xunit;

/// <summary>
/// SecurityLockoutPolicy.razor's self-HTTP calls must go through the injected
/// <see cref="ISelfApiHttpClientFactory"/> (plan §5.10/§5.12) rather than a raw
/// <c>new HttpClient { BaseAddress = NavigationManager.BaseUri }</c> - otherwise, in the WebApp, the
/// call is an untagged self-call and the server can't tell it apart from a real loopback request,
/// which would incorrectly resolve to Local tier for a remote browser user. This proves the page asks
/// a fake factory for its client and sends its request through the handler THAT factory returned,
/// rather than building its own.
/// </summary>
public class SecurityLockoutPolicyPageTests : BunitContext
{
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();

        /// <summary>Body returned by GET /api/v1/lockout-policy; tests override it to simulate stored auto-ban values.</summary>
        public object PolicyBody { get; set; } = new
        {
            maxFailedAttempts = 5,
            lockoutDurationMinutes = 30,
            blockedCountryCodes = (string?)null,
            failClosedOnLookupError = false,
            autoBanEnabled = false,
            autoBanDistinctAccountThreshold = 3,
            autoBanWindowMinutes = 60,
            autoBanDurationMinutes = 1440
        };

        /// <summary>Body returned by GET /api/v1/banned-ips; defaults to an empty list (no active bans).</summary>
        public object BannedIpsBody { get; set; } = new List<object>();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);

            if (request.RequestUri!.AbsolutePath.EndsWith("/api/v1/banned-ips", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(BannedIpsBody)
                });
            }

            if (request.RequestUri!.AbsolutePath.EndsWith("/two-factor-policy/roles", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new List<object>())
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(PolicyBody)
            });
        }
    }

    /// <summary>
    /// Attaches the current session's bearer token like the real factories do (see
    /// DefaultSelfApiHttpClientFactory/WebAppSelfApiHttpClientFactory) - a page that instead built
    /// its own HttpClient wouldn't send THIS bearer value, since it would come from whatever
    /// PairedSessionState the page itself resolved, not this fake's.
    /// </summary>
    private sealed class FakeSelfApiHttpClientFactory : ISelfApiHttpClientFactory
    {
        private readonly RecordingHandler _handler;
        private readonly PairedSessionState _sessionState;

        public FakeSelfApiHttpClientFactory(RecordingHandler handler, PairedSessionState sessionState)
        {
            _handler = handler;
            _sessionState = sessionState;
        }

        public HttpClient CreateClient() => CreateClient(_sessionState.SessionToken);

        public HttpClient CreateClient(string? bearerToken)
        {
            var client = new HttpClient(_handler) { BaseAddress = new Uri("http://videoforensics.example.com/") };
            if (bearerToken is not null)
            {
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
            }

            return client;
        }
    }

    private readonly RecordingHandler _handler = new();

    public SecurityLockoutPolicyPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        Services.AddLocalization();
    }

    private async Task<PairedSessionState> RegisterSignedInSessionAsync()
    {
        var session = new PairedSessionState(JSInterop.JSRuntime);
        await session.SetAsync("test-token", Guid.NewGuid(), OperatorRole.SuperAdmin.ToString());
        Services.AddScoped(_ => session);
        Services.AddScoped<ISelfApiHttpClientFactory>(_ => new FakeSelfApiHttpClientFactory(_handler, session));
        return session;
    }

    [Fact]
    public async Task PageOpen_SignedIn_SendsRequestsThroughInjectedFactorysClient()
    {
        await RegisterSignedInSessionAsync();

        Render<SecurityLockoutPolicy>();

        // The page reloads on OnAfterRenderAsync(firstRender) after SessionState.EnsureLoadedAsync
        // (see SecurityEventsPageTests' identical note) - both OnInitializedAsync's and
        // OnAfterRenderAsync's loads go through the fake factory's client and its RecordingHandler.
        Assert.Contains(
            _handler.Requests,
            r => r.RequestUri!.AbsolutePath.EndsWith("/api/v1/lockout-policy", StringComparison.Ordinal));
        Assert.Contains(
            _handler.Requests,
            r => r.RequestUri!.AbsolutePath.EndsWith("/two-factor-policy/roles", StringComparison.Ordinal));

        // Every request must carry the bearer token the fake factory's client was set up to send -
        // proof the page used THAT client (built from PairedSessionState) rather than one of its own.
        Assert.All(_handler.Requests, r =>
        {
            Assert.NotNull(r.Headers.Authorization);
            Assert.Equal("test-token", r.Headers.Authorization!.Parameter);
        });
    }

    private static IStringLocalizer<SecurityLockoutPolicy> GetLocalizer(BunitContext ctx) =>
        ctx.Services.GetRequiredService<IStringLocalizer<SecurityLockoutPolicy>>();

    [Fact]
    public async Task SecurityLockoutPolicy_RendersAutoBanInputs_WithLocalizedLabels()
    {
        await RegisterSignedInSessionAsync();
        var localizer = GetLocalizer(this);

        var cut = Render<SecurityLockoutPolicy>();

        cut.WaitForAssertion(() =>
        {
            var markup = cut.Markup;
            Assert.Contains(localizer["AutoBanEnabledLabel"].Value, markup, StringComparison.Ordinal);
            Assert.Contains(localizer["AutoBanDistinctAccountThresholdLabel"].Value, markup, StringComparison.Ordinal);
            Assert.Contains(localizer["AutoBanWindowMinutesLabel"].Value, markup, StringComparison.Ordinal);
            Assert.Contains(localizer["AutoBanDurationMinutesLabel"].Value, markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task SecurityLockoutPolicy_LoadedPolicy_PopulatesAutoBanInputs()
    {
        await RegisterSignedInSessionAsync();
        _handler.PolicyBody = new
        {
            maxFailedAttempts = 5,
            lockoutDurationMinutes = 30,
            blockedCountryCodes = (string?)null,
            failClosedOnLookupError = false,
            autoBanEnabled = true,
            autoBanDistinctAccountThreshold = 7,
            autoBanWindowMinutes = 15,
            autoBanDurationMinutes = 240
        };

        var cut = Render<SecurityLockoutPolicy>();

        cut.WaitForAssertion(() =>
        {
            var numbers = cut.FindComponents<SfNumericTextBox<int>>().Select(c => c.Instance.Value).ToList();
            Assert.Contains(7, numbers);
            Assert.Contains(15, numbers);
            Assert.Contains(240, numbers);

            var checkboxes = cut.FindComponents<SfCheckBox<bool>>().Select(c => c.Instance.Checked).ToList();
            Assert.Contains(true, checkboxes);
        });
    }

    [Fact]
    public async Task SecurityLockoutPolicy_Save_IncludesAutoBanFieldsInBody()
    {
        await RegisterSignedInSessionAsync();
        _handler.PolicyBody = new
        {
            maxFailedAttempts = 5,
            lockoutDurationMinutes = 30,
            blockedCountryCodes = (string?)null,
            failClosedOnLookupError = false,
            autoBanEnabled = true,
            autoBanDistinctAccountThreshold = 7,
            autoBanWindowMinutes = 15,
            autoBanDurationMinutes = 240
        };
        var localizer = GetLocalizer(this);

        var cut = Render<SecurityLockoutPolicy>();
        cut.WaitForAssertion(() => Assert.Contains(localizer["AutoBanEnabledLabel"].Value, cut.Markup, StringComparison.Ordinal));

        var saveButton = cut.FindAll("button").First(b => b.TextContent.Trim() == localizer["SaveButton"].Value);
        saveButton.Click();

        cut.WaitForAssertion(() => Assert.Contains(_handler.Requests, r => r.Method == HttpMethod.Put));
        var put = _handler.Requests.First(r => r.Method == HttpMethod.Put);
        var body = System.Text.Json.JsonDocument.Parse(await put.Content!.ReadAsStringAsync()).RootElement;

        Assert.True(body.GetProperty("autoBanEnabled").GetBoolean());
        Assert.Equal(7, body.GetProperty("autoBanDistinctAccountThreshold").GetInt32());
        Assert.Equal(15, body.GetProperty("autoBanWindowMinutes").GetInt32());
        Assert.Equal(240, body.GetProperty("autoBanDurationMinutes").GetInt32());
    }

    private static object BannedRow(string cidr, string reason, Guid createdBy, bool isAutoBan, DateTime? expiresAtUtc) => new
    {
        id = Guid.NewGuid(),
        cidrRange = cidr,
        reason,
        createdAtUtc = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc),
        expiresAtUtc,
        createdByOperatorId = createdBy,
        isAutoBan
    };

    [Fact]
    public async Task BannedIps_RendersRows_WithRangeAndReason()
    {
        await RegisterSignedInSessionAsync();
        _handler.BannedIpsBody = new List<object>
        {
            BannedRow("203.0.113.0/24", "Known botnet C2 network", Guid.NewGuid(), false, null)
        };

        var cut = Render<SecurityLockoutPolicy>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("203.0.113.0/24", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Known botnet C2 network", cut.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task BannedIps_AutoBadge_ShowsOnlyOnSystemRow()
    {
        await RegisterSignedInSessionAsync();
        var localizer = GetLocalizer(this);
        _handler.BannedIpsBody = new List<object>
        {
            BannedRow("198.51.100.7/32", "Auto-ban: 3 accounts locked in 60 min", new Guid("00000000-0000-0000-0000-00000000f2b1"), true, DateTime.UtcNow.AddHours(24)),
            BannedRow("203.0.113.9/32", "Manual", Guid.NewGuid(), false, null)
        };

        var cut = Render<SecurityLockoutPolicy>();

        cut.WaitForAssertion(() =>
        {
            var badge = localizer["BannedIpsAutoBadge"].Value;
            var rows = cut.FindAll("tbody tr");
            var autoRows = rows.Where(r => r.TextContent.Contains(badge, StringComparison.Ordinal)).ToList();
            Assert.Single(autoRows);
            Assert.Contains("198.51.100.7/32", autoRows[0].TextContent, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task BannedIps_NoActiveBans_ShowsLocalizedEmptyState()
    {
        await RegisterSignedInSessionAsync();
        var localizer = GetLocalizer(this);
        _handler.BannedIpsBody = new List<object>();

        var cut = Render<SecurityLockoutPolicy>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(localizer["BannedIpsEmpty"].Value, cut.Markup, StringComparison.Ordinal);
        });
    }
}
