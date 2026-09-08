namespace VideoForensics.Hosting.ServerDiscovery
{
    /// <summary>
    /// Resolves the server address using local mDNS discovery, with fallback to a cached Internet URL.
    /// </summary>
    public interface IServerLocationResolver
    {
        /// <summary>
        /// Resolves the server address by attempting local mDNS discovery first, then falling back to
        /// a cached Internet URL if local discovery times out or fails.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to interrupt discovery.</param>
        /// <returns>A Uri to the server.</returns>
        /// <exception cref="ServerNotReachableException">
        /// Thrown when local discovery fails and no cached Internet URL is available.
        /// </exception>
        Task<Uri> ResolveServerAddressAsync(CancellationToken cancellationToken);
    }
}
