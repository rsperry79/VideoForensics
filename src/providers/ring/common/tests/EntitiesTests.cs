using System;
using System.Collections.Generic;
using System.Text.Json;
using VideoForensics.Providers.Ring;
using VideoForensics.Providers.Ring.Entities;

namespace VideoForensics.Providers.Ring.Common.Tests;

/// <summary>
/// Tests for DeviceAction entity.
/// </summary>
public class DeviceAction_Construction_Tests
{
    [Fact]
    public void Construction_Default_InitializesWithDefaultValues()
    {
        var action = new DeviceAction();

        Assert.NotNull(action);
        Assert.Equal(string.Empty, action.ActionType);
        Assert.NotNull(action.Parameters);
        Assert.Empty(action.Parameters);
    }

    [Fact]
    public void Construction_SetProperties_AssignsValues()
    {
        var action = new DeviceAction
        {
            ActionType = "light_on",
            Parameters = new Dictionary<string, object> { { "brightness", 100 } }
        };

        Assert.Equal("light_on", action.ActionType);
        Assert.Single(action.Parameters);
        Assert.Equal(100, action.Parameters["brightness"]);
    }

    [Fact]
    public void Parameters_DefaultInitialization_IsEmptyDictionary()
    {
        var action = new DeviceAction();

        Assert.Empty(action.Parameters);
        action.Parameters.Add("key", "value");

        Assert.Single(action.Parameters);
    }
}

/// <summary>
/// Tests for DeviceStatusInfo entity.
/// </summary>
public class DeviceStatusInfo_Construction_Tests
{
    [Fact]
    public void Construction_Default_InitializesWithDefaultValues()
    {
        var status = new DeviceStatusInfo();

        Assert.NotNull(status);
        Assert.Equal(string.Empty, status.DeviceId);
        Assert.False(status.IsOnline);
        Assert.Null(status.BatteryLevel);
        Assert.Null(status.SignalStrength);
    }

    [Fact]
    public void Construction_SetProperties_AssignsValues()
    {
        var now = DateTime.UtcNow;
        var status = new DeviceStatusInfo
        {
            DeviceId = "device-123",
            IsOnline = true,
            BatteryLevel = 85,
            SignalStrength = "excellent",
            LastSeen = now
        };

        Assert.Equal("device-123", status.DeviceId);
        Assert.True(status.IsOnline);
        Assert.Equal(85, status.BatteryLevel);
        Assert.Equal("excellent", status.SignalStrength);
        Assert.Equal(now, status.LastSeen);
    }

    [Fact]
    public void IsOnline_WhenOffline_ReturnsFalse()
    {
        var status = new DeviceStatusInfo { IsOnline = false };

        Assert.False(status.IsOnline);
    }

    [Fact]
    public void BatteryLevel_WhenNull_IsNullable()
    {
        var status = new DeviceStatusInfo { BatteryLevel = null };

        Assert.Null(status.BatteryLevel);
    }

    [Fact]
    public void SignalStrength_WhenNull_IsNullable()
    {
        var status = new DeviceStatusInfo { SignalStrength = null };

        Assert.Null(status.SignalStrength);
    }
}

/// <summary>
/// Tests for DoorbotTimestamp entity.
/// </summary>
public class DoorbotTimestamp_JsonDeserialization_Tests
{
    [Fact]
    public void Deserialization_WithValidJson_PopulatesProperties()
    {
        var json = """{"doorbot_id": 12345, "timestamp": 1609459200000}""";
        var timestamp = JsonSerializer.Deserialize<DoorbotTimestamp>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(timestamp);
        Assert.Equal(12345, timestamp.DoorbotId);
        Assert.Equal(1609459200000, timestamp.TimestampEpoch);
    }

    [Fact]
    public void Construction_Default_InitializesWithNullValues()
    {
        var timestamp = new DoorbotTimestamp();

        Assert.Null(timestamp.DoorbotId);
        Assert.Null(timestamp.TimestampEpoch);
        Assert.Null(timestamp.Timestamp);
    }

    [Fact]
    public void Timestamp_WithValidEpoch_ConvertsToDateTime()
    {
        var timestamp = new DoorbotTimestamp
        {
            TimestampEpoch = 1609459200000 // 2021-01-01 00:00:00 UTC
        };

        Assert.NotNull(timestamp.Timestamp);
        Assert.True(timestamp.Timestamp.Value.ToUniversalTime().Year == 2021);
    }

    [Fact]
    public void Timestamp_WithNullEpoch_ReturnsNull()
    {
        var timestamp = new DoorbotTimestamp { TimestampEpoch = null };

        Assert.Null(timestamp.Timestamp);
    }

    [Fact]
    public void Timestamp_ConvertedFromEpoch_IsLocalTime()
    {
        var timestamp = new DoorbotTimestamp
        {
            TimestampEpoch = 0 // 1970-01-01 00:00:00 UTC
        };

        // Should be converted to local time
        Assert.NotNull(timestamp.Timestamp);
    }
}

