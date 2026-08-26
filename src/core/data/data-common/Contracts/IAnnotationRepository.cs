using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for generic key-value annotations on any entity.</summary>
    public interface IAnnotationRepository
    {
        /// <summary>Gets an annotation by ID.</summary>
        Task<Annotation?> GetAsync(Guid annotationId, CancellationToken ct);

        /// <summary>Adds a new annotation.</summary>
        Task<Annotation> AddAsync(string entityType, Guid entityId, string source, string key, string value, CancellationToken ct);

        /// <summary>Gets all annotations for a specific entity.</summary>
        Task<IReadOnlyList<Annotation>> GetForEntityAsync(string entityType, Guid entityId, CancellationToken ct);

        /// <summary>Searches for annotations by key and optional value (cross-entity lookup).</summary>
        Task<IReadOnlyList<Annotation>> SearchAsync(string key, string? value, CancellationToken ct);

        /// <summary>Deletes an annotation.</summary>
        Task DeleteAsync(Guid annotationId, CancellationToken ct);

        /// <summary>Deletes all annotations for a specific entity.</summary>
        Task DeleteForEntityAsync(string entityType, Guid entityId, CancellationToken ct);
    }
}
