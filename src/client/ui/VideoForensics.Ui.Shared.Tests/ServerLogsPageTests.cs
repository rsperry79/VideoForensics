namespace VideoForensics.Ui.Shared.Tests;

using System.Net;
using System.Threading.Channels;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;
using Xunit;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Pages;
using VideoForensics.Ui.Shared.Services;

/// <summary>
/// TimeProvider whose timers only fire when a test says so, keyed by the exact delay the page asked for
/// (render throttle 100 ms, search debounce 300 ms, backoff 1 s/2 s/...), so a test fires one kind of timer
/// without accidentally firing another.
/// </summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private readonly object _gate = new();
    private readonly List<ManualTimer> _timers = new();

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state, dueTime);
        lock (_gate)
        {
            _timers.Add(timer);
        }

        return timer;
    }

    /// <summary>Delays of the timers currently armed.</summary>
    public IReadOnlyList<TimeSpan> Pending()
    {
        lock (_gate)
        {
            return _timers.Where(t => t.Armed).Select(t => t.Due).ToList();
        }
    }

    /// <summary>Fires every armed timer with exactly this delay; returns whether any fired.</summary>
    public bool TryFire(TimeSpan delay)
    {
        List<ManualTimer> due;
        lock (_gate)
        {
            due = _timers.Where(t => t.Armed && t.Due == delay).ToList();
            foreach (ManualTimer t in due)
            {
                t.Armed = false;
            }
        }

        foreach (ManualTimer t in due)
        {
            _ = Task.Run(() => t.Callback(t.State));
        }

        return due.Count > 0;
    }

    /// <summary>Waits until a timer with this delay is armed (the page creates them from background work), then fires it.</summary>
    public async Task FireAsync(TimeSpan delay)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (!TryFire(delay))
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"No timer of {delay} became pending. Pending: {string.Join(", ", Pending())}");
            }

            await Task.Delay(10);
        }
    }

    private sealed class ManualTimer : ITimer
    {
        private readonly ManualTimeProvider _owner;

        public ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state, TimeSpan due)
        {
            _owner = owner;
            Callback = callback;
            State = state;
            Due = due;
            Armed = due != Timeout.InfiniteTimeSpan;
        }

        public TimerCallback Callback { get; }
        public object? State { get; }
        public TimeSpan Due { get; private set; }
        public bool Armed { get; set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (_owner._gate)
            {
                Due = dueTime;
                Armed = dueTime != Timeout.InfiniteTimeSpan;
            }

            return true;
        }

        public void Dispose()
        {
            lock (_owner._gate)
            {
                Armed = false;
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}

/// <summary>
/// Scriptable <see cref="ILogViewerService"/>: records every call, serves pages from a factory, and gives each
/// stream call its own channel so a test decides exactly what that connection delivers and when it fails.
/// </summary>
public sealed class FakeLogViewerService : ILogViewerService
{
    private readonly object _gate = new();
    private readonly List<(LogQuery Query, string Token)> _pageCalls = new();
    private readonly List<StreamCall> _streamCalls = new();

    public Func<int, LogQuery, string, LogPage> OnGetPage { get; set; } = (_, _, _) => new LogPage(Array.Empty<LogEntry>(), 0, false);

    public IReadOnlyList<(LogQuery Query, string Token)> PageCalls
    {
        get { lock (_gate) { return _pageCalls.ToList(); } }
    }

    public IReadOnlyList<StreamCall> StreamCalls
    {
        get { lock (_gate) { return _streamCalls.ToList(); } }
    }

    public Task<LogPage> GetPageAsync(LogQuery query, string stepUpToken, CancellationToken ct)
    {
        int index;
        lock (_gate)
        {
            index = _pageCalls.Count;
            _pageCalls.Add((query, stepUpToken));
        }

        try
        {
            return Task.FromResult(OnGetPage(index, query, stepUpToken));
        }
        catch (Exception ex)
        {
            return Task.FromException<LogPage>(ex);
        }
    }

    public IAsyncEnumerable<LogEntry> StreamAsync(LogQuery query, string stepUpToken, CancellationToken ct)
    {
        var call = new StreamCall(query, stepUpToken);
        ct.Register(() => call.Cancelled = true);
        lock (_gate)
        {
            _streamCalls.Add(call);
        }

        return call.Channel.Reader.ReadAllAsync(ct);
    }

    public sealed class StreamCall
    {
        public StreamCall(LogQuery query, string token)
        {
            Query = query;
            Token = token;
        }

        public LogQuery Query { get; }
        public string Token { get; }
        public Channel<LogEntry> Channel { get; } = System.Threading.Channels.Channel.CreateUnbounded<LogEntry>();
        public volatile bool Cancelled;

        public void Emit(params LogEntry[] entries)
        {
            foreach (LogEntry e in entries)
            {
                Channel.Writer.TryWrite(e);
            }
        }

        public void Fail(Exception ex) => Channel.Writer.TryComplete(ex);
    }
}

public abstract class ServerLogsPageTestBase : BunitContext
{
    protected static readonly TimeSpan RenderDelay = TimeSpan.FromMilliseconds(100);
    protected static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(300);

    private int _stepUpCount;

    protected ServerLogsPageTestBase()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Module = JSInterop.SetupModule("./_content/VideoForensics.Ui.Shared/js/server-logs.js");
        Module.Mode = JSRuntimeMode.Loose;

        Services.AddLocalization();
        Services.AddSingleton<ILogViewerService>(Logs);
        Services.AddSingleton<TimeProvider>(Time);

        WebAuthn = new Mock<WebAuthnClient>(Mock.Of<IJSRuntime>(), Mock.Of<ISelfApiHttpClientFactory>());
        WebAuthn
            .Setup(w => w.StepUpAsync(It.IsAny<string>()))
            .Returns(() =>
            {
                int n = Interlocked.Increment(ref _stepUpCount);
                return StepUpFailures.TryDequeue(out Exception? failure)
                    ? Task.FromException<string>(failure)
                    : Task.FromResult($"tok-{n}");
            });
        Services.AddScoped(_ => WebAuthn.Object);

        Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));
    }

    protected FakeLogViewerService Logs { get; } = new();
    protected ManualTimeProvider Time { get; } = new();
    protected Mock<WebAuthnClient> WebAuthn { get; }
    protected BunitJSModuleInterop Module { get; }
    protected Queue<Exception> StepUpFailures { get; } = new();

    protected async Task SignInSuperAdminAsync()
    {
        var session = new PairedSessionState(JSInterop.JSRuntime);
        await session.SetAsync("session-token", Guid.NewGuid(), OperatorRole.SuperAdmin.ToString());
        Services.AddScoped(_ => session);
    }

    protected static LogEntry Entry(long seq, string level = "Information", string? message = null, string? exception = null, string category = "VideoForensics.Hosting.Remote.Thing")
        => new(seq, new DateTimeOffset(2026, 9, 25, 10, 0, 0, TimeSpan.Zero).AddMilliseconds(seq), level, category, message ?? $"message {seq}", exception);

    protected static LogPage PageOf(long latest, params LogEntry[] entries) => new(entries, latest, false);

    protected async Task<IRenderedComponent<ServerLogs>> RenderSignedInAsync()
    {
        await SignInSuperAdminAsync();
        return Render<ServerLogs>();
    }

    /// <summary>Re-fires the render-throttle timer until the assertion holds (entries reach the page from a background task).</summary>
    protected async Task WaitUntilRendered(IRenderedComponent<ServerLogs> component, Action assertion)
    {
        // Entries reach the page from background work, so the throttle timer is fired from a pump while the wait is
        // driven by renders. Polling the DOM instead would starve the renderer when thousands of rows are parsed.
        using var stop = new CancellationTokenSource();
        Task pump = Task.Run(async () =>
        {
            while (!stop.IsCancellationRequested)
            {
                Time.TryFire(RenderDelay);
                await Task.Delay(10);
            }
        });

        try
        {
            component.WaitForAssertion(assertion, TimeSpan.FromSeconds(10));
        }
        finally
        {
            await stop.CancelAsync();
            await pump;
        }
    }
    protected static async Task UntilAsync(Func<bool> condition, string what)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Timed out waiting for: {what}");
            }

            await Task.Delay(10);
        }
    }

    protected static IReadOnlyList<long> RowSequences(IRenderedComponent<ServerLogs> c)
        => c.FindAll("[data-testid='log-row']").Select(r => long.Parse(r.GetAttribute("data-seq")!)).ToList();

    /// <summary>
    /// Waits for the page to go quiet after its first load (initial fill plus the JS module import and scroll). Clicking
    /// before that races the page's own re-renders, which replace event handler ids under the test's feet.
    /// </summary>
    protected Task SettledAsync() => UntilAsync(() => Module.Invocations["scrollToBottom"].Any(), "page to settle");
    protected static string Status(IRenderedComponent<ServerLogs> c)
        => c.Find("[data-testid='logs-status']").GetAttribute("data-status")!;
}

