using VideoForensics.Client.Ui.Shared.Services.Review;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>The rules that turn the Event Filter selection into what each list shows.</summary>
public class ReviewEventFilters_Tests
{
    [Fact]
    public void ProviderTypes_EmptySelection_ReturnsNullMeaningEveryType()
    {
        Assert.Null(ReviewEventFilters.ProviderTypes([]));
    }

    [Fact]
    public void ProviderTypes_OnlySystemTypes_ReturnsEmptyMeaningNoProviderEvents()
    {
        Assert.Empty(ReviewEventFilters.ProviderTypes(["jamming", "signal_anomaly"])!);
    }

    [Fact]
    public void ProviderTypes_MixedSelection_ReturnsOnlyTheProviderTypes()
    {
        Assert.Equal(["motion"], ReviewEventFilters.ProviderTypes(["motion", "jamming"]));
    }

    [Theory]
    [InlineData(new string[0], "jamming", true)]
    [InlineData(new[] { "jamming" }, "jamming", true)]
    [InlineData(new[] { "motion" }, "jamming", false)]
    [InlineData(new[] { "jamming" }, "signal_anomaly", false)]
    public void Shows_Selection_ShowsAType(string[] selection, string type, bool expected)
    {
        Assert.Equal(expected, ReviewEventFilters.Shows(selection, type));
    }

    [Fact]
    public void SelectableEventTypes_ListsProviderTypesThenSystemTypes()
    {
        Assert.Equal(
            [.. ReviewEventFilters.KnownEventTypes, ReviewEventFilters.SignalAnomaly, ReviewEventFilters.Jamming],
            ReviewEventFilters.SelectableEventTypes);
    }

    [Fact]
    public void ProviderTypes_VehicleSelected_KeepsIt()
    {
        Assert.Equal(["vehicle"], ReviewEventFilters.ProviderTypes(["vehicle", "jamming"]));
    }

    [Fact]
    public void KnownEventTypes_IncludesVehicleAndAnimal()
    {
        Assert.Contains("vehicle", ReviewEventFilters.KnownEventTypes);
        Assert.Contains("animal", ReviewEventFilters.KnownEventTypes);
    }

    [Fact]
    public void KnownEventTypes_IncludesHumanNotPerson()
    {
        // Ring reports a person as the detection type "human"; no event carries the type "person".
        Assert.Contains("human", ReviewEventFilters.KnownEventTypes);
        Assert.DoesNotContain("person", ReviewEventFilters.KnownEventTypes);
    }
}
