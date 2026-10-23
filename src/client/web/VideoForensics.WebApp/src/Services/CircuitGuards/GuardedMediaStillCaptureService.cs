using VideoForensics.Client.Common.Contracts;
using VideoForensics.Ui.Shared.Layout;
using VideoForensics.WebApp.Contracts;

namespace VideoForensics.WebApp.Services.CircuitGuards
{
    /// <summary>
    /// Guards capturing a still from the Evidence page in-process. Matches POST /api/v1/media/{id}/stills, which
    /// requires Review.
    /// </summary>
    public sealed class GuardedMediaStillCaptureService(IMediaStillCaptureService inner, ICircuitOperatorGate gate) : IMediaStillCaptureService
    {
        public Task<MediaStillCaptureResult> CaptureStillAsync(
            Guid sourceMediaItemId,
            long frameOffsetMs,
            string? clientPngBase64,
            Guid? caseId,
            string? pinReason,
            string capturedBy,
            CancellationToken ct)
        {
            return !gate.Allows(AccessPolicy.Review)
                ? throw new UnauthorizedAccessException(gate.Denial(AccessPolicy.Review))
                : inner.CaptureStillAsync(sourceMediaItemId, frameOffsetMs, clientPngBase64, caseId, pinReason, capturedBy, ct);
        }
    }
}
