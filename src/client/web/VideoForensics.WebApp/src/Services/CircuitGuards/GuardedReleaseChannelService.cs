using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Layout;
using VideoForensics.WebApp.Contracts;

namespace VideoForensics.WebApp.Services.CircuitGuards
{
    /// <summary>
    /// Guards release-channel changes. Matches PUT /api/v1/release-channel, which requires SuperAdminLocal; reading stays open.
    /// A successful change is audited like the endpoint does; a circuit has no HttpContext, so the source IP is the one captured at circuit start.
    /// </summary>
    public sealed class GuardedReleaseChannelService(
        IReleaseChannelService inner,
        ICircuitOperatorGate gate,
        ISecurityAuditLogger auditLog,
        SessionNetworkContext network) : IReleaseChannelService
    {
        /// <inheritdoc />
        public Task<UpdateReleaseChannel> GetReleaseChannelAsync(CancellationToken cancellationToken = default)
        {
            return inner.GetReleaseChannelAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task<UpdateReleaseChannel> SetReleaseChannelAsync(UpdateReleaseChannel channel, CancellationToken cancellationToken = default)
        {
            if (!gate.Allows(AccessPolicy.SuperAdminLocal))
            {
                throw new UnauthorizedAccessException(gate.Denial(AccessPolicy.SuperAdminLocal));
            }

            UpdateReleaseChannel oldChannel = await inner.GetReleaseChannelAsync(cancellationToken);
            UpdateReleaseChannel saved = await inner.SetReleaseChannelAsync(channel, cancellationToken);

            await auditLog.LogAsync(SecurityAuditEventTypes.ReleaseChannelChanged, gate.OperatorId, null, network.ClientIp,
                $"Release channel changed from '{oldChannel}' to '{channel}'", isUrgent: true, cancellationToken);

            return saved;
        }
    }
}
