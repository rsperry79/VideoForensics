namespace VideoForensics.Client.Common.Contracts
{
    /// <summary>
    /// Reads and changes the server's update release channel (Stable or Testing).
    /// Implemented remotely by the client host; the server enforces who may read or change it.
    /// </summary>
    public interface IReleaseChannelService
    {
        /// <summary>Returns the server's currently configured update release channel.</summary>
        /// <param name="cancellationToken">Cancels the underlying request.</param>
        /// <exception cref="HttpRequestException">The server returned a non-success status.</exception>
        /// <exception cref="InvalidOperationException">The server returned a channel name this client does not recognise.</exception>
        Task<UpdateReleaseChannel> GetReleaseChannelAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets the server's update release channel. Requires SuperAdmin on the Local tier on the server.
        /// </summary>
        /// <param name="channel">The channel to switch to.</param>
        /// <param name="cancellationToken">Cancels the underlying request.</param>
        /// <returns>The channel the server reports as active after the change.</returns>
        /// <exception cref="HttpRequestException">The server rejected the request (for example 400 or 403).</exception>
        /// <exception cref="InvalidOperationException">The server returned a channel name this client does not recognise.</exception>
        Task<UpdateReleaseChannel> SetReleaseChannelAsync(UpdateReleaseChannel channel, CancellationToken cancellationToken = default);
    }
}
