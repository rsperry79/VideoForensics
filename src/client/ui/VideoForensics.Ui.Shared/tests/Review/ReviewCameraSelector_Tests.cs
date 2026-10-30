using Bunit;

using VideoForensics.Client.Common.Viewer;
using VideoForensics.Client.Ui.Shared.Components.Review;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>The camera selector: its button label, the popup toggles, the nine-camera cap, Reset, and Apply to both states.</summary>
public class ReviewCameraSelector_Tests : ReviewTestContext
{
    private IRenderedComponent<ReviewCameraSelector> RenderSelector()
    {
        return Render<ReviewCameraSelector>(parameters => parameters
            .Add(p => p.State, ReviewState)
            .Add(p => p.Devices, Devices.Select(d => d.Id).ToList())
            .Add(p => p.DeviceNames, Devices.ToDictionary(d => d.Id, NameOf)));
    }

    private void SelectOnly(params int[] deviceIndexes)
    {
        ReviewState.SetCameras(deviceIndexes.Select(i => Devices[i].Id));
    }

    [Fact]
    public void ReviewCameraSelector_EveryCameraSelected_ShowsAllCamerasLabel()
    {
        // Arrange
        AddDevices(3);
        SelectOnly(0, 1, 2);

        // Act
        IRenderedComponent<ReviewCameraSelector> cut = RenderSelector();

        // Assert
        Assert.Equal(L["AllCameras"].Value, cut.Find("[data-testid='review-camera-button']").TextContent.Trim());
    }

    [Fact]
    public void ReviewCameraSelector_TwoCamerasSelected_ShowsThePluralCountLabel()
    {
        // Arrange
        AddDevices(3);
        SelectOnly(0, 1);

        // Act
        IRenderedComponent<ReviewCameraSelector> cut = RenderSelector();

        // Assert
        Assert.Equal(L["CamerasSelected", 2].Value, cut.Find("[data-testid='review-camera-button']").TextContent.Trim());
        Assert.Equal("2 cameras", cut.Find("[data-testid='review-camera-button']").TextContent.Trim());
    }

    [Fact]
    public void ReviewCameraSelector_OneCameraSelected_ShowsTheSingularCountLabel()
    {
        // Arrange
        AddDevices(3);
        SelectOnly(0);

        // Act
        IRenderedComponent<ReviewCameraSelector> cut = RenderSelector();

        // Assert
        Assert.Equal(L["CameraSelectedOne"].Value, cut.Find("[data-testid='review-camera-button']").TextContent.Trim());
        Assert.Equal("1 camera", cut.Find("[data-testid='review-camera-button']").TextContent.Trim());
    }

    [Fact]
    public void ReviewCameraSelector_ApplyWithToggles_WritesReviewStateAndScope()
    {
        // Arrange
        AddDevices(3);
        SelectOnly(0, 1, 2);
        IRenderedComponent<ReviewCameraSelector> cut = RenderSelector();
        cut.Find("[data-testid='review-camera-button']").Click();

        // Act
        cut.Find($"[data-testid='review-camera-toggle-{Devices[1].Id:D}']").Change(false);
        cut.Find("[data-testid='review-camera-apply']").Click();

        // Assert
        Assert.Equal([Devices[0].Id, Devices[2].Id], ReviewState.CameraIds);
        Assert.Equal([Devices[0].Id, Devices[2].Id], ScopeState.Current.DeviceIds);
    }

    [Fact]
    public void ReviewCameraSelector_TenthCamera_DisablesApplyAndShowsTheHint()
    {
        // Arrange
        AddDevices(10);
        SelectOnly(0, 1, 2, 3, 4, 5, 6, 7, 8);
        IRenderedComponent<ReviewCameraSelector> cut = RenderSelector();
        cut.Find("[data-testid='review-camera-button']").Click();

        // Act
        cut.Find($"[data-testid='review-camera-toggle-{Devices[9].Id:D}']").Change(true);

        // Assert
        Assert.True(cut.Find("[data-testid='review-camera-apply']").HasAttribute("disabled"));
        Assert.Equal(L["TooManyCameras", CameraSync.MaxTiles].Value, cut.Find("[data-testid='review-camera-hint']").TextContent.Trim());
    }

    [Fact]
    public void ReviewCameraSelector_ResetWithTwelveCameras_SelectsTheFirstNine()
    {
        // Arrange
        AddDevices(12);
        SelectOnly(0);
        IRenderedComponent<ReviewCameraSelector> cut = RenderSelector();
        cut.Find("[data-testid='review-camera-button']").Click();

        // Act
        cut.Find("[data-testid='review-camera-reset']").Click();
        cut.Find("[data-testid='review-camera-apply']").Click();

        // Assert
        Assert.Equal(Devices.Take(CameraSync.MaxTiles).Select(d => d.Id), ReviewState.CameraIds);
    }

    [Fact]
    public void ReviewCameraSelector_NoCameraSelected_DisablesApplyAndShowsTheHint()
    {
        // Arrange
        AddDevices(2);
        SelectOnly(0);
        IRenderedComponent<ReviewCameraSelector> cut = RenderSelector();
        cut.Find("[data-testid='review-camera-button']").Click();

        // Act
        cut.Find($"[data-testid='review-camera-toggle-{Devices[0].Id:D}']").Change(false);

        // Assert
        Assert.True(cut.Find("[data-testid='review-camera-apply']").HasAttribute("disabled"));
        Assert.Equal(L["SelectAtLeastOneCamera"].Value, cut.Find("[data-testid='review-camera-hint']").TextContent.Trim());
    }

    [Fact]
    public void ReviewCameraSelector_AllCamerasToggle_SelectsEveryCamera()
    {
        // Arrange
        AddDevices(3);
        SelectOnly(0);
        IRenderedComponent<ReviewCameraSelector> cut = RenderSelector();
        cut.Find("[data-testid='review-camera-button']").Click();

        // Act
        cut.Find("[data-testid='review-camera-all']").Change(true);
        cut.Find("[data-testid='review-camera-apply']").Click();

        // Assert
        Assert.Equal(Devices.Select(d => d.Id), ReviewState.CameraIds);
    }
}
