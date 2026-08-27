using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for Annotation entities.</summary>
    public class AnnotationRepository : IAnnotationRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<AnnotationRepository> _logger;

        /// <summary>Initializes a new instance of the AnnotationRepository.</summary>
        public AnnotationRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<AnnotationRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        /// <summary>Gets an annotation by ID.</summary>
        public async Task<Annotation?> GetAsync(Guid annotationId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.Annotations.FirstOrDefaultAsync(a => a.Id == annotationId, ct);
        }

        /// <summary>Adds a new annotation.</summary>
        public async Task<Annotation> AddAsync(string entityType, Guid entityId, string source, string key, string value, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            try
            {
                var annotation = new Annotation
                {
                    Id = Guid.NewGuid(),
                    EntityType = entityType,
                    EntityId = entityId,
                    Source = source,
                    Key = key,
                    Value = value,
                    CreatedAtUtc = DateTime.UtcNow
                };

                db.Annotations.Add(annotation);
                await db.SaveChangesAsync(ct);
                _logger.LogInformation("Annotation added: {AnnotationId} ({EntityType}:{EntityId})",
                    annotation.Id, entityType, entityId);
                return annotation;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding annotation for {EntityType}:{EntityId}", entityType, entityId);
                throw;
            }
        }

        /// <summary>Gets all annotations for a specific entity.</summary>
        public async Task<IReadOnlyList<Annotation>> GetForEntityAsync(string entityType, Guid entityId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.Annotations
                .Where(a => a.EntityType == entityType && a.EntityId == entityId)
                .ToListAsync(ct);
        }

        /// <summary>Searches for annotations by key and optional value (cross-entity lookup).</summary>
        public async Task<IReadOnlyList<Annotation>> SearchAsync(string key, string? value, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var query = db.Annotations.Where(a => a.Key == key);

            if (!string.IsNullOrEmpty(value))
            {
                query = query.Where(a => a.Value == value);
            }

            return await query.ToListAsync(ct);
        }

        /// <summary>Deletes an annotation.</summary>
        public async Task DeleteAsync(Guid annotationId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            try
            {
                var annotation = await db.Annotations.FirstOrDefaultAsync(a => a.Id == annotationId, ct);
                if (annotation != null)
                {
                    db.Annotations.Remove(annotation);
                    await db.SaveChangesAsync(ct);
                    _logger.LogInformation("Annotation deleted: {AnnotationId}", annotationId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting annotation: {AnnotationId}", annotationId);
                throw;
            }
        }

        /// <summary>Deletes all annotations for a specific entity.</summary>
        public async Task DeleteForEntityAsync(string entityType, Guid entityId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            try
            {
                var annotations = await db.Annotations
                    .Where(a => a.EntityType == entityType && a.EntityId == entityId)
                    .ToListAsync(ct);

                if (annotations.Count > 0)
                {
                    db.Annotations.RemoveRange(annotations);
                    await db.SaveChangesAsync(ct);
                    _logger.LogInformation("Annotations deleted for {EntityType}:{EntityId} (count: {Count})",
                        entityType, entityId, annotations.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting annotations for {EntityType}:{EntityId}", entityType, entityId);
                throw;
            }
        }
    }
}
