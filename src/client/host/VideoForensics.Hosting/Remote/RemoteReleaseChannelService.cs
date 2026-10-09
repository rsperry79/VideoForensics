using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed <see cref="IReleaseChannelService"/> that calls the server's versioned Minimal API
    /// (GET/PUT /api/v1/update-settings/release-channel). Bearer auth is attached by the shared
    /// paired-device handler, so this class carries no auth logic of its own.
    /// </summary>
    public class RemoteReleaseChannelService : IReleaseChannelService
    {
        private const string ReleaseChannelRoute = "/api/v1/update-settings/release-channel";

        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;

        public RemoteReleaseChannelService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<UpdateReleaseChannel> GetReleaseChannelAsync(CancellationToken cancellationToken = default)
        {
            HttpResponseMessage response = await _httpClient.GetAsync(ReleaseChannelRoute, cancellationToken);
            _ = response.EnsureSuccessStatusCode();
            ReleaseChannelDto? dto = await response.Content.ReadFromJsonAsync<ReleaseChannelDto>(JsonOptions, cancellationToken);
            return ParseChannel(dto);
        }

        /// <inheritdoc />
        public async Task<UpdateReleaseChannel> SetReleaseChannelAsync(UpdateReleaseChannel channel, CancellationToken cancellationToken = default)
        {
            // Send the enum name as a string: the server's contract is the channel name, not an ordinal,
            // so a reordered enum on the client can never silently change which channel is requested.
            var request = new SetReleaseChannelRequest(channel.ToString());
            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(ReleaseChannelRoute, request, cancellationToken);
            _ = response.EnsureSuccessStatusCode();
            ReleaseChannelDto? dto = await response.Content.ReadFromJsonAsync<ReleaseChannelDto>(JsonOptions, cancellationToken);
            return ParseChannel(dto);
        }

        /// <summary>
        /// Maps the wire channel string to <see cref="UpdateReleaseChannel"/>. Matching is case-insensitive
        /// to mirror the server, and IsDefined guards against numeric strings that TryParse would accept.
        /// An unrecognised value is a server/client contract mismatch, so it fails loudly instead of defaulting.
        /// </summary>
        private static UpdateReleaseChannel ParseChannel(ReleaseChannelDto? dto)
        {
            string? raw = dto?.Channel;
            if (raw is null
                || !Enum.TryParse(raw, ignoreCase: true, out UpdateReleaseChannel channel)
                || !Enum.IsDefined(channel))
            {
                throw new InvalidOperationException("Server returned an unrecognised update release channel.");
            }

            return channel;
        }
    }
}
