namespace VideoForensics.Ui.Shared.Tests;

/// <summary>
/// A fake time provider that returns a fixed time for testing.
/// </summary>
internal sealed class FakeTimeProvider : TimeProvider
{
    private readonly DateTime _fixedTime;

    public FakeTimeProvider(DateTime fixedTime)
    {
        _fixedTime = fixedTime;
    }

    public override DateTimeOffset GetUtcNow() => new(_fixedTime, TimeSpan.Zero);
}
