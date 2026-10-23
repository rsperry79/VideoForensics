namespace VideoForensics.Ui.Shared.Tests;

using System.Text;
using System.Text.Json;
using Xunit;
using VideoForensics.Ui.Shared.Components.Dump;

public class DumpNodeBuilder_FromJson_Tests
{
    [Fact]
    public void FromJson_WithNull_ReturnsNullNode()
    {
        // Act
        var node = DumpNodeBuilder.FromJson("null");

        // Assert
        Assert.Equal(DumpNodeKind.Null, node.Kind);
        Assert.Equal("null", node.Value);
        Assert.Empty(node.Children);
        Assert.False(node.IsTabular);
    }

    [Fact]
    public void FromJson_WithString_ReturnsStringNode()
    {
        // Act
        var node = DumpNodeBuilder.FromJson("\"hello\"");

        // Assert
        Assert.Equal(DumpNodeKind.String, node.Kind);
        Assert.Equal("hello", node.Value);
        Assert.Empty(node.Children);
    }

    [Fact]
    public void FromJson_WithNumber_ReturnsNumberNode()
    {
        // Act
        var node = DumpNodeBuilder.FromJson("42");

        // Assert
        Assert.Equal(DumpNodeKind.Number, node.Kind);
        Assert.Equal("42", node.Value);
        Assert.Empty(node.Children);
    }

    [Fact]
    public void FromJson_WithBoolean_ReturnsBooleanNode()
    {
        // Act & Assert
        var trueNode = DumpNodeBuilder.FromJson("true");
        Assert.Equal(DumpNodeKind.Boolean, trueNode.Kind);
        Assert.Equal("true", trueNode.Value);

        var falseNode = DumpNodeBuilder.FromJson("false");
        Assert.Equal(DumpNodeKind.Boolean, falseNode.Kind);
        Assert.Equal("false", falseNode.Value);
    }

    [Fact]
    public void FromJson_WithEmptyObject_ReturnsObjectNode()
    {
        // Act
        var node = DumpNodeBuilder.FromJson("{}");

        // Assert
        Assert.Equal(DumpNodeKind.Object, node.Kind);
        Assert.Null(node.Value);
        Assert.Empty(node.Children);
    }

    [Fact]
    public void FromJson_WithObjectProperties_ReturnsObjectWithChildren()
    {
        // Act
        var node = DumpNodeBuilder.FromJson("{\"name\":\"test\",\"count\":5}");

        // Assert
        Assert.Equal(DumpNodeKind.Object, node.Kind);
        Assert.Null(node.Value);
        Assert.Equal(2, node.Children.Count);

        var nameChild = node.Children.First(c => c.Name == "name");
        Assert.Equal(DumpNodeKind.String, nameChild.Kind);
        Assert.Equal("test", nameChild.Value);
        Assert.Equal("$.name", nameChild.Path);

        var countChild = node.Children.First(c => c.Name == "count");
        Assert.Equal(DumpNodeKind.Number, countChild.Kind);
        Assert.Equal("5", countChild.Value);
        Assert.Equal("$.count", countChild.Path);
    }

    [Fact]
    public void FromJson_WithNestedObject_BuildsCorrectPath()
    {
        // Act
        var node = DumpNodeBuilder.FromJson("{\"device\":{\"id\":1,\"name\":\"cam1\"}}");

        // Assert
        Assert.Equal(DumpNodeKind.Object, node.Kind);
        var device = node.Children.First(c => c.Name == "device");
        Assert.Equal(DumpNodeKind.Object, device.Kind);
        Assert.Equal("$.device", device.Path);

        var id = device.Children.First(c => c.Name == "id");
        Assert.Equal("$.device.id", id.Path);
    }

    [Fact]
    public void FromJson_WithEmptyArray_ReturnsArrayNode()
    {
        // Act
        var node = DumpNodeBuilder.FromJson("[]");

        // Assert
        Assert.Equal(DumpNodeKind.Array, node.Kind);
        Assert.Null(node.Value);
        Assert.Empty(node.Children);
        Assert.False(node.IsTabular);
    }

    [Fact]
    public void FromJson_WithArrayOfScalars_ReturnsArrayWithIndexLabels()
    {
        // Act
        var node = DumpNodeBuilder.FromJson("[1,2,3]");

        // Assert
        Assert.Equal(DumpNodeKind.Array, node.Kind);
        Assert.Equal(3, node.Children.Count);

        for (int i = 0; i < 3; i++)
        {
            Assert.Equal($"[{i}]", node.Children[i].Name);
            Assert.Equal($"$[{i}]", node.Children[i].Path);
        }

        Assert.False(node.IsTabular);
    }

