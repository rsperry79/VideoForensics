using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Core.Logging.Contracts;

namespace VideoForensics.Core.Logging.Services
{
    /// <summary>High-level logging interface for forensic action records.</summary>
    public class ActionLogger : IActionLogger
    {
        private readonly IActionLogRepository _actionLogRepository;
        private readonly ILogger<ActionLogger> _logger;

        public ActionLogger(IActionLogRepository actionLogRepository, ILogger<ActionLogger> logger)
        {
            _actionLogRepository = actionLogRepository;
            _logger = logger;
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
            var entry = await _actionLogRepository.AppendAsync(actor, actorType, action, entityType, entityId, details, ct);

            _logger.LogInformation(
                "Action logged: actor={Actor}, type={ActorType}, action={Action}, entity={EntityType}, entityId={EntityId}",
                actor,
                actorType,
                action,
                entityType,
                entityId);

            return entry;
        }
    }
}
