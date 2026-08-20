using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Ring.Api.Entities;

namespace Ring.Api
{
    /// <summary>
    /// Ring Smart Lighting groups - collections of light fixtures at a location, separate from
    /// camera-attached floodlights/spotlights (see Session.DeviceControl.cs for those).
    /// </summary>
    public partial class Session
    {
        /// <summary>
        /// Base Uri for the Ring Smart Lighting groups API.
        /// </summary>
        public Uri GroupsApiBaseUrl => new Uri("https://api.ring.com/groups/v1/");

        /// <summary>
        /// Returns the Smart Lighting groups configured at the given location.
        /// </summary>
        /// <param name="locationId">ID of the location to retrieve light groups for</param>
        /// <exception cref="Exceptions.AuthenticationFailedException">Thrown when the refresh token is invalid.</exception>
        /// <exception cref="Exceptions.SessionNotAuthenticatedException">Thrown when there's no OAuth token, or the OAuth token has expired and there is no valid refresh token.</exception>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        public async Task<List<Group>> GetGroups(Guid locationId, CancellationToken cancellationToken = default)
        {
            await EnsureSessionValid(cancellationToken);

            var uri = new Uri(GroupsApiBaseUrl, $"locations/{locationId:D}/groups");
            var response = await _httpUtility.GetContents(uri, AuthenticationToken, _hardwareId, cancellationToken);

            var parsed = JsonSerializer.Deserialize<GroupsResponse>(response);
            return parsed?.DeviceGroups ?? new List<Group>();
        }

        /// <summary>
        /// Turns the lights in a Smart Lighting group on or off. The exact sub-path here
        /// (.../groups/{groupId}/devices) is inferred from ring-client-api's GROUP_DEVICES_ENDPOINT
        /// naming rather than confirmed against a reference implementation - verify against a live
        /// capture before relying on it.
        /// </summary>
        /// <param name="locationId">ID of the location the group belongs to</param>
        /// <param name="groupId">ID of the group to control (Group.DeviceGroupId)</param>
        /// <param name="on">True to turn the group's lights on, false to turn them off</param>
        /// <param name="durationSeconds">Optional duration in seconds to keep the lights on for. Leave NULL to let Ring use its default.</param>
        /// <exception cref="Exceptions.AuthenticationFailedException">Thrown when the refresh token is invalid.</exception>
        /// <exception cref="Exceptions.SessionNotAuthenticatedException">Thrown when there's no OAuth token, or the OAuth token has expired and there is no valid refresh token.</exception>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        public async Task SetGroupLights(Guid locationId, string groupId, bool on, int? durationSeconds = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(groupId))
            {
                throw new ArgumentNullException(nameof(groupId));
            }

            await EnsureSessionValid(cancellationToken);

            var uri = new Uri(GroupsApiBaseUrl, $"locations/{locationId:D}/groups/{groupId}/devices");
            var bodyContent = JsonSerializer.Serialize(new
            {
                lights_on = new { enabled = on, duration_seconds = durationSeconds }
            });
            await _httpUtility.SendRequestWithExpectedStatusOutcome(uri, System.Net.Http.HttpMethod.Post, null, bodyContent, AuthenticationToken, cancellationToken);
        }
    }
}