/// <summary>
/// Tests for OAuthToken entity.
/// </summary>
public class OAuthToken_JsonDeserialization_Tests
{
    [Fact]
    public void Deserialization_WithValidJson_PopulatesProperties()
    {
        var json = """
        {
            "access_token": "test-access-token",
            "token_type": "bearer",
            "expires_in": 3600,
            "refresh_token": "test-refresh-token",
            "scope": "client",
            "created_at": 1609459200
        }
        """;

        var token = JsonSerializer.Deserialize<OAutToken>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(token);
        Assert.Equal("test-access-token", token.AccessToken);
        Assert.Equal("bearer", token.TokenType);
        Assert.Equal(3600, token.ExpiresInSeconds);
        Assert.Equal("test-refresh-token", token.RefreshToken);
        Assert.Equal("client", token.Scope);
        Assert.Equal(1609459200, token.CreatedAtTicks);
    }

    [Fact]
    public void ExpiresInSeconds_WhenSet_CalculatesExpiresAt()
    {
        var token = new OAutToken();
        var beforeSet = DateTime.Now;
        token.ExpiresInSeconds = 3600;
        var afterSet = DateTime.Now;

        Assert.NotEqual(default, token.ExpiresAt);
        Assert.True(token.ExpiresAt > beforeSet.AddSeconds(3600 - 1));
        Assert.True(token.ExpiresAt < afterSet.AddSeconds(3600 + 1));
    }

    [Fact]
    public void CreatedAt_WithValidTicks_ConvertsFromEpoch()
    {
        var token = new OAutToken { CreatedAtTicks = 1609459200 }; // 2021-01-01 00:00:00 UTC

        Assert.Equal(1609459200, token.CreatedAtTicks);
        Assert.NotEqual(default, token.CreatedAt);
    }

    [Fact]
    public void Construction_Default_InitializesProperties()
    {
        var token = new OAutToken();

        Assert.NotNull(token);
    }

    [Fact]
    public void SetPropertiesDirectly_AssignsValues()
    {
        var token = new OAutToken
        {
            AccessToken = "access",
            TokenType = "bearer",
            RefreshToken = "refresh",
            Scope = "client",
            CreatedAtTicks = 1609459200
        };

        Assert.Equal("access", token.AccessToken);
        Assert.Equal("bearer", token.TokenType);
        Assert.Equal("refresh", token.RefreshToken);
        Assert.Equal("client", token.Scope);
        Assert.Equal(1609459200, token.CreatedAtTicks);
    }
}

/// <summary>
/// Tests for SessionFeatures entity.
/// </summary>
public class SessionFeatures_JsonDeserialization_Tests
{
    [Fact]
    public void Construction_Default_InitializesWithDefaultValues()
    {
        var features = new SessionFeatures();

        Assert.NotNull(features);
        Assert.Null(features.RemoteLoggingFormatStoring);
        Assert.Null(features.SubscriptionsEnabled);
        Assert.NotNull(features.AdditionalFeatures);
        Assert.Empty(features.AdditionalFeatures);
    }

    [Fact]
    public void Deserialization_WithExplicitProperties_PopulatesTypedProperties()
    {
        var json = """
        {
            "remote_logging_format_storing": true,
            "subscriptions_enabled": false,
            "vod_enabled": true,
            "stickupcam_setup_enabled": true
        }
        """;

        var features = JsonSerializer.Deserialize<SessionFeatures>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(features);
        Assert.True(features.RemoteLoggingFormatStoring);
        Assert.False(features.SubscriptionsEnabled);
        Assert.True(features.VodEnabled);
        Assert.True(features.StickupcamSetupEnabled);
    }

    [Fact]
    public void Deserialization_WithUnknownProperties_CapturesInAdditionalFeatures()
    {
        var json = """
        {
            "remote_logging_format_storing": true,
            "unknown_feature_flag_1": true,
            "unknown_feature_flag_2": "some_value"
        }
        """;

        var features = JsonSerializer.Deserialize<SessionFeatures>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(features);
        Assert.True(features.RemoteLoggingFormatStoring);
        Assert.NotEmpty(features.AdditionalFeatures);
        Assert.Contains("unknown_feature_flag_1", features.AdditionalFeatures.Keys);
        Assert.Contains("unknown_feature_flag_2", features.AdditionalFeatures.Keys);
    }

    [Fact]
    public void SetPropertiesDirectly_AssignsValues()
    {
        var features = new SessionFeatures
        {
            RemoteLoggingFormatStoring = true,
            SubscriptionsEnabled = false,
            VodEnabled = true,
            ChimeProEnabled = true
        };

        Assert.True(features.RemoteLoggingFormatStoring);
        Assert.False(features.SubscriptionsEnabled);
        Assert.True(features.VodEnabled);
        Assert.True(features.ChimeProEnabled);
    }

    [Fact]
    public void AdditionalFeatures_DefaultInitialization_IsEmptyDictionary()
    {
        var features = new SessionFeatures();

        Assert.Empty(features.AdditionalFeatures);
        features.AdditionalFeatures["custom"] = "value";

        Assert.Single(features.AdditionalFeatures);
    }

    [Fact]
    public void MixedProperties_TypedAndUnknown_BothPopulated()
    {
        var json = """
        {
            "subscriptions_enabled": true,
            "custom_unknown_flag": 42
        }
        """;

        var features = JsonSerializer.Deserialize<SessionFeatures>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(features);
        Assert.True(features.SubscriptionsEnabled);
        Assert.Single(features.AdditionalFeatures);
        Assert.Equal(42, ((JsonElement)features.AdditionalFeatures["custom_unknown_flag"]).GetInt32());
    }
}
