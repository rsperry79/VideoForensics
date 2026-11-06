using VideoForensics.Client.Ui.Shared.Services;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>Stands in for the edit-access check on the account pages. Defaults to allowed, so tests that are not about gating render the controls.</summary>
public sealed class ProviderAccountEditAccessDouble : IProviderAccountEditAccess
{
    public bool CanEdit { get; set; } = true;

    public Task<bool> CanEditAsync(CancellationToken ct)
    {
        return Task.FromResult(CanEdit);
    }
}
