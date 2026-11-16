using System.Text.Json;

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

using VideoForensics.Client.Ui.Shared.Services;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Services;

/// <summary>
/// <see cref="WebPushClient.SubscribeAsync"/> must read the JS subscribe result as a typed object.
/// Blazor hands a JS object to a dynamic target as a <c>JsonElement</c>, and a dynamic indexer on it throws.
/// </summary>
public class WebPushClientTests
{
    /// <summary>JS runtime stub: deserializes a fixed JSON object into whatever type the caller asks for, as Blazor does.</summary>
    private sealed class JsonResultJsRuntime : IJSRuntime
    {
        private readonly string _json;

        public JsonResultJsRuntime(string json)
        {
            _json = json;
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            return ValueTask.FromResult(JsonSerializer.Deserialize<TValue>(_json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            return ValueTask.FromResult(JsonSerializer.Deserialize<TValue>(_json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!);
        }
    }

    [Fact]
    public async Task SubscribeAsync_JsObjectResult_MapsEndpointAndKeys()
    {
        var client = new WebPushClient(
            new JsonResultJsRuntime("{\"endpoint\":\"https://push.example.com/abc\",\"p256dhKey\":\"p256-key-value\",\"authKey\":\"auth-key-value\"}"),
            null!);

        (string endpoint, string p256dh, string auth) = await client.SubscribeAsync("vapid-public-key");

        Assert.Equal("https://push.example.com/abc", endpoint);
        Assert.Equal("p256-key-value", p256dh);
        Assert.Equal("auth-key-value", auth);
    }

    [Fact]
    public async Task SubscribeAsync_MissingAuthKey_ThrowsInvalidOperation()
    {
        var client = new WebPushClient(
            new JsonResultJsRuntime("{\"endpoint\":\"https://push.example.com/abc\",\"p256dhKey\":\"p256-key-value\",\"authKey\":\"\"}"),
            null!);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => client.SubscribeAsync("vapid-public-key"));
    }
}
