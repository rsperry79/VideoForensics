using System;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

#nullable enable

namespace VideoForensics.Providers.Ring;

public enum JsonMode
{
    Default,  // Compact, safe escaping
    Pretty,   // Indented for readability
    Raw       // Unsafe escaping for API responses
}

public static class JsonUtil
{
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        WriteIndented = false,
        Encoder = JavaScriptEncoder.Default,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly JsonSerializerOptions PrettyOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Default,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly JsonSerializerOptions RawOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Serialize<T>(T obj, JsonMode mode = JsonMode.Default)
    {
        var options = mode switch
        {
            JsonMode.Pretty => PrettyOptions,
            JsonMode.Raw => RawOptions,
            _ => DefaultOptions
        };

        return JsonSerializer.Serialize(obj, options);
    }

    public static T? Deserialize<T>(string json, JsonMode mode = JsonMode.Default)
    {
        var options = mode switch
        {
            JsonMode.Pretty => PrettyOptions,
            JsonMode.Raw => RawOptions,
            _ => DefaultOptions
        };

        return JsonSerializer.Deserialize<T>(json, options);
    }

    public static T? Deserialize<T>(ReadOnlySpan<byte> utf8Json, JsonMode mode = JsonMode.Default)
    {
        var options = mode switch
        {
            JsonMode.Pretty => PrettyOptions,
            JsonMode.Raw => RawOptions,
            _ => DefaultOptions
        };

        return JsonSerializer.Deserialize<T>(utf8Json, options);
    }
}
