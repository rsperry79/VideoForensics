using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

using Moq;

using VideoForensics.Client.Ui.Shared.Components.Analysis;
using VideoForensics.Client.Ui.Shared.Components.Notes;
using VideoForensics.Client.Ui.Shared.Services;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

using NoteEntryCheck = VideoForensics.Client.Ui.Shared.Components.Notes.NotesPanel.NoteEntryCheck;

namespace VideoForensics.Client.Ui.Shared.Tests.Components.Analysis;

/// <summary>
/// The jamming notes wrapper: it loads and adds notes for its jamming incident, maps repository notes to NoteItem, sends the
/// trimmed body with the signed-in operator id, and hides the add form when the operator may not add. The panel's own
/// behaviour is tested in NotesPanel_Tests.
/// </summary>
public class JammingNotes_Tests : BunitContext
{
    private static readonly Guid TestOperatorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly Guid _incidentId = Guid.NewGuid();
    private readonly Mock<INoteRepository> _notes = new();

    public JammingNotes_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddLocalization();
        _ = Services.AddLogging();
        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        _ = Services.AddSingleton(_notes.Object);
        _ = _notes.Setup(n => n.ListAsync(NoteTargetKind.JammingIncident, _incidentId, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<Note>());

        _ = JSInterop.Setup<NoteEntryCheck>("vfCaseNoteCheck", inv => inv.Arguments.Count > 0 && inv.Arguments[0] is string text && text.Trim().Length > 0)
            .SetResult(new NoteEntryCheck(true, 5, null));
    }

    /// <summary>Registers a signed-in operator, as the analysis page reads it.</summary>
    private async Task SignInAsync()
    {
        var session = new PairedSessionState(JSInterop.JSRuntime);
        await session.SetAsync("test-token", TestOperatorId, "Review");
        _ = Services.AddScoped(_ => session);
    }

    private static Note Note(Guid incidentId, string author, string body)
    {
        return new Note { Id = Guid.NewGuid(), TargetKind = NoteTargetKind.JammingIncident, TargetId = incidentId, Body = body, AddedBy = author, AddedAtUtc = new DateTime(2026, 11, 1, 9, 30, 0, DateTimeKind.Utc) };
    }

    [Fact]
    public async Task JammingNotes_Render_ListsTheNotesOfItsIncident()
    {
        // Arrange
        await SignInAsync();
        _ = _notes.Setup(n => n.ListAsync(NoteTargetKind.JammingIncident, _incidentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Note(_incidentId, "op-alpha", "incident remark") });

        // Act
        IRenderedComponent<JammingNotes> cut = Render<JammingNotes>(p => p
            .Add(c => c.JammingIncidentId, _incidentId)
            .Add(c => c.CanAdd, true)
            .Add(c => c.IsOpen, true));

        // Assert
        cut.WaitForAssertion(() => Assert.Contains("incident remark", cut.Find("[data-testid='notes-item']").TextContent));
    }

    [Fact]
    public async Task JammingNotes_Add_SendsTrimmedBodyWithOperatorId()
    {
        // Arrange
        await SignInAsync();
        _ = _notes.Setup(n => n.AddAsync(NoteTargetKind.JammingIncident, _incidentId, "hello", TestOperatorId.ToString(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Note(_incidentId, TestOperatorId.ToString(), "hello"));
        IRenderedComponent<JammingNotes> cut = Render<JammingNotes>(p => p
            .Add(c => c.JammingIncidentId, _incidentId)
            .Add(c => c.CanAdd, true)
            .Add(c => c.IsOpen, true));
        cut.Find("#notes-body").Change("  hello  ");

        // Act
        cut.Find("[data-testid='notes-add']").Click();

        // Assert
        cut.WaitForAssertion(() =>
            _notes.Verify(n => n.AddAsync(NoteTargetKind.JammingIncident, _incidentId, "hello", TestOperatorId.ToString(), It.IsAny<CancellationToken>()), Times.Once));
    }

    [Fact]
    public async Task JammingNotes_CanAddFalse_HidesTheAddForm()
    {
        // Arrange
        await SignInAsync();

        // Act
        IRenderedComponent<JammingNotes> cut = Render<JammingNotes>(p => p
            .Add(c => c.JammingIncidentId, _incidentId)
            .Add(c => c.CanAdd, false)
            .Add(c => c.IsOpen, true));

        // Assert
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='notes-form']")));
    }
}