public class ServerLogsPageLoadTests : ServerLogsPageTestBase
{
    [Fact]
    public async Task ServerLogsPage_Load_RendersInitialRowsFromGetPage()
    {
        Logs.OnGetPage = (_, _, _) => PageOf(2, Entry(1, message: "first line"), Entry(2, message: "second line"));

        var page = await RenderSignedInAsync();

        page.WaitForAssertion(() => Assert.Equal(new long[] { 1, 2 }, RowSequences(page)));
        Assert.Contains("first line", page.Markup);
        LogQuery query = Assert.Single(Logs.PageCalls).Query;
        Assert.Equal("Information", query.MinLevel);
        Assert.Null(query.Search);
        Assert.Null(query.AfterSequence);
        Assert.Equal("tok-1", Logs.PageCalls[0].Token);
    }

    [Fact]
    public async Task ServerLogsPage_Load_StartsStreamAfterLatestSequence()
    {
        Logs.OnGetPage = (_, _, _) => PageOf(10, Entry(9), Entry(10));

        await RenderSignedInAsync();

        await UntilAsync(() => Logs.StreamCalls.Count == 1, "stream to open");
        Assert.Equal(10, Logs.StreamCalls[0].Query.AfterSequence);
        Assert.Equal("Information", Logs.StreamCalls[0].Query.MinLevel);
        Assert.Equal("tok-1", Logs.StreamCalls[0].Token);
    }

