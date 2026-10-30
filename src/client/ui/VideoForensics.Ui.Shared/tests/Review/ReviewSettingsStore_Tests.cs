using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using VideoForensics.Client.Ui.Shared.Services.Review;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>The localStorage copy of the shared review settings that Media and the Review pages both read and write.</summary>
public class ReviewSettingsStore_Tests : BunitContext
{
    private ReviewSettingsStore CreateStore()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        return new ReviewSettingsStore(Services.GetRequiredService<IJSRuntime>());
    }

    [Fact]
    public async Task ReviewSettingsStore_Load_ReadsTheSavedQueryAndEvent()
    {
        // Arrange
        ReviewSettingsStore store = CreateStore();
        var eventId = Guid.NewGuid();
        JSInterop.Setup<string?>("localStorage.getItem", ReviewSettingsStore.StorageKey)
            .SetResult($"{{\"query\":\"tab=Events\",\"eventId\":\"{eventId:D}\"}}");

        // Act
        SavedReviewSettings? saved = await store.LoadAsync();

        // Assert
        Assert.NotNull(saved);
        Assert.Equal("tab=Events", saved.Query);
        Assert.Equal(eventId, saved.EventId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    public async Task ReviewSettingsStore_Load_NothingUsableSaved_ReturnsNull(string? stored)
    {
        // Arrange
        ReviewSettingsStore store = CreateStore();
        JSInterop.Setup<string?>("localStorage.getItem", ReviewSettingsStore.StorageKey).SetResult(stored);

        // Act
        SavedReviewSettings? saved = await store.LoadAsync();

        // Assert
        Assert.Null(saved);
    }

    [Fact]
    public async Task ReviewSettingsStore_Save_WritesJsonUnderTheSharedKey()
    {
        // Arrange
        ReviewSettingsStore store = CreateStore();

        // Act
        await store.SaveAsync(new SavedReviewSettings("tab=Detail", null));

        // Assert
        Assert.Contains(JSInterop.Invocations, i =>
            i.Identifier == "localStorage.setItem"
            && i.Arguments[0]!.ToString() == ReviewSettingsStore.StorageKey
            && i.Arguments[1]!.ToString()!.Contains("tab=Detail"));
    }

    [Fact]
    public async Task ReviewSettingsStore_Update_ChangesTheSavedValueAndKeepsTheRest()
    {
        // Arrange
        ReviewSettingsStore store = CreateStore();
        var eventId = Guid.NewGuid();
        JSInterop.Setup<string?>("localStorage.getItem", ReviewSettingsStore.StorageKey)
            .SetResult($"{{\"query\":\"tab=Events\",\"eventId\":\"{eventId:D}\"}}");

        // Act
        await store.UpdateAsync(current => current with { Query = "tab=Detail" });

        // Assert - the query changed, the selected event stayed
        Assert.Contains(JSInterop.Invocations, i =>
            i.Identifier == "localStorage.setItem"
            && i.Arguments[1]!.ToString()!.Contains("tab=Detail")
            && i.Arguments[1]!.ToString()!.Contains(eventId.ToString("D")));
    }
}
