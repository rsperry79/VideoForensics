namespace VideoForensics.Ui.Shared.Tests;

using Xunit;
using VideoForensics.Api.Contracts;
using VideoForensics.Ui.Shared.Services.SelfTest;

public class SelfTestInspectorMapper_ToInspector_Tests
{
    [Fact]
    public void ToInspector_SuccessfulCall_SetsTitleToDisplayNameAndOK()
    {
        // Arrange
        var call = new SelfTestCallDto(
            Endpoint: "list_devices",
            DisplayName: "List Devices",
            SessionMethod: "oauth2",
            Destructive: false,
            Physical: false,
            Target: "location-123",
            StartedAtUtc: DateTime.UtcNow,
            DurationMs: 150,
            Success: true,
            Error: null,
            RestoreAttempted: false,
            RestoreSuccess: null,
            RestoreError: null,
            RestoreSkippedReason: null,
            SchemaIssues: new List<string>(),
            HttpCalls: new List<SelfTestHttpCallDto>()
        );

        // Act
        var model = SelfTestInspectorMapper.ToInspector(call);

        // Assert
        Assert.Equal("List Devices · OK", model.Title);
    }

    [Fact]
    public void ToInspector_FailedCall_SetsTitleToDisplayNameAndFAILED()
    {
        // Arrange
        var call = new SelfTestCallDto(
            Endpoint: "list_devices",
            DisplayName: "List Devices",
            SessionMethod: "oauth2",
            Destructive: false,
            Physical: false,
            Target: null,
            StartedAtUtc: DateTime.UtcNow,
            DurationMs: 5000,
            Success: false,
            Error: "Connection timeout",
            RestoreAttempted: false,
            RestoreSuccess: null,
            RestoreError: null,
            RestoreSkippedReason: null,
            SchemaIssues: new List<string>(),
            HttpCalls: new List<SelfTestHttpCallDto>()
        );

        // Act
        var model = SelfTestInspectorMapper.ToInspector(call);

        // Assert
        Assert.Equal("List Devices · FAILED", model.Title);
    }

    [Fact]
    public void ToInspector_IncludesCallSummaryInFields()
    {
        // Arrange
        var call = new SelfTestCallDto(
            Endpoint: "get_devices",
            DisplayName: "Get Devices",
            SessionMethod: "api_key",
            Destructive: true,
            Physical: true,
            Target: "device-456",
            StartedAtUtc: DateTime.UtcNow,
            DurationMs: 250,
            Success: true,
            Error: null,
            RestoreAttempted: true,
            RestoreSuccess: true,
            RestoreError: null,
            RestoreSkippedReason: null,
            SchemaIssues: new List<string> { "Missing field: device_state" },
            HttpCalls: new List<SelfTestHttpCallDto>()
        );

        // Act
        var model = SelfTestInspectorMapper.ToInspector(call);

        // Assert
        Assert.NotNull(model.Fields);
        var fieldsObj = (dynamic)model.Fields;
        Assert.Equal("get_devices", fieldsObj.Endpoint);
        Assert.Equal("device-456", fieldsObj.Target);
        Assert.Equal(250, fieldsObj.DurationMs);
        Assert.True(fieldsObj.Success);
        Assert.Equal("api_key", fieldsObj.SessionMethod);
        Assert.True(fieldsObj.Destructive);
        Assert.True(fieldsObj.Physical);
        Assert.Equal(1, fieldsObj.SchemaIssuesCount);
        Assert.Equal(0, fieldsObj.HttpCallsCount);
    }

    [Fact]
    public void ToInspector_IncludesRestoreInfoInFields()
    {
        // Arrange
        var call = new SelfTestCallDto(
            Endpoint: "arm_system",
            DisplayName: "Arm System",
            SessionMethod: "oauth2",
            Destructive: true,
            Physical: false,
            Target: "system-789",
            StartedAtUtc: DateTime.UtcNow,
            DurationMs: 500,
            Success: true,
            Error: null,
            RestoreAttempted: true,
            RestoreSuccess: false,
            RestoreError: "Failed to restore",
            RestoreSkippedReason: null,
            SchemaIssues: new List<string>(),
            HttpCalls: new List<SelfTestHttpCallDto>()
        );

        // Act
        var model = SelfTestInspectorMapper.ToInspector(call);

        // Assert
        Assert.NotNull(model.Fields);
        var fieldsObj = (dynamic)model.Fields;
        var restoreInfo = fieldsObj.RestoreInfo;
        Assert.True(restoreInfo.RestoreAttempted);
        Assert.False(restoreInfo.RestoreSuccess);
        Assert.Equal("Failed to restore", restoreInfo.RestoreError);
        Assert.Null(restoreInfo.RestoreSkippedReason);
    }

