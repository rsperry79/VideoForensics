using System;
using System.Threading;
using System.Threading.Tasks;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Core.Logging.Contracts
{
    /// <summary>High-level logging interface for forensic action records.</summary>
    public interface IActionLogger
    {
        /// <summary>
        /// Logs an action attributed to the current user (Environment.UserName, ActorType = Human).
        /// </summary>
        Task<ActionLogEntry> LogAsync(
            string action,
            string entityType,
            Guid? entityId = null,
            string? details = null,
            CancellationToken ct = default);

        /// <summary>
        /// Logs an action attributed to a non-human actor (e.g. a System task or MCP tool).
        /// </summary>
        Task<ActionLogEntry> LogAsAsync(
            string actor,
            ActorType actorType,
            string action,
            string entityType,
            Guid? entityId = null,
            string? details = null,
            CancellationToken ct = default);
    }
}
