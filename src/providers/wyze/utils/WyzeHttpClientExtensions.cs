using System.Net.Http;

namespace VideoForensics.Providers.Wyze.Utils;

/// <summary>Extension methods for HttpClient used with Wyze API</summary>
public static class WyzeHttpClientExtensions
{
    /// <summary>Adds default Wyze API headers to HTTP requests</summary>
    public static HttpClient AddWyzeHeaders(this HttpClient client)
    {
        client.DefaultRequestHeaders.Add("User-Agent", "WyzeVideoForensics/1.0");
        return client;
    }

    /// <summary>Adds authentication token to HTTP requests</summary>
    public static HttpClient AddAuthorizationHeader(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