    [Fact]
    public void FromJson_WithArrayOfObjects_MarkAsTabular()
    {
        // Act
        var node = DumpNodeBuilder.FromJson("[{\"id\":1,\"name\":\"a\"},{\"id\":2,\"name\":\"b\"}]");

        // Assert
        Assert.Equal(DumpNodeKind.Array, node.Kind);
        Assert.True(node.IsTabular);
        Assert.Equal(2, node.TableColumns.Count);
        Assert.Contains("id", node.TableColumns);
        Assert.Contains("name", node.TableColumns);
    }

    [Fact]
    public void FromJson_WithArrayOfObjectsVariedKeys_UnionAllKeys()
    {
        // Act
        var node = DumpNodeBuilder.FromJson("[{\"id\":1,\"name\":\"a\"},{\"id\":2,\"type\":\"b\"}]");

        // Assert
        Assert.True(node.IsTabular);
        Assert.Equal(3, node.TableColumns.Count);
        // Order should be first-seen
        Assert.Equal("id", node.TableColumns[0]);
        Assert.Equal("name", node.TableColumns[1]);
        Assert.Equal("type", node.TableColumns[2]);
    }

    [Fact]
    public void FromJson_WithMixedArray_NotTabular()
    {
        // Act
        var node = DumpNodeBuilder.FromJson("[{\"id\":1},\"string\",42]");

        // Assert
        Assert.Equal(DumpNodeKind.Array, node.Kind);
        Assert.False(node.IsTabular);
    }

    [Fact]
    public void FromJson_WithStringEmbeddedJson_ExpandsWithJsonSuffix()
    {
        // Act
        var node = DumpNodeBuilder.FromJson("{\"metadata\":\"{\\\"key\\\":\\\"value\\\"}\"}");

        // Assert
        var metadata = node.Children.FirstOrDefault(c => c.Name.StartsWith("metadata"));
        Assert.NotNull(metadata);
        Assert.EndsWith(" (json)", metadata.Name);
        Assert.Equal(DumpNodeKind.Object, metadata.Kind);
        var key = metadata.Children.First(c => c.Name == "key");
        Assert.Equal("value", key.Value);
    }

    [Fact]
    public void FromJson_WithStringStartingWithBraceNotJson_StaysString()
    {
        // Act
        var node = DumpNodeBuilder.FromJson("{\"text\":\"{not json\"}");

        // Assert
        var text = node.Children.First(c => c.Name == "text");
        Assert.Equal(DumpNodeKind.String, text.Kind);
        Assert.Equal("{not json", text.Value);
        Assert.DoesNotContain(" (json)", text.Name);
    }

    [Fact]
    public void FromJson_InvalidJson_ThrowsArgumentException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => DumpNodeBuilder.FromJson("{invalid}"));
        Assert.Contains("JSON", ex.Message);
    }

    [Fact]
    public void FromJson_DepthGuard_StopsAt64Levels()
    {
        // Arrange: create deeply nested JSON (65 levels deep)
        var json = new StringBuilder("{");
        for (int i = 0; i < 64; i++)
        {
            json.Append($"\"a{i}\":{{");
        }
        json.Append("\"value\":1");
        for (int i = 0; i < 64; i++)
        {
            json.Append("}");
        }
        json.Append("}");

        // Act
        var node = DumpNodeBuilder.FromJson(json.ToString());

        // Assert - navigate down to depth 63
        var current = node;
        for (int i = 0; i < 63; i++)
        {
            Assert.NotEmpty(current.Children);
            current = current.Children[0];
        }

        // At depth 63, the child property should be the String "…" (depth guard at 64)
        Assert.NotEmpty(current.Children);
        var depthGuardedChild = current.Children.First();
        Assert.Equal(DumpNodeKind.String, depthGuardedChild.Kind);
        Assert.Equal("…", depthGuardedChild.Value);
    }
}

public class DumpNodeBuilder_FromObject_Tests
{
    [Fact]
    public void FromObject_WithNull_ReturnsNullNode()
    {
        // Act
        var node = DumpNodeBuilder.FromObject(null);

        // Assert
        Assert.Equal(DumpNodeKind.Null, node.Kind);
    }

    [Fact]
    public void FromObject_WithString_ReturnsStringNode()
    {
        // Act
        var node = DumpNodeBuilder.FromObject("hello");

        // Assert
        Assert.Equal(DumpNodeKind.String, node.Kind);
        Assert.Equal("hello", node.Value);
    }

    [Fact]
    public void FromObject_WithInt_ReturnsNumberNode()
    {
        // Act
        var node = DumpNodeBuilder.FromObject(42);

        // Assert
        Assert.Equal(DumpNodeKind.Number, node.Kind);
        Assert.Equal("42", node.Value);
    }

