using System.Reflection;
using Microsoft.Extensions.Logging;
using VideoForensics.Data.Core.Contracts;

namespace VideoForensics.Data.Core.Services
{
    /// <summary>Service for redacting sensitive information from report DTOs for external export.</summary>
    internal class RedactionService : IRedactionService
    {
        private readonly ILogger<RedactionService> _logger;

        public RedactionService(ILogger<RedactionService> logger)
        {
            _logger = logger;
        }

        public T RedactForExport<T>(T reportDto, RedactionLevel level) where T : class
        {
            if (level == RedactionLevel.None)
            {
                _logger.LogInformation("No redaction applied (level=None)");
                return reportDto;
            }

            // Create a deep clone of the DTO
            var cloned = DeepClone(reportDto);

            _logger.LogInformation("Redacting report of type {ReportType} with level {RedactionLevel}",
                typeof(T).Name, level);

            // Apply redaction based on level
            ApplyRedaction(cloned, level);

            return cloned;
        }

        private void ApplyRedaction<T>(T obj, RedactionLevel level) where T : class
        {
            if (obj == null) return;

            var properties = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in properties)
            {
                if (!prop.CanRead || !prop.CanWrite) continue;

                var value = prop.GetValue(obj);
                if (value == null) continue;

                // Apply field-level redaction based on property name
                if (level >= RedactionLevel.Light)
                {
                    if (IsEmailProperty(prop.Name))
                    {
                        prop.SetValue(obj, MaskEmail(value.ToString()));
                    }
                    else if (IsPhoneProperty(prop.Name))
                    {
                        prop.SetValue(obj, MaskPhone(value.ToString()));
                    }
                }

                if (level >= RedactionLevel.Medium)
                {
                    if (IsAddressProperty(prop.Name))
                    {
                        prop.SetValue(obj, "[REDACTED_ADDRESS]");
                    }
                    else if (IsCoordinateProperty(prop.Name))
                    {
                        prop.SetValue(obj, "[REDACTED_COORDINATES]");
                    }
                }

                if (level >= RedactionLevel.Heavy)
                {
                    if (IsPersonNameProperty(prop.Name))
                    {
                        prop.SetValue(obj, "[REDACTED_PERSON]");
                    }
                    else if (IsGpsProperty(prop.Name))
                    {
                        prop.SetValue(obj, "[REDACTED_GPS]");
                    }
                }

                // Recursively redact nested objects
                if (value is not string and not ValueType)
                {
                    ApplyRedaction(value, level);
                }
            }

            // Redact collections
            foreach (var prop in properties)
            {
                if (!prop.CanRead || !prop.CanWrite) continue;

                var value = prop.GetValue(obj);
                if (value is System.Collections.IEnumerable enumerable and not string)
                {
                    foreach (var item in enumerable)
                    {
                        if (item is not string and not ValueType)
                        {
                            ApplyRedaction(item, level);
                        }
                    }
                }
            }
        }

        private T DeepClone<T>(T obj) where T : class
        {
            if (obj == null) return null!;

            var type = obj.GetType();

            // For simple types and strings, return as-is
            if (type.IsValueType || type == typeof(string))
                return obj;

            // Use reflection to create a new instance and copy properties
            var clone = Activator.CreateInstance(type) as T;
            if (clone == null) return obj;

            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                if (!prop.CanRead || !prop.CanWrite) continue;

                var value = prop.GetValue(obj);
                if (value != null)
                {
                    if (value is System.Collections.IEnumerable enumerable and not string)
                    {
                        // Clone collections
                        var listType = typeof(List<>);
                        var itemType = prop.PropertyType.GetGenericArguments().FirstOrDefault() ?? typeof(object);
                        var listGeneric = listType.MakeGenericType(itemType);
                        var newList = Activator.CreateInstance(listGeneric);
                        var addMethod = listGeneric.GetMethod("Add");

                        foreach (var item in enumerable)
                        {
                            var clonedItem = item is not string and not ValueType
                                ? DeepClone(item)
                                : item;
                            addMethod?.Invoke(newList, new[] { clonedItem });
                        }

                        prop.SetValue(clone, newList);
                    }
                    else if (value is not string and not ValueType)
                    {
                        // Clone nested objects
                        var clonedValue = DeepClone(value);
                        prop.SetValue(clone, clonedValue);
                    }
                    else
                    {
                        prop.SetValue(clone, value);
                    }
                }
            }

            return clone;
        }

        private bool IsEmailProperty(string propName) =>
            propName.Contains("Email", StringComparison.OrdinalIgnoreCase);

        private bool IsPhoneProperty(string propName) =>
            propName.Contains("Phone", StringComparison.OrdinalIgnoreCase);

        private bool IsAddressProperty(string propName) =>
            propName.Contains("Address", StringComparison.OrdinalIgnoreCase);

        private bool IsCoordinateProperty(string propName) =>
            propName.Contains("Latitude", StringComparison.OrdinalIgnoreCase) ||
            propName.Contains("Longitude", StringComparison.OrdinalIgnoreCase) ||
            propName.Contains("Coordinate", StringComparison.OrdinalIgnoreCase);

        private bool IsPersonNameProperty(string propName) =>
            propName.Contains("Person", StringComparison.OrdinalIgnoreCase) ||
            propName.Contains("Name", StringComparison.OrdinalIgnoreCase);

        private bool IsGpsProperty(string propName) =>
            propName.Contains("GPS", StringComparison.OrdinalIgnoreCase) ||
            propName.Contains("Location", StringComparison.OrdinalIgnoreCase);

        private string MaskEmail(string? email)
        {
            if (string.IsNullOrEmpty(email)) return "[REDACTED_EMAIL]";

            var parts = email.Split('@');
            if (parts.Length != 2) return "[REDACTED_EMAIL]";

            var localPart = parts[0];
            var domain = parts[1];
            var maskedLocal = localPart.Length > 2
                ? $"{localPart[0]}***{localPart[^1]}"
                : "***";

            return $"{maskedLocal}@{domain}";
        }

        private string MaskPhone(string? phone)
        {
            if (string.IsNullOrEmpty(phone)) return "[REDACTED_PHONE]";

            var digits = new string(phone.Where(char.IsDigit).ToArray());
            if (digits.Length < 4) return "[REDACTED_PHONE]";

            return $"***-***-{digits[^4..]}";
        }
    }
}
