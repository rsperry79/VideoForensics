namespace VideoForensics.Core.Api.Contracts.Repositories
{
    /// <summary>Operator reads needed before sign-in, with DTO signatures. Client hosts use this interface.</summary>
    public interface IOperatorRepository
    {
        /// <summary>True when no operator exists yet (first-run setup).</summary>
        Task<bool> IsEmptyAsync(CancellationToken ct);
    }
}
