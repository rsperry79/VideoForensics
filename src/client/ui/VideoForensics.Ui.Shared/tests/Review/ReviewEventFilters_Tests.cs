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
}