    [Fact]
    public void FromObject_WithBool_ReturnsBooleanNode()
    {
        // Act
        var nodeTrue = DumpNodeBuilder.FromObject(true);
        var nodeFalse = DumpNodeBuilder.FromObject(false);

        // Assert
        Assert.Equal(DumpNodeKind.Boolean, nodeTrue.Kind);
        Assert.Equal(DumpNodeKind.Boolean, nodeFalse.Kind);
    }

    [Fact]
    public void FromObject_WithPocoObject_ReturnsObjectNode()
    {
        // Arrange
        var obj = new TestPoco { Name = "test", Count = 5 };

        // Act
        var node = DumpNodeBuilder.FromObject(obj);

        // Assert
        Assert.Equal(DumpNodeKind.Object, node.Kind);
        Assert.Equal(2, node.Children.Count);
    }

    [Fact]
    public void FromObject_WithEnum_SerializesAsString()
    {
        // Arrange
        var obj = new TestPocoWithEnum { Status = TestStatus.Active };

        // Act
        var node = DumpNodeBuilder.FromObject(obj);

        // Assert
        var statusChild = node.Children.First(c => c.Name == "Status");
        Assert.Equal(DumpNodeKind.String, statusChild.Kind);
        Assert.Equal("Active", statusChild.Value);
    }

    [Fact]
    public void FromObject_WithNestedObject_ReturnsNestedStructure()
    {
        // Arrange
        var obj = new TestPocoWithNested
        {
            Id = 1,
            Device = new TestPoco { Name = "cam1", Count = 10 }
        };

        // Act
        var node = DumpNodeBuilder.FromObject(obj);

        // Assert
        Assert.Equal(DumpNodeKind.Object, node.Kind);
        var deviceChild = node.Children.First(c => c.Name == "Device");
        Assert.Equal(DumpNodeKind.Object, deviceChild.Kind);
    }

    [Fact]
    public void FromObject_WithNullProperty_CreatesNullChild()
    {
        // Arrange
        var obj = new TestPocoWithNested { Id = 1, Device = null };

        // Act
        var node = DumpNodeBuilder.FromObject(obj);

        // Assert
        var deviceChild = node.Children.First(c => c.Name == "Device");
        Assert.Equal(DumpNodeKind.Null, deviceChild.Kind);
    }

    private class TestPoco
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }
    }

    private enum TestStatus
    {
        Inactive = 0,
        Active = 1
    }

    private class TestPocoWithEnum
    {
        public TestStatus Status { get; set; }
    }

    private class TestPocoWithNested
    {
        public int Id { get; set; }
        public TestPoco? Device { get; set; }
    }
}

public class DumpNodeBuilder_Matches_Tests
{
    [Fact]
    public void Matches_WithSearchInNodeName_ReturnsTrue()
    {
        // Arrange
        var node = DumpNodeBuilder.FromJson("{\"device_id\":123}");
        var deviceNode = node.Children.First();

        // Act & Assert
        Assert.True(DumpNodeBuilder.Matches(deviceNode, "device"));
        Assert.True(DumpNodeBuilder.Matches(deviceNode, "DEVICE"));
        Assert.False(DumpNodeBuilder.Matches(deviceNode, "notfound"));
    }

    [Fact]
    public void Matches_WithSearchInScalarValue_ReturnsTrue()
    {
        // Arrange
        var node = DumpNodeBuilder.FromJson("{\"name\":\"my_camera\"}");
        var nameNode = node.Children.First();

        // Act & Assert
        Assert.True(DumpNodeBuilder.Matches(nameNode, "camera"));
        Assert.True(DumpNodeBuilder.Matches(nameNode, "CAMERA"));
        Assert.False(DumpNodeBuilder.Matches(nameNode, "device"));
    }

    [Fact]
    public void Matches_WithSearchInDescendant_ReturnsTrue()
    {
        // Arrange
        var node = DumpNodeBuilder.FromJson("{\"device\":{\"name\":\"camera1\"}}");

        // Act & Assert
        Assert.True(DumpNodeBuilder.Matches(node, "camera"));
        Assert.False(DumpNodeBuilder.Matches(node, "notfound"));
    }

    [Fact]
    public void Matches_CaseInsensitive()
    {
        // Arrange
        var node = DumpNodeBuilder.FromJson("{\"id\":1}");

        // Act & Assert
        Assert.True(DumpNodeBuilder.Matches(node, "ID"));
        Assert.True(DumpNodeBuilder.Matches(node, "Id"));
        Assert.True(DumpNodeBuilder.Matches(node, "id"));
    }
}
