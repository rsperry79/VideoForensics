using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Core.Api.Contracts.Repositories
{
    /// <summary>Server-side <see cref="IOperatorRepository"/> over <see cref="IOperatorWriteRepository"/>.</summary>
    public sealed class OperatorDtoRepository(IOperatorWriteRepository operators) : IOperatorRepository
    {
        /// <inheritdoc />
        public Task<bool> IsEmptyAsync(CancellationToken ct) => operators.IsEmptyAsync(ct);
    }
}