    [Fact]
    public async Task ServerLogsPage_StepUpCancelled_ShowsMessageAndDoesNotCallService()
    {
        StepUpFailures.Enqueue(new InvalidOperationException("user cancelled"));

        var page = await RenderSignedInAsync();

        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("[data-testid='logs-stepup-error']")));
        Assert.Contains("user cancelled", page.Find("[data-testid='logs-stepup-error']").TextContent);
        Assert.Empty(Logs.PageCalls);
        Assert.Empty(Logs.StreamCalls);
        Assert.Equal("disconnected", Status(page));
    }

    [Fact]
    public async Task ServerLogsPage_StepUpCancelled_TryAgainStepsUpAgainAndLoads()
    {
        StepUpFailures.Enqueue(new InvalidOperationException("user cancelled"));
        Logs.OnGetPage = (_, _, _) => PageOf(1, Entry(1, message: "after retry"));
        var page = await RenderSignedInAsync();
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("[data-testid='logs-retry']")));

        page.Find("[data-testid='logs-retry']").Click();

        page.WaitForAssertion(() => Assert.Contains("after retry", page.Markup));
        Assert.Empty(page.FindAll("[data-testid='logs-stepup-error']"));
        Assert.Equal("tok-2", Assert.Single(Logs.PageCalls).Token);
    }

    [Fact]
    public async Task ServerLogsPage_SignedOut_DoesNotStepUpOrCallService()
    {
        var page = Render<ServerLogs>();

        await Task.Delay(100);
        Assert.Empty(Logs.PageCalls);
        WebAuthn.Verify(w => w.StepUpAsync(It.IsAny<string>()), Times.Never);
        Assert.Empty(page.FindAll("[data-testid='log-row']"));
    }

    [Fact]
    public void ServerLogsPage_SignedOut_SignInLinkReturnsToThisPage()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("http://localhost/settings/logs");
        var page = Render<ServerLogs>();

        var href = page.Find("a[href^='/signin?returnUrl=']").GetAttribute("href")!;
        Assert.Equal("/settings/logs", Uri.UnescapeDataString(href["/signin?returnUrl=".Length..]));
    }
}

public class ServerLogsPageStreamTests : ServerLogsPageTestBase
{
    [Fact]
    public async Task ServerLogsPage_LiveEntries_AppearInOrderAfterThrottleTick()
    {
        Logs.OnGetPage = (_, _, _) => PageOf(10, Entry(10));
        var page = await RenderSignedInAsync();
        await UntilAsync(() => Logs.StreamCalls.Count == 1, "stream to open");

        Logs.StreamCalls[0].Emit(Entry(11, message: "live one"), Entry(12, message: "live two"));

        await WaitUntilRendered(page, () => Assert.Equal(new long[] { 10, 11, 12 }, RowSequences(page)));
        Assert.Equal("live", Status(page));
    }

