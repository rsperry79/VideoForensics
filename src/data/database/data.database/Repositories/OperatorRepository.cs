using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for Operator entities.</summary>
    public class OperatorRepository : IOperatorRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<OperatorRepository> _logger;

        public OperatorRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<OperatorRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        public async Task<Operator?> GetAsync(Guid operatorId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.Operators.FirstOrDefaultAsync(o => o.Id == operatorId, ct);
        }

        public async Task<Operator> AddAsync(Operator @operator, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            db.Operators.Add(@operator);
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Operator created: {OperatorId} ({DisplayName})", @operator.Id, @operator.DisplayName);
            return @operator;
        }

        public async Task<IReadOnlyList<Operator>> ListAsync(CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.Operators.OrderBy(o => o.DisplayName).ToListAsync(ct);
        }

        public async Task<bool> IsEmptyAsync(CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return !await db.Operators.AnyAsync(ct);
        }

        public async Task DeactivateAsync(Guid operatorId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var op = await db.Operators.FirstOrDefaultAsync(o => o.Id == operatorId, ct);
            if (op == null)
            {
                return;
            }

            op.Active = false;
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Operator deactivated: {OperatorId}", operatorId);
        }
    }
}
