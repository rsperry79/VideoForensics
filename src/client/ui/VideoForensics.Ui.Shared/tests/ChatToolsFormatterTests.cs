using Xunit;
using VideoForensics.Ui.Shared.Formatting;

namespace VideoForensics.Ui.Shared.Tests;

public class ChatToolsFormatterTests
{
    [Fact]
    public void FormatToolsInvoked_EmptyList_ReturnsEmptyString()
    {
        // Arrange
        var tools = new List<string>();

        // Act
        var result = ChatToolsFormatter.FormatToolsInvoked(tools);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void FormatToolsInvoked_SingleTool_ReturnsFormattedString()
    {
        // Arrange
        var tools = new List<string> { "JammingDetection" };

        // Act
        var result = ChatToolsFormatter.FormatToolsInvoked(tools);

        // Assert
        Assert.Equal("Checked: Jamming Detection", result);
    }

    [Fact]
    public void FormatToolsInvoked_MultipleCamelCaseTools_ReturnsFormattedList()
    {
        // Arrange
        var tools = new List<string> { "JammingDetection", "TimelineQuery", "DeviceStatus" };

        // Act
        var result = ChatToolsFormatter.FormatToolsInvoked(tools);

        // Assert
        Assert.Equal("Checked: Jamming Detection, Timeline Query, Device Status", result);
    }

    [Fact]
    public void FormatToolsInvoked_ToolsWithUnderscores_ReplacesUnderscoresWithSpaces()
    {
        // Arrange
        var tools = new List<string> { "Signal_Analysis", "Event_Query" };

        // Act
        var result = ChatToolsFormatter.FormatToolsInvoked(tools);

        // Assert
        Assert.Equal("Checked: Signal Analysis, Event Query", result);
    }

    [Fact]
    public void FormatToolsInvoked_NullList_ReturnsEmptyString()
    {
        // Act
        var result = ChatToolsFormatter.FormatToolsInvoked(null!);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void FormatToolsInvoked_MixedCamelCaseAndUnderscore_FormatsCorrectly()
    {
        // Arrange
        var tools = new List<string> { "JammingDetection", "Signal_Analysis", "TimelineQuery" };

        // Act
        var result = ChatToolsFormatter.FormatToolsInvoked(tools);

        // Assert
        Assert.Equal("Checked: Jamming Detection, Signal Analysis, Timeline Query", result);
    }

    [Fact]
    public void FormatToolsInvoked_AllCapsToolNames_HandledCorrectly()
    {
        // Arrange
        var tools = new List<string> { "API", "HTTP" };

        // Act
        var result = ChatToolsFormatter.FormatToolsInvoked(tools);

        // Assert
        // All-caps names won't split on the regex since there's no lowercase before uppercase
        Assert.Equal("Checked: API, HTTP", result);
    }
}
