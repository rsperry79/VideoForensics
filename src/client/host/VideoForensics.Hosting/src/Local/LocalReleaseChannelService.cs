using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.Hosting.Local
{
    /// <summary>
    /// In-process <see cref="IReleaseChannelService"/> for the server tier: reads and persists the update release channel
    /// directly through the server's configuration instead of calling the HTTP API.
    /// </summary>
    /// <param name="config">The server's live configuration, whose <see cref="IForensicsConfiguration.ReleaseChannel"/> is read and updated.</param>
    /// <param name="configService">Persists the configuration after a change.</param>
    public sealed class LocalReleaseChannelService(IForensicsConfiguration config, IForensicsConfigurationService configService) : IReleaseChannelService
    {
        /// <inheritdoc />
        public Task<UpdateReleaseChannel> GetReleaseChannelAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(config.ReleaseChannel);

        /// <inheritdoc />
        public async Task<UpdateReleaseChannel> SetReleaseChannelAsync(UpdateReleaseChannel channel, CancellationToken cancellationToken = default)
        {
            config.ReleaseChannel = channel;
            await configService.SaveConfigurationAsync(config, cancellationToken).ConfigureAwait(false);
            return channel;
        }
    }
}
