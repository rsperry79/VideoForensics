using VideoForensics.Api.Contracts;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Auth;

namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// LLM settings API endpoints for configuring the embedded MCP-backed chat assistant (Milestone 8).
    /// Operators retrieve and update LLM provider configuration (provider, model, API key, base URL).
    /// Gated to SuperAdmin role only.
    /// </summary>
    public static class LlmSettingsEndpoints
    {
        public static void MapLlmSettingsEndpoints(this WebApplication app)
        {
            _ = app.MapGet("/api/v1/llm/settings", GetLlmSettingsAsync)
                .RequireAuthorization(VideoForensicsPolicies.SuperAdmin)
                .WithSummary("Get the current LLM configuration (provider, model, base URL)")
                .Produces<LlmSettingsResponseDto>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status403Forbidden)
                .Produces(StatusCodes.Status500InternalServerError);

            _ = app.MapPost("/api/v1/llm/settings", UpdateLlmSettingsAsync)
                .RequireAuthorization(VideoForensicsPolicies.SuperAdmin)
                .WithSummary("Update LLM configuration (provider, model, API key, base URL)")
                .Produces<LlmSettingsResponseDto>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status403Forbidden)
                .Produces(StatusCodes.Status500InternalServerError);
        }

        private static async Task<IResult> GetLlmSettingsAsync(
            ILlmApiKeyStore keyStore,
            ILogger<Program> logger,
            CancellationToken ct)
        {
            try
            {
                var provider = await keyStore.GetProviderAsync(ct);
                var model = await keyStore.GetModelAsync(ct);
                var baseUrl = await keyStore.GetBaseUrlAsync(ct);

                // Return empty strings for unset values
                var response = new LlmSettingsResponseDto(
                    Provider: provider ?? string.Empty,
                    Model: model ?? string.Empty,
                    BaseUrl: baseUrl ?? string.Empty
                );

                logger.LogInformation("LlmSettings: GET successful, provider={Provider}", provider ?? "(not set)");
                return Results.Ok(response);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "LlmSettings: GET failed with unhandled exception");
                return Results.StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        private static async Task<IResult> UpdateLlmSettingsAsync(
            LlmSettingsRequestDto request,
            ILlmApiKeyStore keyStore,
            ILogger<Program> logger,
            CancellationToken ct)
        {
            // Validate request
            if (request == null)
            {
                return Results.BadRequest(new { error = "Request body is required." });
            }

            if (string.IsNullOrWhiteSpace(request.Provider))
            {
                return Results.BadRequest(new { error = "Provider cannot be empty." });
            }

            if (request.Provider != "Anthropic" && request.Provider != "OpenAiCompatible")
            {
                return Results.BadRequest(new { error = "Provider must be 'Anthropic' or 'OpenAiCompatible'." });
            }

            if (string.IsNullOrWhiteSpace(request.Model))
            {
                return Results.BadRequest(new { error = "Model cannot be empty." });
            }

            try
            {
                // Update provider
                await keyStore.SetProviderAsync(request.Provider, ct);

                // Update model
                await keyStore.SetModelAsync(request.Model, ct);

                // Only update API key if non-null and non-empty
                if (!string.IsNullOrWhiteSpace(request.ApiKey))
                {
                    await keyStore.SetApiKeyAsync(request.ApiKey, ct);
                }

                // Update base URL (null/empty means delete)
                await keyStore.SetBaseUrlAsync(request.BaseUrl, ct);

                // Retrieve updated settings to return (excluding API key)
                var provider = await keyStore.GetProviderAsync(ct);
                var model = await keyStore.GetModelAsync(ct);
                var baseUrl = await keyStore.GetBaseUrlAsync(ct);

                var response = new LlmSettingsResponseDto(
                    Provider: provider ?? string.Empty,
                    Model: model ?? string.Empty,
                    BaseUrl: baseUrl ?? string.Empty
                );

                logger.LogInformation("LlmSettings: POST successful, provider={Provider}, model={Model}", provider, model);
                return Results.Ok(response);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "LlmSettings: POST failed with unhandled exception");
                return Results.StatusCode(StatusCodes.Status500InternalServerError);
            }
        }
    }
}
