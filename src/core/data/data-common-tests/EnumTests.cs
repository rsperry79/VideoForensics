using VideoForensics.Data.Common.Entities;
using Xunit;

namespace VideoForensics.Data.Common.Tests;

public class EnumTests
{
    [Fact]
    public void ActorType_HasHumanMember_Defined()
    {
        // Act
        var isDefined = Enum.IsDefined(typeof(ActorType), ActorType.Human);

        // Assert
        Assert.True(isDefined);
    }

    [Fact]
    public void ActorType_HasSystemMember_Defined()
    {
        // Act
        var isDefined = Enum.IsDefined(typeof(ActorType), ActorType.System);

        // Assert
        Assert.True(isDefined);
    }

    [Fact]
    public void ActorType_HasMcpToolMember_Defined()
    {
        // Act
        var isDefined = Enum.IsDefined(typeof(ActorType), ActorType.McpTool);

        // Assert
        Assert.True(isDefined);
    }

    [Fact]
    public void ActorType_AllMembers_AreDistinct()
    {
        // Arrange
        var values = Enum.GetValues(typeof(ActorType)).Cast<ActorType>().ToList();

        // Assert
        Assert.Equal(3, values.Count);
        Assert.Contains(ActorType.Human, values);
        Assert.Contains(ActorType.System, values);
        Assert.Contains(ActorType.McpTool, values);
    }

    [Fact]
    public void DiscrepancyType_HasMissingFromProviderMember_Defined()
    {
        // Act
        var isDefined = Enum.IsDefined(typeof(DiscrepancyType), DiscrepancyType.MissingFromProvider);

        // Assert
        Assert.True(isDefined);
    }

    [Fact]
    public void DiscrepancyType_HasMetadataChangedMember_Defined()
    {
        // Act
        var isDefined = Enum.IsDefined(typeof(DiscrepancyType), DiscrepancyType.MetadataChanged);

        // Assert
        Assert.True(isDefined);
    }

    [Fact]
    public void DiscrepancyType_HasNewEventFoundOnProviderMember_Defined()
    {
        // Act
        var isDefined = Enum.IsDefined(typeof(DiscrepancyType), DiscrepancyType.NewEventFoundOnProvider);

        // Assert
        Assert.True(isDefined);
    }

    [Fact]
    public void DiscrepancyType_AllMembers_AreDistinct()
    {
        // Arrange
        var values = Enum.GetValues(typeof(DiscrepancyType)).Cast<DiscrepancyType>().ToList();

        // Assert
        Assert.Equal(3, values.Count);
        Assert.Contains(DiscrepancyType.MissingFromProvider, values);
        Assert.Contains(DiscrepancyType.MetadataChanged, values);
        Assert.Contains(DiscrepancyType.NewEventFoundOnProvider, values);
    }

    [Fact]
    public void DeviceConfigSource_HasFetchedMember_Defined()
    {
        // Act
        var isDefined = Enum.IsDefined(typeof(DeviceConfigSource), DeviceConfigSource.Fetched);

        // Assert
        Assert.True(isDefined);
    }

    [Fact]
    public void DeviceConfigSource_HasAppliedMember_Defined()
    {
        // Act
        var isDefined = Enum.IsDefined(typeof(DeviceConfigSource), DeviceConfigSource.Applied);

        // Assert
        Assert.True(isDefined);
    }

    [Fact]
    public void DeviceConfigSource_AllMembers_AreDistinct()
    {
        // Arrange
        var values = Enum.GetValues(typeof(DeviceConfigSource)).Cast<DeviceConfigSource>().ToList();

        // Assert
        Assert.Equal(2, values.Count);
        Assert.Contains(DeviceConfigSource.Fetched, values);
        Assert.Contains(DeviceConfigSource.Applied, values);
    }

    [Fact]
    public void ActorType_CanConvertToInt_AndBack()
    {
        // Act
        var humanInt = (int)ActorType.Human;
        var systemInt = (int)ActorType.System;
        var toolInt = (int)ActorType.McpTool;

        // Assert
        Assert.Equal(ActorType.Human, (ActorType)humanInt);
        Assert.Equal(ActorType.System, (ActorType)systemInt);
        Assert.Equal(ActorType.McpTool, (ActorType)toolInt);
    }

    [Fact]
    public void DiscrepancyType_CanConvertToInt_AndBack()
    {
        // Act
        var missingInt = (int)DiscrepancyType.MissingFromProvider;
        var changedInt = (int)DiscrepancyType.MetadataChanged;
        var newInt = (int)DiscrepancyType.NewEventFoundOnProvider;

        // Assert
        Assert.Equal(DiscrepancyType.MissingFromProvider, (DiscrepancyType)missingInt);
        Assert.Equal(DiscrepancyType.MetadataChanged, (DiscrepancyType)changedInt);
        Assert.Equal(DiscrepancyType.NewEventFoundOnProvider, (DiscrepancyType)newInt);
    }

    [Fact]
    public void DeviceConfigSource_CanConvertToInt_AndBack()
    {
        // Act
        var fetchedInt = (int)DeviceConfigSource.Fetched;
        var appliedInt = (int)DeviceConfigSource.Applied;

        // Assert
        Assert.Equal(DeviceConfigSource.Fetched, (DeviceConfigSource)fetchedInt);
        Assert.Equal(DeviceConfigSource.Applied, (DeviceConfigSource)appliedInt);
    }
}
