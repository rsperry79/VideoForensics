using System.Globalization;
using System.Text.Json;

namespace VideoForensics.Providers.Ring.Services
{
    /// <summary>
    /// Lenient scalar readers for Ring JSON. Each returns true when the value was usable (a JSON null is usable and yields null) and
    /// false when its shape does not fit the target type, so callers can note the value as not stored instead of guessing.
    /// </summary>
    internal static class RingJsonValue
    {
        public static bool TryBool(JsonElement v, out bool? result)
        {
            result = v.ValueKind switch { JsonValueKind.True => true, JsonValueKind.False => false, _ => null };
            return result != null || v.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined;
        }

        public static bool TryDouble(JsonElement v, out double? result)
        {
            result = null;
            if (v.ValueKind == JsonValueKind.Number)
            {
                result = v.GetDouble();
                return true;
            }

            if (v.ValueKind == JsonValueKind.String && double.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
            {
                result = d;
                return true;
            }

            return v.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined;
        }

        public static bool TryDecimal(JsonElement v, out decimal? result)
        {
            result = null;
            if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out decimal m))
            {
                result = m;
                return true;
            }

            if (v.ValueKind == JsonValueKind.String && decimal.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out decimal p))
            {
                result = p;
                return true;
            }

            return v.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined;
        }

        public static bool TryLong(JsonElement v, out long? result)
        {
            result = null;
            if (v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out long n))
            {
                result = n;
                return true;
            }

            if (v.ValueKind == JsonValueKind.String && long.TryParse(v.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long s))
            {
                result = s;
                return true;
            }

            return v.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined;
        }

        public static bool TryInt(JsonElement v, out int? result)
        {
            bool ok = TryLong(v, out long? n);
            if (n is >= int.MinValue and <= int.MaxValue)
            {
                result = (int)n.Value;
                return ok;
            }

            result = null;
            return ok && n == null;
        }

        /// <summary>Strings as-is; numbers and booleans as their text; objects and arrays are never turned into text.</summary>
        public static bool TryText(JsonElement v, out string? result)
        {
            result = v.ValueKind switch
            {
                JsonValueKind.String => v.GetString(),
                JsonValueKind.Number => v.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null
            };
            return result != null || v.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined;
        }

        /// <summary>An ISO-8601 string as a UTC time. Numbers and other shapes are not interpreted (the unit is unknown).</summary>
        public static bool TryUtcTime(JsonElement v, out DateTime? result)
        {
            result = null;
            if (v.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(v.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset t))
            {
                result = t.UtcDateTime;
                return true;
            }

            return v.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined;
        }
    }
}
