using VideoForensics.Client.WebApp.Contracts;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Client.Ui.Shared.Layout;

namespace VideoForensics.Client.WebApp.Services.CircuitGuards
{
    /// <summary>
    /// Guards destructive self-test runs. Matches POST /api/v1/selftest/run: a request with Destructive set requires
    /// SuperAdminLocal. Listing, status and results are read-only and stay open.
    /// </summary>
    public sealed class GuardedRingSelfTestService(IRingSelfTestService inner, ICircuitOperatorGate gate) : IRingSelfTestService
    {
        public Task<IReadOnlyList<SelfTestEndpointDto>> ListEndpointsAsync(CancellationToken cancellationToken = default)
        {
            return inner.ListEndpointsAsync(cancellationToken);
        }

        public Task<SelfTestRunResponseDto> StartRunAsync(SelfTestRunRequestDto request, CancellationToken cancellationToken = default)
        {
            return request.Destructive && !gate.Allows(AccessPolicy.SuperAdminLocal)
                ? throw new UnauthorizedAccessException(gate.Denial(AccessPolicy.SuperAdminLocal))
                : inner.StartRunAsync(request, cancellationToken);
        }

        public Task<SelfTestStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            return inner.GetStatusAsync(cancellationToken);
        }

        public Task<SelfTestResultDto?> GetResultAsync(CancellationToken cancellationToken = default)
        {
            return inner.GetResultAsync(cancellationToken);
        }
    }
}