    [Fact]
    public async Task ServerLogsPage_LiveEntries_AreBatchedUntilThrottleFires()
    {
        Logs.OnGetPage = (_, _, _) => PageOf(10, Entry(10));
        var page = await RenderSignedInAsync();
        await UntilAsync(() => Logs.StreamCalls.Count == 1, "stream to open");

        Logs.StreamCalls[0].Emit(Entry(11));
        await UntilAsync(() => Time.Pending().Contains(RenderDelay), "render throttle timer");
        await Task.Delay(50);

        Assert.Equal(new long[] { 10 }, RowSequences(page));
    }

    [Fact]
    public async Task ServerLogsPage_StreamedEntryAtOrBelowLastSequence_IsIgnored()
    {
        Logs.OnGetPage = (_, _, _) => PageOf(10, Entry(10));
        var page = await RenderSignedInAsync();
        await UntilAsync(() => Logs.StreamCalls.Count == 1, "stream to open");

        Logs.StreamCalls[0].Emit(Entry(10, message: "duplicate"), Entry(11));

        await WaitUntilRendered(page, () => Assert.Equal(new long[] { 10, 11 }, RowSequences(page)));
    }

    [Fact]
    public async Task ServerLogsPage_ManyEntries_ListIsBoundedAt2000DroppingOldest()
    {
        Logs.OnGetPage = (_, _, _) => PageOf(1500, Enumerable.Range(1, 1500).Select(i => Entry(i)).ToArray());
        var page = await RenderSignedInAsync();
        await UntilAsync(() => Logs.StreamCalls.Count == 1, "stream to open");

        Logs.StreamCalls[0].Emit(Enumerable.Range(1501, 700).Select(i => Entry(i)).ToArray());

        await WaitUntilRendered(page, () =>
        {
            IReadOnlyList<long> seqs = RowSequences(page);
            Assert.Equal(2000, seqs.Count);
            Assert.Equal(201, seqs[0]);
            Assert.Equal(2200, seqs[^1]);
        });
    }

    [Fact]
    public async Task ServerLogsPage_Clear_EmptiesRowsWithoutCallingService()
    {
        Logs.OnGetPage = (_, _, _) => PageOf(2, Entry(1), Entry(2));
        var page = await RenderSignedInAsync();
        page.WaitForAssertion(() => Assert.Equal(2, RowSequences(page).Count));

        await SettledAsync();

        page.Find("[data-testid='logs-clear']").Click();

        Assert.Empty(page.FindAll("[data-testid='log-row']"));
        Assert.Single(Logs.PageCalls);
        await UntilAsync(() => Logs.StreamCalls.Count == 1, "stream to open");
        Logs.StreamCalls[0].Emit(Entry(3));
        await WaitUntilRendered(page, () => Assert.Equal(new long[] { 3 }, RowSequences(page)));
    }

    [Fact]
    public async Task ServerLogsPage_Dispose_CancelsTheStream()
    {
        Logs.OnGetPage = (_, _, _) => PageOf(1, Entry(1));
        var page = await RenderSignedInAsync();
        await UntilAsync(() => Logs.StreamCalls.Count == 1, "stream to open");
        Assert.False(Logs.StreamCalls[0].Cancelled);

        await DisposeComponentsAsync();

        await UntilAsync(() => Logs.StreamCalls[0].Cancelled, "stream cancellation");
    }

    [Fact]
    public async Task ServerLogsPage_Dispose_StopsPendingBackoffReconnect()
    {
        Logs.OnGetPage = (_, _, _) => PageOf(1, Entry(1));
        var page = await RenderSignedInAsync();
        await UntilAsync(() => Logs.StreamCalls.Count == 1, "stream to open");
        Logs.StreamCalls[0].Fail(new HttpRequestException("boom", null, HttpStatusCode.BadGateway));
        await UntilAsync(() => Time.Pending().Contains(TimeSpan.FromSeconds(1)), "backoff timer");

        await DisposeComponentsAsync();
        Time.TryFire(TimeSpan.FromSeconds(1));
        await Task.Delay(100);

        Assert.Single(Logs.StreamCalls);
    }
}

