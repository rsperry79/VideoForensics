using VideoForensics.Client.Ui.Shared.Services.Review;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>The debounce helper: the last Schedule wins, Cancel and Dispose drop a pending run.</summary>
public class DebouncedAction_Tests
{
    /// <summary>A delay the test completes by hand, so no real time passes.</summary>
    private sealed class ManualDelay
    {
        private readonly List<TaskCompletionSource> _pending = [];

        public int Calls { get; private set; }

        public TimeSpan LastDelay { get; private set; }

        public Task Delay(TimeSpan delay, CancellationToken token)
        {
            Calls++;
            LastDelay = delay;
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _ = token.Register(() => tcs.TrySetCanceled(token));
            _pending.Add(tcs);
            return tcs.Task;
        }

        public void CompleteAll()
        {
            foreach (TaskCompletionSource tcs in _pending.ToList())
            {
                _ = tcs.TrySetResult();
            }
        }
    }

    private static async Task SettleAsync()
    {
        await Task.Delay(30);
    }

    /// <summary>Waits for a thread-pool continuation to land; a fixed delay is too short on a loaded test host.</summary>
    private static async Task WaitForAsync(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
    }

    [Fact]
    public void DebouncedAction_DefaultDelay_IsOneSecond()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(1000), DebouncedAction.DefaultDelay);
    }

    [Fact]
    public async Task Schedule_AfterTheDelay_RunsTheActionOnce()
    {
        // Arrange
        var delay = new ManualDelay();
        int runs = 0;
        using var debounce = new DebouncedAction(TimeSpan.FromSeconds(1), () => runs++, delay.Delay);

        // Act
        debounce.Schedule();
        await SettleAsync();
        Assert.Equal(0, runs);
        delay.CompleteAll();
        await WaitForAsync(() => Volatile.Read(ref runs) == 1);

        // Assert
        Assert.Equal(1, runs);
        Assert.Equal(TimeSpan.FromSeconds(1), delay.LastDelay);
    }

    [Fact]
    public async Task Schedule_CalledAgainBeforeTheDelay_RestartsAndRunsOnce()
    {
        // Arrange
        var delay = new ManualDelay();
        int runs = 0;
        using var debounce = new DebouncedAction(TimeSpan.FromSeconds(1), () => runs++, delay.Delay);

        // Act
        debounce.Schedule();
        debounce.Schedule();
        debounce.Schedule();
        delay.CompleteAll();
        await WaitForAsync(() => Volatile.Read(ref runs) == 1);
        await SettleAsync();

        // Assert
        Assert.Equal(3, delay.Calls);
        Assert.Equal(1, runs);
    }

    [Fact]
    public async Task Cancel_BeforeTheDelay_NeverRuns()
    {
        // Arrange
        var delay = new ManualDelay();
        int runs = 0;
        using var debounce = new DebouncedAction(TimeSpan.FromSeconds(1), () => runs++, delay.Delay);
        debounce.Schedule();

        // Act
        debounce.Cancel();
        delay.CompleteAll();
        await SettleAsync();

        // Assert
        Assert.Equal(0, runs);
    }

    [Fact]
    public async Task Dispose_BeforeTheDelay_NeverRuns()
    {
        // Arrange
        var delay = new ManualDelay();
        int runs = 0;
        var debounce = new DebouncedAction(TimeSpan.FromSeconds(1), () => runs++, delay.Delay);
        debounce.Schedule();

        // Act
        debounce.Dispose();
        delay.CompleteAll();
        await SettleAsync();

        // Assert
        Assert.Equal(0, runs);
    }

    [Fact]
    public async Task Schedule_AfterDispose_DoesNothing()
    {
        // Arrange
        var delay = new ManualDelay();
        int runs = 0;
        var debounce = new DebouncedAction(TimeSpan.FromSeconds(1), () => runs++, delay.Delay);
        debounce.Dispose();

        // Act
        debounce.Schedule();
        delay.CompleteAll();
        await SettleAsync();

        // Assert
        Assert.Equal(0, runs);
        Assert.Equal(0, delay.Calls);
    }

    [Fact]
    public async Task Schedule_AsyncAction_IsAwaitedAndCanScheduleAgain()
    {
        // Arrange
        var delay = new ManualDelay();
        int runs = 0;
        using var debounce = new DebouncedAction(TimeSpan.FromSeconds(1), async () =>
        {
            await Task.Yield();
            runs++;
        }, delay.Delay);

        // Act
        debounce.Schedule();
        delay.CompleteAll();
        await SettleAsync();
        debounce.Schedule();
        delay.CompleteAll();
        await SettleAsync();

        // Assert
        Assert.Equal(2, runs);
    }

    [Fact]
    public async Task Schedule_RealShortDelay_RunsTheAction()
    {
        // Arrange
        var ran = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var debounce = new DebouncedAction(TimeSpan.FromMilliseconds(20), () => ran.TrySetResult());

        // Act
        debounce.Schedule();

        // Assert
        Task winner = await Task.WhenAny(ran.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(ran.Task, winner);
    }
}
