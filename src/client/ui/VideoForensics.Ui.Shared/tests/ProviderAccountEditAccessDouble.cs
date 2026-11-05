using VideoForensics.Client.Ui.Shared.Services;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>Stands in for the edit-access check on the account pages. Defaults to allowed, so tests that are not about gating render the controls.</summary>
public sealed class ProviderAccountEditAccessDouble : IProviderAccountEditAccess
{
    public bool CanEdit { get; set; } = true;

    /// <summary>Answers returned in order before <see cref="CanEdit"/> applies. Lets a test model edit access that arrives after the first render.</summary>
    public Queue<bool> Answers { get; } = new();

    public Task<bool> CanEditAsync(CancellationToken ct)
    {
        bool answer = Answers.Count > 0 ? Answers.Dequeue() : CanEdit;
        return Task.FromResult(answer);
    }
}
