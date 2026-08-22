using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

using VideoForensics.Providers.Ring.Entities;

namespace VideoForensics.Providers.Ring.Utils
{
    /// <summary>
    /// Compares actual API JSON responses against declared entity schemas, reporting type
    /// mismatches, missing fields, and unused schema properties. Helps catch silent
    /// deserialization failures (where System.Text.Json ignores unmatched properties).
    /// </summary>
    public class JsonSchemaValidator
    {
        public class SchemaIssue
        {
            public required string Path { get; set; }
            public required string IssueType { get; set; } // "TypeMismatch", "MissingInSchema", "UnusedInSchema", "NullabilityMismatch"
            public required string Expected { get; set; }
            public required string Actual { get; set; }
            public required string Severity { get; set; } // "Error", "Warning", "Info"
        }

        private static readonly Dictionary<string, Type> EntityTypeCache = new();

        static JsonSchemaValidator()
        {
            // Pre-cache all entity types from the Entities namespace
            var entityAssembly = typeof(Profile).Assembly;
            foreach (var type in entityAssembly.GetTypes()
                .Where(t => t.Namespace == "Ring.Api.Entities" && !t.IsAbstract && !t.IsInterface))
            {
                EntityTypeCache[type.Name] = type;
            }
        }

        public List<SchemaIssue> ValidateAgainstSchema(JsonElement actualResponse, Type expectedType)
        {
            var issues = new List<SchemaIssue>();
            if (expectedType == null || actualResponse.ValueKind == JsonValueKind.Null)
                return issues;

            ValidateType(actualResponse, expectedType, "$", issues);
            return issues;
        }

        private void ValidateType(JsonElement json, Type schemaType, string path, List<SchemaIssue> issues)
        {
            if (json.ValueKind == JsonValueKind.Null)
            {
                // Null is OK for nullable types, check if schema expects it
                if (!IsNullableType(schemaType))
                {
                    issues.Add(new SchemaIssue
                    {
                        Path = path,
                        IssueType = "NullabilityMismatch",
                        Expected = schemaType.Name,
                        Actual = "null",
                        Severity = "Warning"
                    });
                }
                return;
            }

            var underlyingType = Nullable.GetUnderlyingType(schemaType) ?? schemaType;

            if (underlyingType == typeof(string))
            {
                if (json.ValueKind != JsonValueKind.String)
                {
                    issues.Add(new SchemaIssue
                    {
                        Path = path,
                        IssueType = "TypeMismatch",
                        Expected = "String",
                        Actual = json.ValueKind.ToString(),
                        Severity = "Error"
                    });
                }
            }
            else if (underlyingType == typeof(int) || underlyingType == typeof(long) ||
                     underlyingType == typeof(decimal) || underlyingType == typeof(double))
            {
                if (json.ValueKind == JsonValueKind.String)
                {
                    // Numeric string - might be OK if converter exists, flag as warning
                    if (double.TryParse(json.GetString(), out _))
                    {
                        issues.Add(new SchemaIssue
                        {
                            Path = path,
                            IssueType = "TypeMismatch",
                            Expected = underlyingType.Name,
                            Actual = "String (numeric-looking)",
                            Severity = "Warning"
                        });
                    }
                    else
                    {
                        issues.Add(new SchemaIssue
                        {
                            Path = path,
                            IssueType = "TypeMismatch",
                            Expected = underlyingType.Name,
                            Actual = "String (non-numeric)",
                            Severity = "Error"
                        });
                    }
                }
                else if (json.ValueKind != JsonValueKind.Number)
                {
                    issues.Add(new SchemaIssue
                    {
                        Path = path,
                        IssueType = "TypeMismatch",
                        Expected = underlyingType.Name,
                        Actual = json.ValueKind.ToString(),
                        Severity = "Error"
                    });
                }
            }
            else if (underlyingType == typeof(bool))
            {
                if (json.ValueKind != JsonValueKind.True && json.ValueKind != JsonValueKind.False)
                {
                    issues.Add(new SchemaIssue
                    {
                        Path = path,
                        IssueType = "TypeMismatch",
                        Expected = "Boolean",
                        Actual = json.ValueKind.ToString(),
                        Severity = "Error"
                    });
                }
            }
            else if (json.ValueKind == JsonValueKind.Object)
            {
                ValidateObject(json, underlyingType, path, issues);
            }
            else if (json.ValueKind == JsonValueKind.Array)
            {
                ValidateArray(json, underlyingType, path, issues);
            }
        }

        private void ValidateObject(JsonElement jsonObj, Type schemaType, string path, List<SchemaIssue> issues)
        {
            var schemaProperties = GetJsonProperties(schemaType);
            var jsonProps = new HashSet<string>(jsonObj.EnumerateObject().Select(p => p.Name), StringComparer.Ordinal);
            var usedSchemaProps = new HashSet<string>(StringComparer.Ordinal);

            // Check each schema property
            foreach (var schemaProp in schemaProperties)
            {
                var jsonPropName = schemaProp.JsonName;
                usedSchemaProps.Add(jsonPropName);

                if (jsonObj.TryGetProperty(jsonPropName, out var jsonProp))
                {
                    ValidateType(jsonProp, schemaProp.PropertyType, $"{path}.{jsonPropName}", issues);
                }
                // Missing in JSON is OK (nullable/optional fields are fine)
            }

            // Check for extra fields in JSON not in schema
            var extraJsonProps = jsonProps.Except(usedSchemaProps);
            foreach (var extra in extraJsonProps)
            {
                issues.Add(new SchemaIssue
                {
                    Path = $"{path}.{extra}",
                    IssueType = "MissingInSchema",
                    Expected = "Schema property",
                    Actual = jsonObj.GetProperty(extra).ValueKind.ToString(),
                    Severity = "Info"
                });
            }
        }

        private void ValidateArray(JsonElement jsonArray, Type schemaType, string path, List<SchemaIssue> issues)
        {
            // Get the generic argument (T from List<T>)
            if (!schemaType.IsGenericType)
            {
                // Can't validate raw array without generic info
                return;
            }

            var elementType = schemaType.GetGenericArguments().FirstOrDefault();
            if (elementType == null)
                return;

            var arrayEnum = jsonArray.EnumerateArray();
            int index = 0;
            foreach (var item in arrayEnum)
            {
                ValidateType(item, elementType, $"{path}[{index}]", issues);
                index++;
            }
        }

        private class JsonPropertyInfo
        {
            public required string JsonName { get; set; }
            public required Type PropertyType { get; set; }
        }

        private List<JsonPropertyInfo> GetJsonProperties(Type type)
        {
            var props = new List<JsonPropertyInfo>();
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var jsonAttr = prop.GetCustomAttribute<JsonPropertyNameAttribute>();
                var jsonName = jsonAttr?.Name ?? prop.Name;
                props.Add(new JsonPropertyInfo { JsonName = jsonName, PropertyType = prop.PropertyType });
            }
            return props;
        }

        private bool IsNullableType(Type type)
        {
            return Nullable.GetUnderlyingType(type) != null || !type.IsValueType;
        }
    }
}

