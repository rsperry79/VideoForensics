using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using Syncfusion.Blazor.Buttons;
using Syncfusion.Blazor.Grids;

using VideoForensics.Client.Ui.Shared.Components.Analysis;
using VideoForensics.Client.Ui.Shared.Components.Notes;
using VideoForensics.Client.Ui.Shared.Services.Scope;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Models;

using Xunit;

using Device = VideoForensics.Data.Common.Entities.Device;

namespace VideoForensics.Client.Ui.Shared.Tests.Components.Analysis;

/// <summary>
/// The jamming panel: it has no free-text notes box on the manual record form, the record sends no notes, and selecting a
/// detected incident opens its notes thread.
/// </summary>
public class JammingPanel_Tests : AnalyzePageTestBase
{
    public JammingPanel_Tests()
    {
        _ = Services.AddLogging();
    }

    /// <summary>Renders the panel for one device over the test window.</summary>
    private IRenderedComponent<JammingPanel> RenderPanel(Device device)
    {
        return Render<JammingPanel>(p => p
            .Add(c => c.Scope, new ForensicScope([device.Id], FixedNowUtc.AddDays(-1), FixedNowUtc, null))
            .Add(c => c.Devices, [device]));
    }

    /// <summary>Builds the row-select event the grid raises for a clicked row. Syncfusion keeps the row data read-only.</summary>
    private static RowSelectEventArgs<JammingIncidentRecord> SelectArgs(JammingIncidentRecord row)
    {
        var args = new RowSelectEventArgs<JammingIncidentRecord>();
        typeof(RowSelectEventArgs<JammingIncidentRecord>)
            .GetField("<Data>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(args, row);
        return args;
    }

    [Fact]
    public void JammingPanel_Render_HasNoManualNotesBox()
    {
        // Arrange
        Device device = CreateTestDevice("Porch");

        // Act
        IRenderedComponent<JammingPanel> cut = RenderPanel(device);

        // Assert
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindComponents<SfButton>()));
        Assert.Empty(cut.FindAll("textarea"));
    }

    [Fact]
    public async Task JammingPanel_RecordIncident_SendsNullNotes()
    {
        // Arrange
        Device device = CreateTestDevice("Porch");
        JammingRepositoryMock
            .Setup(m => m.UpsertIncidentAsync(It.IsAny<JammingIncidentRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JammingIncidentRecord r, CancellationToken _) => r);
        IRenderedComponent<JammingPanel> cut = RenderPanel(device);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindComponents<SfButton>()));

        // Act
        await cut.InvokeAsync(() => cut.FindComponent<SfButton>().Find("button").Click());

        // Assert
        cut.WaitForAssertion(() => JammingRepositoryMock.Verify(
            m => m.UpsertIncidentAsync(It.Is<JammingIncidentRecord>(r => r.DeviceId == device.Id && r.Notes == null), It.IsAny<CancellationToken>()),
            Times.Once));
    }

    [Fact]
    public async Task JammingPanel_SelectIncident_ShowsItsNotesThread()
    {
        // Arrange
        Device device = CreateTestDevice("Porch");
        var incident = new JammingIncidentRecord
        {
            Id = Guid.NewGuid(),
            DeviceId = device.Id,
            StartUtc = FixedNowUtc.AddHours(-3),
            EndUtc = FixedNowUtc.AddHours(-2),
            AffectedEventCount = 2,
            AverageDegradationDb = 12,
            Confidence = JammingConfidenceLevel.Medium,
            Source = JammingIncidentSource.AutoDetected,
        };
        JammingRepositoryMock
            .Setup(m => m.ListIncidentsAsync(It.IsAny<Guid?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([incident]);
        IRenderedComponent<JammingPanel> cut = RenderPanel(device);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindComponents<SfGrid<JammingIncidentRecord>>()));
        Assert.Empty(cut.FindComponents<JammingNotes>());

        // Act
        GridEvents<JammingIncidentRecord> events = cut.FindComponent<GridEvents<JammingIncidentRecord>>().Instance;
        await cut.InvokeAsync(() => events.RowSelected.InvokeAsync(SelectArgs(incident)));

        // Assert
        cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<JammingNotes>()));
        Assert.Single(cut.FindComponents<NotesPanel>());
    }
}