public class ServerLogsPageFilterTests : ServerLogsPageTestBase
{
    [Fact]
    public async Task ServerLogsPage_LevelFilterChanged_RestartsFetchAndStreamWithNewQuery()
    {
        Logs.OnGetPage = (i, q, _) => i == 0 ? PageOf(5, Entry(5, message: "old info")) : PageOf(9, Entry(9, "Error", "an error"));
        var page = await RenderSignedInAsync();
        await UntilAsync(() => Logs.StreamCalls.Count == 1, "first stream");
        await SettledAsync();

        ((IHtmlSelectElement)page.Find("[data-testid='logs-level']")).Change("Error");

        await UntilAsync(() => Logs.StreamCalls.Count == 2, "second stream");
        Assert.Equal(2, Logs.PageCalls.Count);
        Assert.Equal("Error", Logs.PageCalls[1].Query.MinLevel);
        Assert.Null(Logs.PageCalls[1].Query.AfterSequence);
        Assert.Equal("Error", Logs.StreamCalls[1].Query.MinLevel);
        Assert.Equal(9, Logs.StreamCalls[1].Query.AfterSequence);
        Assert.True(Logs.StreamCalls[0].Cancelled);
        // The cached token is reused; changing a filter does not prompt for step-up again.
        Assert.Equal("tok-1", Logs.PageCalls[1].Token);
        page.WaitForAssertion(() => Assert.Equal(new long[] { 9 }, RowSequences(page)));
    }

    [Fact]
    public async Task ServerLogsPage_SearchTyped_IsDebouncedAndRestartsOnceWithFinalText()
    {
        var page = await RenderSignedInAsync();
        await UntilAsync(() => Logs.StreamCalls.Count == 1, "first stream");
        await SettledAsync();
        IElement search = page.Find("[data-testid='logs-search']");

        search.Input("a");
        search.Input("ab");
        search.Input("abc");
        await Task.Delay(50);

        Assert.Single(Logs.PageCalls);
        await Time.FireAsync(DebounceDelay);
        await UntilAsync(() => Logs.PageCalls.Count == 2, "restart after debounce");
        await UntilAsync(() => Logs.StreamCalls.Count == 2, "restart stream");

        Assert.Equal("abc", Logs.PageCalls[1].Query.Search);
        Assert.Equal("abc", Logs.StreamCalls[1].Query.Search);
        await Task.Delay(50);
        Assert.Equal(2, Logs.PageCalls.Count);
    }

    [Fact]
    public async Task ServerLogsPage_SearchClearedToBlank_QueriesWithNullSearch()
    {
        var page = await RenderSignedInAsync();
        await UntilAsync(() => Logs.StreamCalls.Count == 1, "first stream");
        await SettledAsync();
        page.Find("[data-testid='logs-search']").Input("abc");
        await Time.FireAsync(DebounceDelay);
        await UntilAsync(() => Logs.PageCalls.Count == 2, "first restart");

        page.Find("[data-testid='logs-search']").Input("  ");
        await Time.FireAsync(DebounceDelay);

        await UntilAsync(() => Logs.PageCalls.Count == 3, "second restart");
        Assert.Null(Logs.PageCalls[2].Query.Search);
    }
}

public class ServerLogsPagePauseTests : ServerLogsPageTestBase
{
    [Fact]
    public async Task ServerLogsPage_Paused_HoldsNewRowsShowsBadgeAndResumeAppends()
    {
        Logs.OnGetPage = (_, _, _) => PageOf(10, Entry(10));
        var page = await RenderSignedInAsync();
        await UntilAsync(() => Logs.StreamCalls.Count == 1, "stream to open");
        page.WaitForAssertion(() => Assert.Single(RowSequences(page)));

        await SettledAsync();

        page.Find("[data-testid='logs-pause']").Click();
        Logs.StreamCalls[0].Emit(Entry(11), Entry(12));

        await WaitUntilRendered(page, () => Assert.Contains("2", page.Find("[data-testid='logs-new-badge']").TextContent));
        Assert.Equal(new long[] { 10 }, RowSequences(page));
        Assert.Equal("paused", Status(page));
        Assert.Single(Logs.StreamCalls);

        await SettledAsync();

        page.Find("[data-testid='logs-pause']").Click();

        Assert.Equal(new long[] { 10, 11, 12 }, RowSequences(page));
        Assert.Empty(page.FindAll("[data-testid='logs-new-badge']"));
        Assert.Equal("live", Status(page));
    }