    [Fact]
    public void ToInspector_WithTestPhaseHttpCall_SetsRawJsonToFirstTestPhaseBody()
    {
        // Arrange
        var httpCalls = new List<SelfTestHttpCallDto>
        {
            new("POST", "https://api.example.com/test", 200, "test", DateTime.UtcNow, 1000, "{\"result\": \"ok\"}", false),
            new("POST", "https://api.example.com/restore", 200, "restore", DateTime.UtcNow.AddSeconds(1), 100, "{}", false)
        };

        var call = new SelfTestCallDto(
            Endpoint: "endpoint1",
            DisplayName: "Endpoint 1",
            SessionMethod: "oauth2",
            Destructive: false,
            Physical: false,
            Target: null,
            StartedAtUtc: DateTime.UtcNow,
            DurationMs: 200,
            Success: true,
            Error: null,
            RestoreAttempted: false,
            RestoreSuccess: null,
            RestoreError: null,
            RestoreSkippedReason: null,
            SchemaIssues: new List<string>(),
            HttpCalls: httpCalls
        );

        // Act
        var model = SelfTestInspectorMapper.ToInspector(call);

        // Assert
        Assert.NotNull(model.RawJson);
        Assert.Equal("{\"result\": \"ok\"}", model.RawJson);
    }

    [Fact]
    public void ToInspector_WithoutTestPhaseHttpCall_RawJsonIsNull()
    {
        // Arrange
        var httpCalls = new List<SelfTestHttpCallDto>
        {
            new("POST", "https://api.example.com/restore", 200, "restore", DateTime.UtcNow, 100, "{}", false)
        };

        var call = new SelfTestCallDto(
            Endpoint: "endpoint1",
            DisplayName: "Endpoint 1",
            SessionMethod: "oauth2",
            Destructive: false,
            Physical: false,
            Target: null,
            StartedAtUtc: DateTime.UtcNow,
            DurationMs: 200,
            Success: false,
            Error: null,
            RestoreAttempted: false,
            RestoreSuccess: null,
            RestoreError: null,
            RestoreSkippedReason: null,
            SchemaIssues: new List<string>(),
            HttpCalls: httpCalls
        );

        // Act
        var model = SelfTestInspectorMapper.ToInspector(call);

        // Assert
        Assert.Null(model.RawJson);
    }

    [Fact]
    public void ToInspector_WithHttpCalls_ProvenanceIncludesAllCalls()
    {
        // Arrange
        var httpCalls = new List<SelfTestHttpCallDto>
        {
            new("GET", "https://api.example.com/devices", 200, "test", DateTime.UtcNow, 5000, "{}", false),
            new("POST", "https://api.example.com/devices/restore", 200, "restore", DateTime.UtcNow.AddSeconds(1), 500, "{}", true)
        };

        var call = new SelfTestCallDto(
            Endpoint: "endpoint1",
            DisplayName: "Endpoint 1",
            SessionMethod: "oauth2",
            Destructive: false,
            Physical: false,
            Target: null,
            StartedAtUtc: DateTime.UtcNow,
            DurationMs: 200,
            Success: true,
            Error: null,
            RestoreAttempted: false,
            RestoreSuccess: null,
            RestoreError: null,
            RestoreSkippedReason: null,
            SchemaIssues: new List<string>(),
            HttpCalls: httpCalls
        );

        // Act
        var model = SelfTestInspectorMapper.ToInspector(call);

        // Assert
        Assert.NotNull(model.Provenance);
        Assert.Equal(2, model.Provenance.Count);
        Assert.Equal("test GET 200", model.Provenance[0].Key);
        Assert.Equal("https://api.example.com/devices", model.Provenance[0].Value);
        Assert.Equal("restore POST 200", model.Provenance[1].Key);
        Assert.Contains("truncated", model.Provenance[1].Value);
        Assert.Contains("500 bytes", model.Provenance[1].Value);
    }

