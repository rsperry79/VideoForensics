using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

using Moq;

using VideoForensics.Client.Ui.Shared.Components.Case;
using VideoForensics.Client.Ui.Shared.Services;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

using NoteEntryCheck = VideoForensics.Client.Ui.Shared.Components.Notes.NotesPanel.NoteEntryCheck;

namespace VideoForensics.Client.Ui.Shared.Tests.Components.Case;

/// <summary>
/// The case notes wrapper: it loads and adds notes for its CaseId, maps repository notes to NoteItem, sends the trimmed body
/// with the signed-in operator id, and maps a missing case to its own message. The panel's behaviour is tested in NotesPanel_Tests.
/// </summary>
public class CaseNotes_Tests : BunitContext
{
    private static readonly Guid TestOperatorId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly Guid _caseId = Guid.NewGuid();
    private readonly Mock<ICaseNoteRepository> _notes = new();

    public CaseNotes_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddLocalization();
        _ = Services.AddLogging();
        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        _ = Services.AddSingleton(_notes.Object);
        _ = _notes.Setup(n => n.ListForCaseAsync(_caseId, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<Note>());

        _ = JSInterop.Setup<NoteEntryCheck>("vfCaseNoteCheck", inv => inv.Arguments.Count > 0 && inv.Arguments[0] is string text && text.Trim().Length > 0)
            .SetResult(new NoteEntryCheck(true, 5, null));
    }

    /// <summary>Registers a signed-in operator, as the case page reads it.</summary>
    private async Task SignInAsync()
    {
        var session = new PairedSessionState(JSInterop.JSRuntime);
        await session.SetAsync("test-token", TestOperatorId, "Review");
        _ = Services.AddScoped(_ => session);
    }

    private static Note Note(Guid caseId, string author, string body)
    {
        return new Note { Id = Guid.NewGuid(), TargetKind = NoteTargetKind.Case, TargetId = caseId, Body = body, AddedBy = author, AddedAtUtc = new DateTime(2026, 11, 1, 9, 30, 0, DateTimeKind.Utc) };
    }

    [Fact]
    public async Task CaseNotes_Render_ListsTheNotesOfItsCase()
    {
        // Arrange
        await SignInAsync();
        _ = _notes.Setup(n => n.ListForCaseAsync(_caseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Note(_caseId, "op-alpha", "case remark") });

        // Act
        IRenderedComponent<CaseNotes> cut = Render<CaseNotes>(p => p
            .Add(c => c.CaseId, _caseId)
            .Add(c => c.CanAdd, true)
            .Add(c => c.IsOpen, true));

        // Assert
        cut.WaitForAssertion(() => Assert.Contains("case remark", cut.Find("[data-testid='notes-item']").TextContent));
    }

    [Fact]
    public async Task CaseNotes_Add_SendsTrimmedBodyWithOperatorId()
    {
        // Arrange
        await SignInAsync();
        _ = _notes.Setup(n => n.AddAsync(_caseId, "hello", TestOperatorId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Note(_caseId, TestOperatorId.ToString(), "hello"));
        IRenderedComponent<CaseNotes> cut = Render<CaseNotes>(p => p
            .Add(c => c.CaseId, _caseId)
            .Add(c => c.CanAdd, true)
            .Add(c => c.IsOpen, true));
        cut.Find("#notes-body").Change("  hello  ");

        // Act
        cut.Find("[data-testid='notes-add']").Click();

        // Assert
        cut.WaitForAssertion(() =>
            _notes.Verify(n => n.AddAsync(_caseId, "hello", TestOperatorId.ToString(), It.IsAny<CancellationToken>()), Times.Once));
    }

    [Fact]
    public async Task CaseNotes_AddToMissingCase_ShowsErrorFromThePanel()
    {
        // Arrange
        await SignInAsync();
        _ = _notes.Setup(n => n.AddAsync(_caseId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("case"));
        IRenderedComponent<CaseNotes> cut = Render<CaseNotes>(p => p
            .Add(c => c.CaseId, _caseId)
            .Add(c => c.CanAdd, true)
            .Add(c => c.IsOpen, true));
        cut.Find("#notes-body").Change("hello");

        // Act
        cut.Find("[data-testid='notes-add']").Click();

        // Assert
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.Find("[data-testid='notes-error']").TextContent));
    }
}
