using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

using VideoForensics.Client.Ui.Shared.Components.Notes;

using Xunit;

using NoteEntryCheck = VideoForensics.Client.Ui.Shared.Components.Notes.NotesPanel.NoteEntryCheck;

namespace VideoForensics.Client.Ui.Shared.Tests.Components.Notes;

/// <summary>
/// The generic notes panel, with plain delegates and no repository: notes are listed with author and body, the add form needs
/// CanAdd and an open list, the TypeScript entry check enables the add button, the counter shows the characters left, and a
/// save passes the trimmed body to the add delegate.
/// </summary>
public class NotesPanel_Tests : BunitContext
{
    private static readonly DateTime FixedUtc = new(2026, 11, 1, 9, 30, 0, DateTimeKind.Utc);

    public NotesPanel_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddLocalization();
        _ = Services.AddLogging();
        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();

        // The TypeScript check accepts a body that is not blank and not over 4000 characters after trimming.
        _ = JSInterop.Setup<NoteEntryCheck>("vfCaseNoteCheck", inv => inv.Arguments.Count > 0 && inv.Arguments[0] is string text && text.Trim().Length > 0)
            .SetResult(new NoteEntryCheck(true, 5, null));
        _ = JSInterop.Setup<NoteEntryCheck>("vfCaseNoteCheck", inv => inv.Arguments.Count > 0 && inv.Arguments[0] is string text && text.Trim().Length == 0)
            .SetResult(new NoteEntryCheck(false, 0, "empty"));
    }

    private IRenderedComponent<NotesPanel> RenderPanel(
        IReadOnlyList<NoteItem> notes,
        bool canAdd = true,
        bool isOpen = true,
        Func<string, CancellationToken, Task>? add = null)
    {
        return Render<NotesPanel>(p => p
            .Add(c => c.LoadNotes, _ => Task.FromResult(notes))
            .Add(c => c.AddNote, add ?? ((_, _) => Task.CompletedTask))
            .Add(c => c.CanAdd, canAdd)
            .Add(c => c.IsOpen, isOpen));
    }

    [Fact]
    public void NotesPanel_WithNotes_ShowsAuthorAndBody()
    {
        // Arrange
        var notes = new[] { new NoteItem(Guid.NewGuid(), "op-alpha", FixedUtc, "first line\nsecond line") };

        // Act
        IRenderedComponent<NotesPanel> cut = RenderPanel(notes);

        // Assert
        cut.WaitForAssertion(() =>
        {
            AngleSharp.Dom.IElement note = cut.Find("[data-testid='notes-item']");
            Assert.Contains("op-alpha", note.TextContent);
            Assert.Contains("first line", note.TextContent);
            Assert.Contains("second line", note.TextContent);
        });
    }

    [Fact]
    public void NotesPanel_CanAddFalse_HidesAddForm()
    {
        // Arrange and Act
        IRenderedComponent<NotesPanel> cut = RenderPanel([], canAdd: false);

        // Assert
        Assert.Empty(cut.FindAll("[data-testid='notes-add']"));
        Assert.Empty(cut.FindAll("#notes-body"));
    }

    [Fact]
    public void NotesPanel_WhitespaceOnlyBody_KeepsAddButtonDisabled()
    {
        // Arrange
        IRenderedComponent<NotesPanel> cut = RenderPanel([]);

        // Act
        cut.Find("#notes-body").Change("   ");

        // Assert
        Assert.True(cut.Find("[data-testid='notes-add']").HasAttribute("disabled"));
    }

    [Fact]
    public void NotesPanel_Counter_CountsTheTrimmedLengthAgainstTheLimit()
    {
        // Arrange
        IRenderedComponent<NotesPanel> cut = RenderPanel([]);

        // Act
        cut.Find("#notes-body").Change("   abc   ");

        // Assert
        cut.WaitForAssertion(() => Assert.Contains("3997", cut.Find("[data-testid='notes-remaining']").TextContent));
    }

    [Fact]
    public void NotesPanel_ClosedList_ShowsNotesButNoForm()
    {
        // Arrange
        var notes = new[] { new NoteItem(Guid.NewGuid(), "op-alpha", FixedUtc, "closing remark") };

        // Act
        IRenderedComponent<NotesPanel> cut = RenderPanel(notes, canAdd: true, isOpen: false);

        // Assert
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='notes-item']")));
        Assert.Empty(cut.FindAll("#notes-body"));
        Assert.Empty(cut.FindAll("[data-testid='notes-add']"));
    }

    [Fact]
    public void NotesPanel_AddClick_SendsTheTrimmedBody()
    {
        // Arrange
        string? sent = null;
        IRenderedComponent<NotesPanel> cut = RenderPanel([], add: (body, _) =>
        {
            sent = body;
            return Task.CompletedTask;
        });
        cut.Find("#notes-body").Change("  hello  ");

        // Act
        cut.Find("[data-testid='notes-add']").Click();

        // Assert
        cut.WaitForAssertion(() => Assert.Equal("hello", sent));
    }

    [Fact]
    public void NotesPanel_AddThrowsInvalidOperation_ShowsErrorText()
    {
        // Arrange
        IRenderedComponent<NotesPanel> cut = RenderPanel([], add: (_, _) => throw new InvalidOperationException("closed"));
        cut.Find("#notes-body").Change("hello");

        // Act
        cut.Find("[data-testid='notes-add']").Click();

        // Assert
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='notes-error']")));
    }
}
