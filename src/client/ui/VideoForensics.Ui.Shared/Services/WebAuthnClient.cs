using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

using System.Net.Http.Json;
using System.Text.Json;

namespace VideoForensics.Ui.Shared.Services
{
    /// <summary>
    /// Drives the three WebAuthn ceremonies against the server's pairing/auth API (plan §5.1/§5.7):
    /// passkey registration (pairing), passkey authentication (session sign-in), and step-up
    /// re-authentication. Each ceremony is the same three-step shape - fetch options from the
    /// server, hand them to the browser's native WebAuthn API via <c>wwwroot/js/webauthn.js</c>,
    /// post the result back - factored once here rather than duplicated across Pair.razor,
    /// DeviceSignIn.razor, and every step-up-gated action.
    /// </summary>
    public class WebAuthnClient
    {
        private readonly IJSRuntime _js;
        private readonly NavigationManager _nav;

        public WebAuthnClient(IJSRuntime js, NavigationManager nav)
        {
            _js = js;
            _nav = nav;
        }

        public bool IsBrowserSupportRequired => true;

        public async Task<bool> IsAvailableAsync()
        {
            try
            {
                return await _js.InvokeAsync<bool>("vfWebAuthn.isAvailable");
            }
            catch (JSException)
            {
                return false;
            }
        }

        public async Task<RegistrationResult> CompleteRegistrationAsync(
            string pairingToken, string operatorDisplayName, string deviceName)
        {
            using HttpClient client = CreateClient(null);

            HttpResponseMessage optionsResponse = await client.PostAsJsonAsync(
                $"api/pairing/{pairingToken}/register/options",
                new { operatorDisplayName, deviceName });
            if (!optionsResponse.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(await ExtractErrorAsync(optionsResponse));
            }

            JsonElement optionsBody = await optionsResponse.Content.ReadFromJsonAsync<JsonElement>();
            string nonce = optionsBody.GetProperty("nonce").GetString()!;
            string optionsJson = optionsBody.GetProperty("options").GetRawText();

            string attestationJson = await _js.InvokeAsync<string>("vfWebAuthn.register", optionsJson);
            JsonElement attestation = JsonSerializer.Deserialize<JsonElement>(attestationJson);

            HttpResponseMessage completeResponse = await client.PostAsJsonAsync(
                $"api/pairing/{pairingToken}/register/complete",
                new { nonce, attestationResponse = attestation });
            if (!completeResponse.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(await ExtractErrorAsync(completeResponse));
            }

            JsonElement result = await completeResponse.Content.ReadFromJsonAsync<JsonElement>();
            return new RegistrationResult(
                result.GetProperty("operatorId").GetGuid(),
                result.GetProperty("pairedDeviceId").GetGuid(),
                result.GetProperty("role").GetString()!,
                result.GetProperty("isApproved").GetBoolean());
        }

        public async Task<(string SessionToken, Guid OperatorId, string Role)> SignInAsync()
        {
            using HttpClient client = CreateClient(null);

            HttpResponseMessage optionsResponse = await client.PostAsync("api/auth/webauthn/assertion-options", null);
            if (!optionsResponse.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(await ExtractErrorAsync(optionsResponse));
            }

            JsonElement optionsBody = await optionsResponse.Content.ReadFromJsonAsync<JsonElement>();
            string nonce = optionsBody.GetProperty("nonce").GetString()!;
            string optionsJson = optionsBody.GetProperty("options").GetRawText();

            string assertionJson = await _js.InvokeAsync<string>("vfWebAuthn.authenticate", optionsJson);
            JsonElement assertion = JsonSerializer.Deserialize<JsonElement>(assertionJson);

            HttpResponseMessage completeResponse = await client.PostAsJsonAsync(
                "api/auth/webauthn/assertion-complete",
                new { nonce, assertionResponse = assertion });
            if (!completeResponse.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(await ExtractErrorAsync(completeResponse));
            }

            JsonElement result = await completeResponse.Content.ReadFromJsonAsync<JsonElement>();
            return (
                result.GetProperty("sessionToken").GetString()!,
                result.GetProperty("operatorId").GetGuid(),
                result.GetProperty("role").GetString()!);
        }

        /// <summary>Fresh passkey assertion for an already-signed-in session (plan §5.7) - returns the short-lived step-up token to attach as the X-StepUp-Token header on the one protected call it authorizes.</summary>
        public async Task<string> StepUpAsync(string sessionToken)
        {
            using HttpClient client = CreateClient(sessionToken);

            HttpResponseMessage optionsResponse = await client.PostAsync("api/auth/webauthn/assertion-options", null);
            if (!optionsResponse.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(await ExtractErrorAsync(optionsResponse));
            }

            JsonElement optionsBody = await optionsResponse.Content.ReadFromJsonAsync<JsonElement>();
            string nonce = optionsBody.GetProperty("nonce").GetString()!;
            string optionsJson = optionsBody.GetProperty("options").GetRawText();

            string assertionJson = await _js.InvokeAsync<string>("vfWebAuthn.authenticate", optionsJson);
            JsonElement assertion = JsonSerializer.Deserialize<JsonElement>(assertionJson);

            HttpResponseMessage completeResponse = await client.PostAsJsonAsync(
                "api/auth/webauthn/stepup-complete",
                new { nonce, assertionResponse = assertion });
            if (!completeResponse.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(await ExtractErrorAsync(completeResponse));
            }

            JsonElement result = await completeResponse.Content.ReadFromJsonAsync<JsonElement>();
            return result.GetProperty("stepUpToken").GetString()!;
        }

        private HttpClient CreateClient(string? bearerToken)
        {
            var client = new HttpClient { BaseAddress = new Uri(_nav.BaseUri) };
            if (bearerToken is not null)
            {
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
            }

            return client;
        }

        private static async Task<string> ExtractErrorAsync(HttpResponseMessage response)
        {
            try
            {
                Dictionary<string, string>? doc = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
                if (doc is not null && doc.TryGetValue("error", out string? msg))
                {
                    return msg;
                }
            }
            catch (JsonException)
            {
            }

            return $"Request failed: {response.StatusCode}";
        }
    }

    public record RegistrationResult(Guid OperatorId, Guid PairedDeviceId, string Role, bool IsApproved);
}