    [Fact]
    public void ToInspector_WithTruncatedBody_ProviderShowsTruncationNote()
    {
        // Arrange
        var httpCalls = new List<SelfTestHttpCallDto>
        {
            new("POST", "https://api.example.com/test", 200, "test", DateTime.UtcNow, 262000, "{\"truncated\": true}", true)
        };

        var call = new SelfTestCallDto(
            Endpoint: "endpoint1",
            DisplayName: "Endpoint 1",
            SessionMethod: "oauth2",
            Destructive: false,
            Physical: false,
            Target: null,
            StartedAtUtc: DateTime.UtcNow,
            DurationMs: 200,
            Success: true,
            Error: null,
            RestoreAttempted: false,
            RestoreSuccess: null,
            RestoreError: null,
            RestoreSkippedReason: null,
            SchemaIssues: new List<string>(),
            HttpCalls: httpCalls
        );

        // Act
        var model = SelfTestInspectorMapper.ToInspector(call);

        // Assert
        Assert.NotNull(model.Provenance);
        Assert.Single(model.Provenance);
        var provValue = model.Provenance[0].Value;
        Assert.Contains("truncated", provValue);
        Assert.Contains("262000 bytes", provValue);
    }

    [Fact]
    public void ToInspector_EmptyHttpCalls_ProvenanceIsEmpty()
    {
        // Arrange
        var call = new SelfTestCallDto(
            Endpoint: "endpoint1",
            DisplayName: "Endpoint 1",
            SessionMethod: "oauth2",
            Destructive: false,
            Physical: false,
            Target: null,
            StartedAtUtc: DateTime.UtcNow,
            DurationMs: 200,
            Success: true,
            Error: null,
            RestoreAttempted: false,
            RestoreSuccess: null,
            RestoreError: null,
            RestoreSkippedReason: null,
            SchemaIssues: new List<string>(),
            HttpCalls: new List<SelfTestHttpCallDto>()
        );

        // Act
        var model = SelfTestInspectorMapper.ToInspector(call);

        // Assert
        Assert.NotNull(model.Provenance);
        Assert.Empty(model.Provenance);
    }
}

public class SelfTestInspectorMapper_IsValidJson_Tests
{
    [Fact]
    public void IsValidJson_WithValidJsonObject_ReturnsTrue()
    {
        var result = SelfTestInspectorMapper.IsValidJson("{\"key\": \"value\"}");
        Assert.True(result);
    }

    [Fact]
    public void IsValidJson_WithValidJsonArray_ReturnsTrue()
    {
        var result = SelfTestInspectorMapper.IsValidJson("[1, 2, 3]");
        Assert.True(result);
    }

    [Fact]
    public void IsValidJson_WithValidJsonNumber_ReturnsTrue()
    {
        var result = SelfTestInspectorMapper.IsValidJson("42");
        Assert.True(result);
    }

    [Fact]
    public void IsValidJson_WithInvalidJson_ReturnsFalse()
    {
        var result = SelfTestInspectorMapper.IsValidJson("{invalid}");
        Assert.False(result);
    }

    [Fact]
    public void IsValidJson_WithPlainText_ReturnsFalse()
    {
        var result = SelfTestInspectorMapper.IsValidJson("This is plain text");
        Assert.False(result);
    }

    [Fact]
    public void IsValidJson_WithNull_ReturnsFalse()
    {
        var result = SelfTestInspectorMapper.IsValidJson(null);
        Assert.False(result);
    }

    [Fact]
    public void IsValidJson_WithEmptyString_ReturnsFalse()
    {
        var result = SelfTestInspectorMapper.IsValidJson("");
        Assert.False(result);
    }

    [Fact]
    public void IsValidJson_WithWhitespaceOnly_ReturnsFalse()
    {
        var result = SelfTestInspectorMapper.IsValidJson("   ");
        Assert.False(result);
    }
}