    [Fact]
    public async Task ServerLogsPage_PausedBuffer_IsBoundedAt2000()
    {
        Logs.OnGetPage = (_, _, _) => PageOf(1, Entry(1));
        var page = await RenderSignedInAsync();
        await UntilAsync(() => Logs.StreamCalls.Count == 1, "stream to open");
        page.WaitForAssertion(() => Assert.Single(RowSequences(page)));
        await SettledAsync();
        page.Find("[data-testid='logs-pause']").Click();

        Logs.StreamCalls[0].Emit(Enumerable.Range(2, 2500).Select(i => Entry(i)).ToArray());

        await WaitUntilRendered(page, () => Assert.Contains("2000", page.Find("[data-testid='logs-new-badge']").TextContent));
        await SettledAsync();
        page.Find("[data-testid='logs-pause']").Click();
        IReadOnlyList<long> seqs = RowSequences(page);
        Assert.Equal(2000, seqs.Count);
        Assert.Equal(2501, seqs[^1]);
    }
}

public class ServerLogsPageErrorTests : ServerLogsPageTestBase
{
    [Fact]
    public async Task ServerLogsPage_Unauthorized_StepsUpAgainExactlyOnceThenShowsAccessDenied()
    {
        Logs.OnGetPage = (_, _, _) => throw new HttpRequestException("denied", null, HttpStatusCode.Forbidden);

        var page = await RenderSignedInAsync();

        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("[data-testid='logs-denied']")));
        WebAuthn.Verify(w => w.StepUpAsync(It.IsAny<string>()), Times.Exactly(2));
        Assert.Equal(2, Logs.PageCalls.Count);
        Assert.Equal("tok-1", Logs.PageCalls[0].Token);
        Assert.Equal("tok-2", Logs.PageCalls[1].Token);
        Assert.Empty(Logs.StreamCalls);
        Assert.Equal("disconnected", Status(page));
    }

    [Fact]
    public async Task ServerLogsPage_UnauthorizedOnce_ReStepsUpAndRecovers()
    {
        Logs.OnGetPage = (i, _, _) => i == 0
            ? throw new HttpRequestException("expired", null, HttpStatusCode.Unauthorized)
            : PageOf(1, Entry(1, message: "recovered"));

        var page = await RenderSignedInAsync();

        page.WaitForAssertion(() => Assert.Contains("recovered", page.Markup));
        Assert.Equal("tok-2", Logs.PageCalls[1].Token);
        Assert.Empty(page.FindAll("[data-testid='logs-denied']"));
    }

    [Fact]
    public async Task ServerLogsPage_StreamUnauthorized_ReStepsUpThenReconnectsWithNewToken()
    {
        Logs.OnGetPage = (_, _, _) => PageOf(5, Entry(5));
        var page = await RenderSignedInAsync();
        await UntilAsync(() => Logs.StreamCalls.Count == 1, "stream to open");

        Logs.StreamCalls[0].Fail(new HttpRequestException("expired", null, HttpStatusCode.Unauthorized));

        await UntilAsync(() => Logs.StreamCalls.Count == 2, "stream reconnect");
        Assert.Equal("tok-2", Logs.StreamCalls[1].Token);
        Assert.Equal(5, Logs.StreamCalls[1].Query.AfterSequence);
        Assert.Empty(page.FindAll("[data-testid='logs-denied']"));
    }

    [Fact]
    public async Task ServerLogsPage_TransientStreamError_ReconnectsFromLastReceivedSequenceWithBackoff()
    {
        Logs.OnGetPage = (_, _, _) => PageOf(10, Entry(10));
        var page = await RenderSignedInAsync();
        await UntilAsync(() => Logs.StreamCalls.Count == 1, "stream to open");
        Logs.StreamCalls[0].Emit(Entry(11), Entry(12));
        await WaitUntilRendered(page, () => Assert.Equal(3, RowSequences(page).Count));

        Logs.StreamCalls[0].Fail(new HttpRequestException("boom", null, HttpStatusCode.InternalServerError));

        page.WaitForAssertion(() => Assert.Equal("reconnecting", Status(page)));
        Assert.Single(Logs.StreamCalls);
        await Time.FireAsync(TimeSpan.FromSeconds(1));
        await UntilAsync(() => Logs.StreamCalls.Count == 2, "reconnect");
        Assert.Equal(12, Logs.StreamCalls[1].Query.AfterSequence);
        Assert.Equal("tok-1", Logs.StreamCalls[1].Token);
        Assert.Single(Logs.PageCalls);

        // A reconnect that fails immediately doubles the wait: 1s, 2s, ...
        Logs.StreamCalls[1].Fail(new HttpRequestException("still down", null, HttpStatusCode.BadGateway));
        await UntilAsync(() => Time.Pending().Contains(TimeSpan.FromSeconds(2)), "doubled backoff");
        await Time.FireAsync(TimeSpan.FromSeconds(2));
        await UntilAsync(() => Logs.StreamCalls.Count == 3, "third connect");
        Logs.StreamCalls[2].Emit(Entry(13));
        await WaitUntilRendered(page, () => Assert.Equal(4, RowSequences(page).Count));
        Assert.Equal("live", Status(page));
    }

    [Fact]
    public async Task ServerLogsPage_BackoffIsCappedAt30Seconds()
    {
        Logs.OnGetPage = (_, _, _) => PageOf(1, Entry(1));
        await RenderSignedInAsync();
        int expectedCalls = 1;
        foreach (int seconds in new[] { 1, 2, 4, 8, 16, 30, 30 })
        {
            await UntilAsync(() => Logs.StreamCalls.Count == expectedCalls, "stream connect");
            Logs.StreamCalls[expectedCalls - 1].Fail(new HttpRequestException("down", null, HttpStatusCode.BadGateway));
            await UntilAsync(() => Time.Pending().Contains(TimeSpan.FromSeconds(seconds)), $"{seconds}s backoff");
            await Time.FireAsync(TimeSpan.FromSeconds(seconds));
            expectedCalls++;
        }
    }

    [Fact]
    public async Task ServerLogsPage_GetPageTransientError_RetriesWithBackoffWithoutNewStepUp()
    {
        Logs.OnGetPage = (i, _, _) => i == 0
            ? throw new HttpRequestException("down", null, HttpStatusCode.ServiceUnavailable)
            : PageOf(1, Entry(1, message: "finally"));

        var page = await RenderSignedInAsync();
        await UntilAsync(() => Time.Pending().Contains(TimeSpan.FromSeconds(1)), "backoff");
        page.WaitForAssertion(() => Assert.Equal("reconnecting", Status(page)));
        await Time.FireAsync(TimeSpan.FromSeconds(1));

        page.WaitForAssertion(() => Assert.Contains("finally", page.Markup));
        WebAuthn.Verify(w => w.StepUpAsync(It.IsAny<string>()), Times.Once);
    }
}

