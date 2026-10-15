using Microsoft.Extensions.Logging;

using Serilog.Context;

using System;
using System.Threading;
using System.Threading.Tasks;

using VideoForensics.Core.Logging.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Core.Logging.Services
{
    /// <summary>
    /// High-level logging interface for forensic action records.
    /// Uses dual-write semantics: Serilog file sink (primary/truth) + IActionLogRepository (secondary/searchable).
    /// </summary>
    public class ActionLogger : IActionLogger
    {
        private readonly ILogger<ActionLogger> _logger;
        private readonly IActionLogRepository _actionLogRepository;

        /// <summary>
        /// Initializes a new instance with Serilog-backed ILogger (primary) and repository (secondary).
        /// </summary>
        /// <param name="logger">Serilog ILogger for structured audit logging to file sinks.</param>
        /// <param name="actionLogRepository">Secondary database sink for search and UI access.</param>
        public ActionLogger(ILogger<ActionLogger> logger, IActionLogRepository actionLogRepository)
        {
            _logger = logger;
            _actionLogRepository = actionLogRepository;
        }

        public Task<ActionLogEntry> LogAsync(
            string action,
            string entityType,
            Guid? entityId = null,
            string? details = null,
            CancellationToken ct = default)
        {
            return LogAsAsync(Environment.UserName, ActorType.Human, action, entityType, entityId, details, ct);
        }

        public async Task<ActionLogEntry> LogAsAsync(
            string actor,
            ActorType actorType,
            string action,
            string entityType,
            Guid? entityId = null,
            string? details = null,
            CancellationToken ct = default)
        {
            // PRIMARY: Log to Serilog with structured enrichment (file is the source of truth).
            // LogContext properties flow through to Serilog sinks and are included in structured JSON output.
            using (LogContext.PushProperty("Actor", actor))
            using (LogContext.PushProperty("ActorType", actorType))
            using (LogContext.PushProperty("EntityType", entityType))
            using (LogContext.PushProperty("EntityId", entityId))
            {
                _logger.LogInformation(
                    "Action logged: {Action} on {EntityType}",
                    action,
                    entityType);
            }

            // SECONDARY: Log to repository for database searchability and UI access.
            // If Serilog write succeeds but repository fails, the entry is still in the Serilog file.
            ActionLogEntry entry = await _actionLogRepository.AppendAsync(
                actor,
                actorType,
                action,
                entityType,
                entityId,
                details,
                ct);

            return entry;
        }
    }
}
