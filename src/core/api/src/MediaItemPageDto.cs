namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>
    /// One page of media items for the media viewer. <see cref="TotalCount"/> counts every match, not just this page.
    /// </summary>
    public sealed record MediaItemPageDto(IReadOnlyList<MediaItemDto> Items, int TotalCount);
}
