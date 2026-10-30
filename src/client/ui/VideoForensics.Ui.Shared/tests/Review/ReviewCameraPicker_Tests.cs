using Bunit;

using Microsoft.AspNetCore.Components;

using VideoForensics.Client.Common.Viewer;
using VideoForensics.Client.Ui.Shared.Components.Review;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>The camera picker: the All switch, one switch per camera, the nine-camera cap hint, and the new set it raises.</summary>
public class ReviewCameraPicker_Tests : ReviewTestContext
{
    private HashSet<Guid>? _emitted;

    private IRenderedComponent<ReviewCameraPicker> RenderPicker(HashSet<Guid> selected)
    {
        return Render<ReviewCameraPicker>(parameters => parameters
            .Add(p => p.Devices, Devices.Select(d => d.Id).ToList())
            .Add(p => p.DeviceNames, Devices.ToDictionary(d => d.Id, NameOf))
            .Add(p => p.Selected, selected)
            .Add(p => p.SelectedChanged, EventCallback.Factory.Create<HashSet<Guid>>(this, set => _emitted = set)));
    }

    private HashSet<Guid> Ids(params int[] indexes)
    {
        return indexes.Select(i => Devices[i].Id).ToHashSet();
    }

    [Fact]
    public void ReviewCameraPicker_Rendered_ShowsOneSwitchPerCameraWithItsName()
    {
        // Arrange
        AddDevices(3);

        // Act
        IRenderedComponent<ReviewCameraPicker> cut = RenderPicker(Ids(0));

        // Assert
        Assert.Equal(3, cut.FindAll("[data-testid^='review-camera-toggle-']").Count);
        Assert.Contains(NameOf(Devices[1]), cut.Markup);
        Assert.True(cut.Find($"[data-testid='review-camera-toggle-{Devices[0].Id:D}']").HasAttribute("checked"));
        Assert.False(cut.Find($"[data-testid='review-camera-toggle-{Devices[1].Id:D}']").HasAttribute("checked"));
    }

    [Fact]
    public void ReviewCameraPicker_AllSwitchOn_RaisesEveryCamera()
    {
        // Arrange
        AddDevices(3);
        IRenderedComponent<ReviewCameraPicker> cut = RenderPicker(Ids(0));

        // Act
        cut.Find("[data-testid='review-camera-all']").Change(true);

        // Assert
        Assert.Equal(Ids(0, 1, 2), _emitted);
    }

    [Fact]
    public void ReviewCameraPicker_AllSwitchOnWithTwelveCameras_RaisesTheFirstNine()
    {
        // Arrange
        AddDevices(12);
        IRenderedComponent<ReviewCameraPicker> cut = RenderPicker(Ids(0));

        // Act
        cut.Find("[data-testid='review-camera-all']").Change(true);

        // Assert
        Assert.Equal(Devices.Take(CameraSync.MaxTiles).Select(d => d.Id).ToHashSet(), _emitted);
    }

    [Fact]
    public void ReviewCameraPicker_AllSwitchOff_RaisesAnEmptySet()
    {
        // Arrange
        AddDevices(3);
        IRenderedComponent<ReviewCameraPicker> cut = RenderPicker(Ids(0, 1, 2));
        Assert.True(cut.Find("[data-testid='review-camera-all']").HasAttribute("checked"));

        // Act
        cut.Find("[data-testid='review-camera-all']").Change(false);

        // Assert
        Assert.NotNull(_emitted);
        Assert.Empty(_emitted);
    }

    [Fact]
    public void ReviewCameraPicker_CameraToggled_RaisesANewSetAndLeavesTheParentsSetAlone()
    {
        // Arrange
        AddDevices(3);
        HashSet<Guid> parent = Ids(0, 1);
        IRenderedComponent<ReviewCameraPicker> cut = RenderPicker(parent);

        // Act
        cut.Find($"[data-testid='review-camera-toggle-{Devices[2].Id:D}']").Change(true);
        cut.Find($"[data-testid='review-camera-toggle-{Devices[0].Id:D}']").Change(false);

        // Assert
        Assert.NotNull(_emitted);
        Assert.NotSame(parent, _emitted);
        Assert.Equal(Ids(0, 1), parent);
    }

    [Fact]
    public void ReviewCameraPicker_CameraSwitchedOn_RaisesTheSelectionPlusThatCamera()
    {
        // Arrange
        AddDevices(3);
        IRenderedComponent<ReviewCameraPicker> cut = RenderPicker(Ids(0));

        // Act
        cut.Find($"[data-testid='review-camera-toggle-{Devices[1].Id:D}']").Change(true);

        // Assert
        Assert.Equal(Ids(0, 1), _emitted);
    }

    [Fact]
    public void ReviewCameraPicker_CameraSwitchedOff_RaisesTheSelectionWithoutThatCamera()
    {
        // Arrange
        AddDevices(3);
        IRenderedComponent<ReviewCameraPicker> cut = RenderPicker(Ids(0, 1));

        // Act
        cut.Find($"[data-testid='review-camera-toggle-{Devices[1].Id:D}']").Change(false);

        // Assert
        Assert.Equal(Ids(0), _emitted);
    }

    [Fact]
    public void ReviewCameraPicker_NoCameraSelected_ShowsTheSelectOneHint()
    {
        // Arrange
        AddDevices(2);

        // Act
        IRenderedComponent<ReviewCameraPicker> cut = RenderPicker([]);

        // Assert
        Assert.Equal(L["SelectAtLeastOneCamera"].Value, cut.Find("[data-testid='review-camera-hint']").TextContent.Trim());
    }

    [Fact]
    public void ReviewCameraPicker_MoreThanNineSelected_ShowsTheTooManyHint()
    {
        // Arrange
        AddDevices(10);

        // Act
        IRenderedComponent<ReviewCameraPicker> cut = RenderPicker(Ids(0, 1, 2, 3, 4, 5, 6, 7, 8, 9));

        // Assert
        Assert.Equal(L["TooManyCameras", CameraSync.MaxTiles].Value, cut.Find("[data-testid='review-camera-hint']").TextContent.Trim());
    }

    [Fact]
    public void ReviewCameraPicker_ValidSelection_ShowsNoHint()
    {
        // Arrange
        AddDevices(3);

        // Act
        IRenderedComponent<ReviewCameraPicker> cut = RenderPicker(Ids(0, 1));

        // Assert
        Assert.Empty(cut.FindAll("[data-testid='review-camera-hint']"));
    }

    [Fact]
    public void ReviewCameraPicker_NoDevices_ShowsTheNoCamerasNote()
    {
        // Arrange and Act
        IRenderedComponent<ReviewCameraPicker> cut = RenderPicker([]);

        // Assert
        Assert.Contains(L["NoCameras"].Value, cut.Markup);
    }
}
