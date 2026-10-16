using VideoForensics.Core.Api.Contracts;
using VideoForensics.Ui.Shared.Layout;
using VideoForensics.WebApp.Contracts;

namespace VideoForensics.WebApp.Services.CircuitGuards
{
    /// <summary>
    /// Guards destructive self-test runs. Matches POST /api/v1/selftest/run: a request with Destructive set requires
    /// SuperAdminLocal. Listing, status and results are read-only and stay open.
    /// </summary>
    public sealed class GuardedRingSelfTestService(IRingSelfTestService inner, ICircuitOperatorGate gate) : IRingSelfTestService
    {
        public Task<IReadOnlyList<SelfTestEndpointDto>> ListEndpointsAsync(CancellationToken cancellationToken = default) =>
            inner.ListEndpointsAsync(cancellationToken);

        public Task<SelfTestRunResponseDto> StartRunAsync(SelfTestRunRequestDto request, CancellationToken cancellationToken = default)
        {
            if (request.Destructive && !gate.Allows(AccessPolicy.SuperAdminLocal))
            {
                throw new UnauthorizedAccessException("Destructive self-test runs require SuperAdminLocal.");
            }

            return inner.StartRunAsync(request, cancellationToken);
        }

        public Task<SelfTestStatusDto> GetStatusAsync(CancellationToken cancellationToken = default) => inner.GetStatusAsync(cancellationToken);

        public Task<SelfTestResultDto?> GetResultAsync(CancellationToken cancellationToken = default) => inner.GetResultAsync(cancellationToken);
    }
}
