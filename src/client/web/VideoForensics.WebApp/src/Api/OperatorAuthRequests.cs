using Fido2NetLib;
using Fido2NetLib.Objects;

using Microsoft.AspNetCore.Identity;

using System.Net;
using System.Security.Claims;
using System.Text.Json;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting;
using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Client.WebApp.Services;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.WebApp.Api
{
    // Request/response DTOs
    /// <param name="Username">The operator signing in.</param>
    /// <param name="AllCredentials">False offers only the most recently used passkey; true offers every approved passkey (the fallback).</param>
    public record UsernameAssertionOptionsRequest(string Username, bool AllCredentials = false);
    /// <param name="Username">The name the operator typed before the passkey prompt. Used only to record a failed attempt against that name when no operator matches.</param>
    public record UsernameAssertionCompleteRequest(string Nonce, JsonElement AssertionResponse, string? TwoFactorCorrelationId = null, string? Username = null);
    public record CredentialRegisterOptionsRequest(string Label);
    public record CredentialRegisterCompleteRequest(string Nonce, JsonElement AttestationResponse, string Label);
    public record RegisterRequest(string Username, string Password, string FirstName, string LastName, string Email, string? Phone = null, string? DisplayName = null);
    /// <param name="PasskeyAvailable">False when this device cannot run a passkey ceremony (Windows Hello disabled). Only honored for a direct local request.</param>
    public record LoginPasswordRequest(string Username, string Password, bool PasskeyAvailable = true);
    public record ChangePasswordRequest(string? CurrentPassword, string NewPassword);
    public record StepUpPasswordRequest(string Password);

    internal record PendingCredentialRegistration(Guid OperatorId, string Label, string OptionsJson);
}
