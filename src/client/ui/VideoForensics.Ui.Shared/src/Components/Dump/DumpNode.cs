namespace VideoForensics.Ui.Shared.Components.Dump;

using System.Text.Json;
using System.Text.Json.Serialization;

public enum DumpNodeKind
{
    Object,
    Array,
    String,
    Number,
    Boolean,
    Null
}

public sealed class DumpNode
{
    public string Name { get; }
    public string Path { get; }
    public DumpNodeKind Kind { get; }
    public string? Value { get; }
    public IReadOnlyList<DumpNode> Children { get; }
    public bool IsTabular { get; }
    public IReadOnlyList<string> TableColumns { get; }

    public DumpNode(
        string name,
        string path,
        DumpNodeKind kind,
        string? value = null,
        IReadOnlyList<DumpNode>? children = null,
        bool isTabular = false,
        IReadOnlyList<string>? tableColumns = null)
    {
        Name = name;
        Path = path;
        Kind = kind;
        Value = value;
        Children = children ?? Array.Empty<DumpNode>();
        IsTabular = isTabular;
        TableColumns = tableColumns ?? Array.Empty<string>();
    }
}

public static class DumpNodeBuilder
{
    private const int MaxDepth = 64;
    private const string EllipsisValue = "…";

    public static DumpNode FromJson(string json)
    {
        try
        {
            var options = new JsonDocumentOptions { MaxDepth = MaxDepth + 1 };
            using var doc = JsonDocument.Parse(json, options);
            return BuildNode("", "$", doc.RootElement, 0);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Invalid JSON: {ex.Message}", nameof(json), ex);
        }
    }

    public static DumpNode FromObject(object? value)
    {
        if (value == null)
        {
            return new DumpNode("", "$", DumpNodeKind.Null, "null");
        }

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = null, // Keep .NET naming
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            Converters = { new JsonStringEnumConverter() }
        };

        var json = JsonSerializer.Serialize(value, options);
        return FromJson(json);
    }

    public static bool Matches(DumpNode node, string search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        var searchLower = search.ToLowerInvariant();

        // Check current node's name or value
        if (node.Name.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
            (node.Value != null && node.Value.Contains(searchLower, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // Check descendants
        return node.Children.Any(child => Matches(child, search));
    }

    private static DumpNode BuildNode(string name, string path, JsonElement element, int depth)
    {
        // Depth guard
        if (depth >= MaxDepth)
        {
            return new DumpNode(name, path, DumpNodeKind.String, EllipsisValue);
        }

        return element.ValueKind switch
        {
            JsonValueKind.Null => new DumpNode(name, path, DumpNodeKind.Null, "null"),

            JsonValueKind.True => new DumpNode(name, path, DumpNodeKind.Boolean, "true"),

            JsonValueKind.False => new DumpNode(name, path, DumpNodeKind.Boolean, "false"),

            JsonValueKind.Number => new DumpNode(
                name,
                path,
                DumpNodeKind.Number,
                element.GetRawText()),

            JsonValueKind.String => BuildStringNode(name, path, element.GetString() ?? "", depth),

            JsonValueKind.Array => BuildArrayNode(name, path, element, depth),

            JsonValueKind.Object => BuildObjectNode(name, path, element, depth),

            _ => new DumpNode(name, path, DumpNodeKind.String, element.GetRawText())
        };
    }

    private static DumpNode BuildStringNode(string name, string path, string value, int depth)
    {
        // Check if the string contains embedded JSON
        var trimmed = value.Trim();
        if ((trimmed.StartsWith('{') || trimmed.StartsWith('[')) && TryParseEmbeddedJson(trimmed, depth, out var parsedNode))
        {
            // Append " (json)" to the name and return the parsed node with modified name
            var newName = name + " (json)";
            return new DumpNode(
                newName,
                path,
                parsedNode.Kind,
                parsedNode.Value,
                parsedNode.Children,
                parsedNode.IsTabular,
                parsedNode.TableColumns);
        }

        return new DumpNode(name, path, DumpNodeKind.String, value);
    }

    private static bool TryParseEmbeddedJson(string json, int depth, out DumpNode? node)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            node = BuildNode("", "$", doc.RootElement, depth);
            return true;
        }
        catch
        {
            node = null;
            return false;
        }
    }

    private static DumpNode BuildObjectNode(string name, string path, JsonElement element, int depth)
    {
        var children = new List<DumpNode>();

        foreach (var property in element.EnumerateObject())
        {
            var propPath = $"{path}.{property.Name}";
            var propNode = BuildNode(property.Name, propPath, property.Value, depth + 1);
            children.Add(propNode);
        }

        return new DumpNode(name, path, DumpNodeKind.Object, null, children);
    }

    private static DumpNode BuildArrayNode(string name, string path, JsonElement element, int depth)
    {
        var children = new List<DumpNode>();
        var arrayElements = element.EnumerateArray().ToList();

        for (int i = 0; i < arrayElements.Count; i++)
        {
            var elemPath = $"{path}[{i}]";
            var elemNode = BuildNode($"[{i}]", elemPath, arrayElements[i], depth + 1);
            children.Add(elemNode);
        }

        // Determine if this is tabular (all non-empty children are objects)
        bool isTabular = false;
        var tableColumns = new List<string>();

        if (children.Count > 0 && children.All(c => c.Kind == DumpNodeKind.Object))
        {
            isTabular = true;
            // Collect all unique column names in first-seen order
            foreach (var child in children)
            {
                foreach (var col in child.Children)
                {
                    if (!tableColumns.Contains(col.Name))
                    {
                        tableColumns.Add(col.Name);
                    }
                }
            }
        }

        return new DumpNode(
            name,
            path,
            DumpNodeKind.Array,
            null,
            children,
            isTabular,
            tableColumns);
    }
}
