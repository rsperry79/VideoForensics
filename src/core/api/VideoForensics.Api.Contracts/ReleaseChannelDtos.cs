namespace VideoForensics.Api.Contracts
{
    /// <summary>
    /// Wire representation of the configured update release channel.
    /// </summary>
    /// <param name="Channel">Name of the release channel: "Stable" or "Testing".</param>
    public record ReleaseChannelDto(string Channel);

    /// <summary>
    /// Request body to change the update release channel. SuperAdmin-only on the server.
    /// </summary>
    /// <param name="Channel">Requested channel name, "Stable" or "Testing". Matched case-insensitively; anything else is rejected with 400.</param>
    public record SetReleaseChannelRequest(string Channel);
}
