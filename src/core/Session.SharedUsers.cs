using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Ring.Api.Entities;

namespace Ring.Api
{
    /// <summary>
    /// Read-only visibility into who has access to a Ring location, and any pending invitations.
    /// Useful for diagnosing why a shared account sees fewer devices than the owner - Ring grants
    /// access per-device to shared users, and doesn't extend that access automatically when new
    /// devices are added, so this is how to confirm a device gap is a permissions issue rather than
    /// an app bug (see api_raw_responses.jsonl's is_owner: false / empty health data investigation).
    /// No invite/remove mutation methods are included - those are destructive account-permission
    /// changes better left to the Ring app itself.
    /// </summary>
    public partial class Session
    {
        private class InvitationsResponse
        {
            [JsonPropertyName("invitations")]
            public List<Invitation> Invitations { get; set; }
        }

        /// <summary>
        /// Returns the users who currently have access to the given location. Response shape
        /// (a bare JSON array, not a {"users": [...]} wrapper) is confirmed against a live capture -
        /// see SharedUser.cs.
        /// </summary>
        /// <param name="locationId">ID of the location to retrieve shared users for</param>
        /// <exception cref="Exceptions.AuthenticationFailedException">Thrown when the refresh token is invalid.</exception>
        /// <exception cref="Exceptions.SessionNotAuthenticatedException">Thrown when there's no OAuth token, or the OAuth token has expired and there is no valid refresh token.</exception>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        public async Task<List<SharedUser>> GetSharedUsers(Guid locationId)
        {
            await EnsureSessionValid();

            var uri = new Uri(BaseUrl, $"locations/{locationId:D}/users");
            var response = await _httpUtility.GetContents(uri, AuthenticationToken, _hardwareId);

            return JsonSerializer.Deserialize<List<SharedUser>>(response) ?? new List<SharedUser>();
        }

        /// <summary>
        /// Returns pending invitations for the given location. Response shape is inferred (see
        /// Invitation.cs) - not confirmed against a live capture.
        /// </summary>
        /// <param name="locationId">ID of the location to retrieve invitations for</param>
        /// <exception cref="Exceptions.AuthenticationFailedException">Thrown when the refresh token is invalid.</exception>
        /// <exception cref="Exceptions.SessionNotAuthenticatedException">Thrown when there's no OAuth token, or the OAuth token has expired and there is no valid refresh token.</exception>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        public async Task<List<Invitation>> GetInvitations(Guid locationId)
        {
            await EnsureSessionValid();

            var uri = new Uri(BaseUrl, $"locations/{locationId:D}/invitations");
            var response = await _httpUtility.GetContents(uri, AuthenticationToken, _hardwareId);

            var parsed = JsonSerializer.Deserialize<InvitationsResponse>(response);
            return parsed?.Invitations ?? new List<Invitation>();
        }
    }
}
