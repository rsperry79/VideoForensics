using VideoForensics.Client.Ui.Shared.Services.Signal;

namespace VideoForensics.Client.Ui.Shared.Contracts;

/// <summary>
/// Bit rate calculations for cameras over a window. The server computes them: the WebApp runs the calculation in
/// process, and the MAUI client calls the server's API. Pages read the same figures on either host.
/// </summary>
public interface IBitrateAnalysisService
{
    /// <summary>
    /// Computes the figures for the given cameras over <paramref name="fromUtc"/> to <paramref name="toUtc"/>, from the
    /// bit rate the stream reported for each clip (estimated and missing values are left out). Values are kbps.
    /// </summary>
    Task<BitrateAnalysis> AnalyzeAsync(IReadOnlyList<Guid> deviceIds, DateTime fromUtc, DateTime toUtc, CancellationToken ct);
}