public class ServerLogsPageRenderingTests : ServerLogsPageTestBase
{
    [Fact]
    public async Task ServerLogsPage_HtmlInMessageAndException_IsRenderedAsTextNotMarkup()
    {
        const string payload = "<script>window.pwned=1</script><b id=\"evil\">bold</b>";
        Logs.OnGetPage = (_, _, _) => PageOf(1, Entry(1, message: payload, exception: "<img src=x onerror=alert(1)>", category: "<i>cat</i>"));
        var page = await RenderSignedInAsync();
        page.WaitForAssertion(() => Assert.Single(RowSequences(page)));
        await SettledAsync();
        page.Find("[data-testid='log-row-toggle']").Click();

        Assert.Empty(page.FindAll("script"));
        Assert.Empty(page.FindAll("#evil"));
        Assert.Empty(page.FindAll("img"));
        Assert.Empty(page.FindAll("td i"));
        Assert.Contains(payload, page.Find("[data-testid='log-message']").TextContent);
        Assert.Contains("<img src=x onerror=alert(1)>", page.Find("[data-testid='log-exception']").TextContent);
    }

    [Fact]
    public async Task ServerLogsPage_RowWithException_ExpandsAndCollapsesOnToggle()
    {
        Logs.OnGetPage = (_, _, _) => PageOf(2, Entry(1, exception: "System.Boom: stack trace"), Entry(2));
        var page = await RenderSignedInAsync();
        page.WaitForAssertion(() => Assert.Equal(2, RowSequences(page).Count));

        Assert.Single(page.FindAll("[data-testid='log-row-toggle']"));
        Assert.Empty(page.FindAll("[data-testid='log-exception']"));

        await SettledAsync();

        page.Find("[data-testid='log-row-toggle']").Click();
        Assert.Contains("System.Boom", page.Find("[data-testid='log-exception']").TextContent);

        await SettledAsync();

        page.Find("[data-testid='log-row-toggle']").Click();
        Assert.Empty(page.FindAll("[data-testid='log-exception']"));
    }

