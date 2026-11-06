using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

using Moq;

using VideoForensics.Client.Ui.Shared.Components.Notes;
using VideoForensics.Client.Ui.Shared.Components.Review;
using VideoForensics.Client.Ui.Shared.Services;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

using NoteEntryCheck = VideoForensics.Client.Ui.Shared.Components.Notes.NotesPanel.NoteEntryCheck;

namespace VideoForensics.Client.Ui.Shared.Tests.Components.Review;

/// <summary>
/// The flag notes wrapper: it loads and adds notes for its media flag, maps repository notes to NoteItem, sends the trimmed
/// body with the signed-in operator id, and hides the add form when the operator may not add. The panel's own behaviour is
/// tested in NotesPanel_Tests.
/// </summary>
public class FlagNotes_Tests : BunitContext
{
    private static readonly Guid TestOperatorId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private readonly Guid _flagId = Guid.NewGuid();
    private readonly Mock<INoteRepository> _notes = new();

    public FlagNotes_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddLocalization();
        _ = Services.AddLogging();
        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        _ = Services.AddSingleton(_notes.Object);
        _ = _notes.Setup(n => n.ListAsync(NoteTargetKind.MediaFlag, _flagId, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<Note>());

        _ = JSInterop.Setup<NoteEntryCheck>("vfCaseNoteCheck", inv => inv.Arguments.Count > 0 && inv.Arguments[0] is string text && text.Trim().Length > 0)
            .SetResult(new NoteEntryCheck(true, 5, null));
    }

    /// <summary>Registers a signed-in operator, as the review page reads it.</summary>
    private async Task SignInAsync()
    {
        var session = new PairedSessionState(JSInterop.JSRuntime);
        await session.SetAsync("test-token", TestOperatorId, "Review");
        _ = Services.AddScoped(_ => session);
    }

    private static Note Note(Guid flagId, string author, string body)
    {
        return new Note { Id = Guid.NewGuid(), TargetKind = NoteTargetKind.MediaFlag, TargetId = flagId, Body = body, AddedBy = author, AddedAtUtc = new DateTime(2026, 11, 1, 9, 30, 0, DateTimeKind.Utc) };
    }

    [Fact]
    public async Task FlagNotes_Render_ListsTheNotesOfItsFlag()
    {
        // Arrange
        await SignInAsync();
        _ = _notes.Setup(n => n.ListAsync(NoteTargetKind.MediaFlag, _flagId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Note(_flagId, "op-alpha", "flag remark") });

        // Act
        IRenderedComponent<FlagNotes> cut = Render<FlagNotes>(p => p
            .Add(c => c.FlagId, _flagId)
            .Add(c => c.CanAdd, true)
            .Add(c => c.IsOpen, true));

        // Assert
        cut.WaitForAssertion(() => Assert.Contains("flag remark", cut.Find("[data-testid='notes-item']").TextContent));
    }

    [Fact]
    public async Task FlagNotes_Add_SendsTrimmedBodyWithOperatorId()
    {
        // Arrange
        await SignInAsync();
        _ = _notes.Setup(n => n.AddAsync(NoteTargetKind.MediaFlag, _flagId, "hello", TestOperatorId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Note(_flagId, TestOperatorId.ToString(), "hello"));
        IRenderedComponent<FlagNotes> cut = Render<FlagNotes>(p => p
            .Add(c => c.FlagId, _flagId)
            .Add(c => c.CanAdd, true)
            .Add(c => c.IsOpen, true));
        cut.Find("#notes-body").Change("  hello  ");

        // Act
        cut.Find("[data-testid='notes-add']").Click();

        // Assert
        cut.WaitForAssertion(() =>
            _notes.Verify(n => n.AddAsync(NoteTargetKind.MediaFlag, _flagId, "hello", TestOperatorId.ToString(), It.IsAny<CancellationToken>()), Times.Once));
    }

    [Fact]
    public async Task FlagNotes_CanAddFalse_HidesTheAddForm()
    {
        // Arrange
        await SignInAsync();

        // Act
        IRenderedComponent<FlagNotes> cut = Render<FlagNotes>(p => p
            .Add(c => c.FlagId, _flagId)
            .Add(c => c.CanAdd, false)
            .Add(c => c.IsOpen, true));

        // Assert
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='notes-form']")));
    }
}
