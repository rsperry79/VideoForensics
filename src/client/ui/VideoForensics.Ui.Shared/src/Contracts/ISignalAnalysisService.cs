using VideoForensics.Client.Ui.Shared.Services.Signal;

namespace VideoForensics.Client.Ui.Shared.Contracts;

/// <summary>
/// Signal calculations for cameras over a window. The server computes them: the WebApp runs the calculation in
/// process, and the MAUI client calls the server's API. Pages read the same figures on either host.
/// </summary>
public interface ISignalAnalysisService
{
    /// <summary>Computes the figures for the given cameras over <paramref name="fromUtc"/> to <paramref name="toUtc"/>.</summary>
    Task<SignalAnalysis> AnalyzeAsync(IReadOnlyList<Guid> deviceIds, DateTime fromUtc, DateTime toUtc, CancellationToken ct);
}
