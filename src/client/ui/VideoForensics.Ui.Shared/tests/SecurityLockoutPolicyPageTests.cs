
using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using Syncfusion.Blazor.Buttons;
using Syncfusion.Blazor.Inputs;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Pages;
using VideoForensics.Ui.Shared.Services;

using Xunit;

namespace VideoForensics.Ui.Shared.Tests;
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
        public List<HttpRequestMessage> Requests { get; } = [];

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

        /// <summary>Status returned for DELETE /api/v1/banned-ips/{id}; tests set a failure to exercise the error path.</summary>
        public HttpStatusCode DeleteStatus { get; set; } = HttpStatusCode.NoContent;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);

            return request.Method == HttpMethod.Delete
                ? Task.FromResult(new HttpResponseMessage(DeleteStatus))
                : request.RequestUri!.AbsolutePath.EndsWith("/api/v1/banned-ips", StringComparison.Ordinal)
                ? Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(BannedIpsBody)
                })
                : request.RequestUri!.AbsolutePath.EndsWith("/two-factor-policy/roles", StringComparison.Ordinal)
                ? Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new List<object>())
                })
                : Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
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

        public HttpClient CreateClient()
        {
            return CreateClient(_sessionState.SessionToken);
        }

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

        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        _ = Services.AddLocalization();
    }

    private async Task<PairedSessionState> RegisterSignedInSessionAsync()
    {
        var session = new PairedSessionState(JSInterop.JSRuntime);
        await session.SetAsync("test-token", Guid.NewGuid(), OperatorRole.SuperAdmin.ToString());
        _ = Services.AddScoped(_ => session);
        _ = Services.AddScoped<ISelfApiHttpClientFactory>(_ => new FakeSelfApiHttpClientFactory(_handler, session));
        return session;
    }

    [Fact]
    public async Task PageOpen_SignedIn_SendsRequestsThroughInjectedFactorysClient()
    {
        _ = await RegisterSignedInSessionAsync();

        _ = Render<SecurityLockoutPolicy>();

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

    private static IStringLocalizer<SecurityLockoutPolicy> GetLocalizer(BunitContext ctx)
    {
        return ctx.Services.GetRequiredService<IStringLocalizer<SecurityLockoutPolicy>>();
    }

    [Fact]
    public async Task SecurityLockoutPolicy_RendersAutoBanInputs_WithLocalizedLabels()
    {
        _ = await RegisterSignedInSessionAsync();
        IStringLocalizer<SecurityLockoutPolicy> localizer = GetLocalizer(this);

        IRenderedComponent<SecurityLockoutPolicy> cut = Render<SecurityLockoutPolicy>();

        cut.WaitForAssertion(() =>
        {
            string markup = cut.Markup;
            Assert.Contains(localizer["AutoBanEnabledLabel"].Value, markup, StringComparison.Ordinal);
            Assert.Contains(localizer["AutoBanDistinctAccountThresholdLabel"].Value, markup, StringComparison.Ordinal);
            Assert.Contains(localizer["AutoBanWindowMinutesLabel"].Value, markup, StringComparison.Ordinal);
            Assert.Contains(localizer["AutoBanDurationMinutesLabel"].Value, markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task SecurityLockoutPolicy_LoadedPolicy_PopulatesAutoBanInputs()
    {
        _ = await RegisterSignedInSessionAsync();
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

        IRenderedComponent<SecurityLockoutPolicy> cut = Render<SecurityLockoutPolicy>();

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
        _ = await RegisterSignedInSessionAsync();
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
        IStringLocalizer<SecurityLockoutPolicy> localizer = GetLocalizer(this);

        IRenderedComponent<SecurityLockoutPolicy> cut = Render<SecurityLockoutPolicy>();
        cut.WaitForAssertion(() => Assert.Contains(localizer["AutoBanEnabledLabel"].Value, cut.Markup, StringComparison.Ordinal));

        AngleSharp.Dom.IElement saveButton = cut.FindAll("button").First(b => b.TextContent.Trim() == localizer["SaveButton"].Value);
        saveButton.Click();

        cut.WaitForAssertion(() => Assert.Contains(_handler.Requests, r => r.Method == HttpMethod.Put));
        HttpRequestMessage put = _handler.Requests.First(r => r.Method == HttpMethod.Put);
        JsonElement body = System.Text.Json.JsonDocument.Parse(await put.Content!.ReadAsStringAsync()).RootElement;

        Assert.True(body.GetProperty("autoBanEnabled").GetBoolean());
        Assert.Equal(7, body.GetProperty("autoBanDistinctAccountThreshold").GetInt32());
        Assert.Equal(15, body.GetProperty("autoBanWindowMinutes").GetInt32());
        Assert.Equal(240, body.GetProperty("autoBanDurationMinutes").GetInt32());
    }

    private static object BannedRow(string cidr, string reason, Guid createdBy, bool isAutoBan, DateTime? expiresAtUtc, Guid? id = null)
    {
        return new
        {
            id = id ?? Guid.NewGuid(),
            cidrRange = cidr,
            reason,
            createdAtUtc = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc),
            expiresAtUtc,
            createdByOperatorId = createdBy,
            isAutoBan
        };
    }

    [Fact]
    public async Task BannedIps_RendersRows_WithRangeAndReason()
    {
        _ = await RegisterSignedInSessionAsync();
        _handler.BannedIpsBody = new List<object>
        {
            BannedRow("203.0.113.0/24", "Known botnet C2 network", Guid.NewGuid(), false, null)
        };

        IRenderedComponent<SecurityLockoutPolicy> cut = Render<SecurityLockoutPolicy>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("203.0.113.0/24", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Known botnet C2 network", cut.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task BannedIps_AutoBadge_ShowsOnlyOnSystemRow()
    {
        _ = await RegisterSignedInSessionAsync();
        IStringLocalizer<SecurityLockoutPolicy> localizer = GetLocalizer(this);
        _handler.BannedIpsBody = new List<object>
        {
            BannedRow("198.51.100.7/32", "Auto-ban: 3 accounts locked in 60 min", new Guid("00000000-0000-0000-0000-00000000f2b1"), true, DateTime.UtcNow.AddHours(24)),
            BannedRow("203.0.113.9/32", "Manual", Guid.NewGuid(), false, null)
        };

        IRenderedComponent<SecurityLockoutPolicy> cut = Render<SecurityLockoutPolicy>();

        cut.WaitForAssertion(() =>
        {
            string badge = localizer["BannedIpsAutoBadge"].Value;
            IReadOnlyList<AngleSharp.Dom.IElement> rows = cut.FindAll("tbody tr");
            var autoRows = rows.Where(r => r.TextContent.Contains(badge, StringComparison.Ordinal)).ToList();
            _ = Assert.Single(autoRows);
            Assert.Contains("198.51.100.7/32", autoRows[0].TextContent, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task BannedIps_NoActiveBans_ShowsLocalizedEmptyState()
    {
        _ = await RegisterSignedInSessionAsync();
        IStringLocalizer<SecurityLockoutPolicy> localizer = GetLocalizer(this);
        _handler.BannedIpsBody = new List<object>();

        IRenderedComponent<SecurityLockoutPolicy> cut = Render<SecurityLockoutPolicy>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(localizer["BannedIpsEmpty"].Value, cut.Markup, StringComparison.Ordinal);
        });
    }

    private const string UnbanCidr = "203.0.113.9/32";

    private static IEnumerable<AngleSharp.Dom.IElement> ButtonsWithText(IRenderedComponent<SecurityLockoutPolicy> cut, string text)
    {
        return cut.FindAll("button").Where(b => b.TextContent.Trim() == text);
    }

    private static IEnumerable<AngleSharp.Dom.IElement> RowsContaining(IRenderedComponent<SecurityLockoutPolicy> cut, string text)
    {
        return cut.FindAll("tbody tr").Where(r => r.TextContent.Contains(text, StringComparison.Ordinal));
    }

    private async Task<(Guid BanId, IRenderedComponent<SecurityLockoutPolicy> Cut)> RenderWithManualBanAsync()
    {
        _ = await RegisterSignedInSessionAsync();
        var banId = Guid.NewGuid();
        _handler.BannedIpsBody = new List<object>
        {
            BannedRow(UnbanCidr, "Manual", Guid.NewGuid(), false, null, banId)
        };

        IRenderedComponent<SecurityLockoutPolicy> cut = Render<SecurityLockoutPolicy>();
        cut.WaitForAssertion(() => Assert.Contains(UnbanCidr, cut.Markup, StringComparison.Ordinal));
        return (banId, cut);
    }

    [Fact]
    public async Task BannedIps_UnbanClick_ShowsLocalizedConfirmStepWithoutSendingDelete()
    {
        (_, IRenderedComponent<SecurityLockoutPolicy>? cut) = await RenderWithManualBanAsync();
        IStringLocalizer<SecurityLockoutPolicy> localizer = GetLocalizer(this);

        ButtonsWithText(cut, localizer["UnbanButton"].Value).Single().Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(localizer["UnbanConfirmMessage", UnbanCidr].Value, cut.Markup, StringComparison.Ordinal);
            _ = Assert.Single(ButtonsWithText(cut, localizer["UnbanConfirmButton"].Value));
            _ = Assert.Single(ButtonsWithText(cut, localizer["UnbanCancelButton"].Value));
        });
        Assert.DoesNotContain(_handler.Requests, r => r.Method == HttpMethod.Delete);
    }

    [Fact]
    public async Task BannedIps_UnbanConfirm_SendsDeleteAndRemovesRow()
    {
        (Guid banId, IRenderedComponent<SecurityLockoutPolicy>? cut) = await RenderWithManualBanAsync();
        IStringLocalizer<SecurityLockoutPolicy> localizer = GetLocalizer(this);

        ButtonsWithText(cut, localizer["UnbanButton"].Value).Single().Click();
        cut.WaitForAssertion(() => Assert.Single(ButtonsWithText(cut, localizer["UnbanConfirmButton"].Value)));
        ButtonsWithText(cut, localizer["UnbanConfirmButton"].Value).Single().Click();

        cut.WaitForAssertion(() => Assert.Empty(RowsContaining(cut, UnbanCidr)));
        HttpRequestMessage delete = Assert.Single(_handler.Requests, r => r.Method == HttpMethod.Delete);
        Assert.EndsWith($"/api/v1/banned-ips/{banId}", delete.RequestUri!.AbsolutePath, StringComparison.Ordinal);
        Assert.Contains(localizer["UnbanSucceeded"].Value, cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BannedIps_UnbanCancel_LeavesRowAndSendsNothing()
    {
        (_, IRenderedComponent<SecurityLockoutPolicy>? cut) = await RenderWithManualBanAsync();
        IStringLocalizer<SecurityLockoutPolicy> localizer = GetLocalizer(this);

        ButtonsWithText(cut, localizer["UnbanButton"].Value).Single().Click();
        cut.WaitForAssertion(() => Assert.Single(ButtonsWithText(cut, localizer["UnbanCancelButton"].Value)));
        ButtonsWithText(cut, localizer["UnbanCancelButton"].Value).Single().Click();

        cut.WaitForAssertion(() => Assert.Empty(ButtonsWithText(cut, localizer["UnbanConfirmButton"].Value)));
        _ = Assert.Single(RowsContaining(cut, UnbanCidr));
        Assert.DoesNotContain(_handler.Requests, r => r.Method == HttpMethod.Delete);
    }

    [Fact]
    public async Task BannedIps_UnbanServerFailure_ShowsLocalizedErrorAndKeepsRow()
    {
        (_, IRenderedComponent<SecurityLockoutPolicy>? cut) = await RenderWithManualBanAsync();
        IStringLocalizer<SecurityLockoutPolicy> localizer = GetLocalizer(this);
        _handler.DeleteStatus = HttpStatusCode.InternalServerError;

        ButtonsWithText(cut, localizer["UnbanButton"].Value).Single().Click();
        cut.WaitForAssertion(() => Assert.Single(ButtonsWithText(cut, localizer["UnbanConfirmButton"].Value)));
        ButtonsWithText(cut, localizer["UnbanConfirmButton"].Value).Single().Click();

        cut.WaitForAssertion(() => Assert.Contains(localizer["UnbanFailed", HttpStatusCode.InternalServerError.ToString()].Value, cut.Markup, StringComparison.Ordinal));
        _ = Assert.Single(RowsContaining(cut, UnbanCidr));
    }
}