    [Fact]
    public async Task ServerLogsPage_Row_ShowsLevelBadgeShortCategoryAndUtcTooltip()
    {
        Logs.OnGetPage = (_, _, _) => PageOf(1, Entry(1, "Error", "boom", category: "VideoForensics.Hosting.Remote.Thing"));
        var page = await RenderSignedInAsync();
        page.WaitForAssertion(() => Assert.Single(RowSequences(page)));

        IElement row = page.Find("[data-testid='log-row']");
        Assert.Equal("Error", row.GetAttribute("data-level"));
        Assert.Equal("Error", page.Find("[data-testid='log-level-badge']").TextContent.Trim());
        IElement category = page.Find("[data-testid='log-category']");
        Assert.Equal("Thing", category.TextContent.Trim());
        Assert.Equal("VideoForensics.Hosting.Remote.Thing", category.GetAttribute("title"));
        Assert.Contains("2026-09-25 10:00:00.001 UTC", page.Find("[data-testid='log-time']").GetAttribute("title"));
    }

    [Fact]
    public async Task ServerLogsPage_Defaults_MinLevelInformationAutoScrollOn()
    {
        var page = await RenderSignedInAsync();

        Assert.Equal("Information", ((IHtmlSelectElement)page.Find("[data-testid='logs-level']")).Value);
        Assert.Equal(6, page.FindAll("[data-testid='logs-level'] option").Count);
        Assert.True(((IHtmlInputElement)page.Find("[data-testid='logs-autoscroll']")).IsChecked);
    }
}

public class ServerLogsPageClientActionTests : ServerLogsPageTestBase
{
    [Fact]
    public async Task ServerLogsPage_AutoScrollOn_ScrollsAfterNewRowsAndStopsWhenOff()
    {
        Logs.OnGetPage = (_, _, _) => PageOf(1, Entry(1));
        var page = await RenderSignedInAsync();
        await UntilAsync(() => Logs.StreamCalls.Count == 1, "stream to open");
        await UntilAsync(() => Module.Invocations["scrollToBottom"].Any(), "initial scroll");

        int before = Module.Invocations["scrollToBottom"].Count();
        Logs.StreamCalls[0].Emit(Entry(2));
        await WaitUntilRendered(page, () => Assert.Equal(2, RowSequences(page).Count));
        await UntilAsync(() => Module.Invocations["scrollToBottom"].Count() > before, "scroll after live row");

        page.Find("[data-testid='logs-autoscroll']").Change(false);
        int afterOff = Module.Invocations["scrollToBottom"].Count();
        Logs.StreamCalls[0].Emit(Entry(3));
        await WaitUntilRendered(page, () => Assert.Equal(3, RowSequences(page).Count));
        await Task.Delay(50);
        Assert.Equal(afterOff, Module.Invocations["scrollToBottom"].Count());
    }

    [Fact]
    public async Task ServerLogsPage_Download_SendsVisibleRowsAsTextFile()
    {
        Logs.OnGetPage = (_, _, _) => PageOf(2, Entry(1, message: "alpha"), Entry(2, "Error", "beta", exception: "System.Boom"));
        var page = await RenderSignedInAsync();
        page.WaitForAssertion(() => Assert.Equal(2, RowSequences(page).Count));

        await SettledAsync();

        page.Find("[data-testid='logs-download']").Click();

        await UntilAsync(() => Module.Invocations["downloadText"].Any(), "download call");
        var args = Module.Invocations["downloadText"].Single().Arguments;
        Assert.EndsWith(".txt", (string)args[0]!);
        string text = (string)args[1]!;
        Assert.Contains("alpha", text);
        Assert.Contains("[Error]", text);
        Assert.Contains("beta", text);
        Assert.Contains("System.Boom", text);
        Assert.True(text.IndexOf("alpha", StringComparison.Ordinal) < text.IndexOf("beta", StringComparison.Ordinal));
    }
}
