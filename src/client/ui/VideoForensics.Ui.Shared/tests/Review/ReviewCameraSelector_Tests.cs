using Bunit;

using VideoForensics.Client.Common.Viewer;
using VideoForensics.Client.Ui.Shared.Components.Review;
using VideoForensics.Client.Ui.Shared.Services.Review;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>The camera selector: its button label, the popup toggles, the nine-camera cap, and the debounced auto-apply to both states.</summary>
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
        Assert.Equal("2 Cameras", cut.Find("[data-testid='review-camera-button']").TextContent.Trim());
    }

    [Fact]
    public void ReviewCameraSelector_OneCameraSelected_ShowsTheCameraName()
    {
        // Arrange
        AddDevices(3);
        SelectOnly(0);

        // Act
        IRenderedComponent<ReviewCameraSelector> cut = RenderSelector();

        // Assert
        Assert.Equal(NameOf(Devices[0]), cut.Find("[data-testid='review-camera-button']").TextContent.Trim());
    }

    [Fact]
    public void ReviewCameraSelector_AllDevicesMode_TwoSelected_ShowsTheDeviceCount()
    {
        // Arrange
        AddDevices(3);
        SelectOnly(0, 1);

        // Act
        IRenderedComponent<ReviewCameraSelector> cut = Render<ReviewCameraSelector>(parameters => parameters
            .Add(p => p.State, ReviewState)
            .Add(p => p.Mode, DevicePickerMode.AllDevices)
            .Add(p => p.Devices, Devices.Select(d => d.Id).ToList())
            .Add(p => p.DeviceNames, Devices.ToDictionary(d => d.Id, NameOf)));

        // Assert
        Assert.Equal("2 Devices", cut.Find("[data-testid='review-camera-button']").TextContent.Trim());
    }

    [Fact]
    public void ReviewCameraSelector_AllDevicesMode_EveryDeviceSelected_ShowsAllDevices()
    {
        // Arrange
        AddDevices(3);
        SelectOnly(0, 1, 2);

        // Act
        IRenderedComponent<ReviewCameraSelector> cut = Render<ReviewCameraSelector>(parameters => parameters
            .Add(p => p.State, ReviewState)
            .Add(p => p.Mode, DevicePickerMode.AllDevices)
            .Add(p => p.Devices, Devices.Select(d => d.Id).ToList())
            .Add(p => p.DeviceNames, Devices.ToDictionary(d => d.Id, NameOf)));

        // Assert
        Assert.Equal(L["AllDevices"].Value, cut.Find("[data-testid='review-camera-button']").TextContent.Trim());
    }

    [Fact]
    public void ReviewCameraSelector_Opened_HasNoApplyOrResetButtons()
    {
        // Arrange
        AddDevices(3);
        SelectOnly(0, 1, 2);
        IRenderedComponent<ReviewCameraSelector> cut = RenderSelector();

        // Act
        cut.Find("[data-testid='review-camera-button']").Click();

        // Assert
        Assert.Empty(cut.FindAll("[data-testid='review-camera-apply']"));
        Assert.Empty(cut.FindAll("[data-testid='review-camera-reset']"));
        Assert.Contains("review-popup", cut.Find("[data-testid='review-camera-popup']").ClassName);
    }

    [Fact]
    public void ReviewCameraSelector_Button_IsFullWidth()
    {
        // Arrange
        AddDevices(2);

        // Act
        IRenderedComponent<ReviewCameraSelector> cut = RenderSelector();

        // Assert
        string classes = cut.Find("[data-testid='review-camera-button']").ClassName!;
        Assert.Contains("w-100", classes);
        Assert.Contains("text-start", classes);
    }

    [Fact]
    public void ReviewCameraSelector_ToggleOff_WritesReviewStateAndScopeAfterTheDebounce()
    {
        // Arrange
        AddDevices(3);
        SelectOnly(0, 1, 2);
        IRenderedComponent<ReviewCameraSelector> cut = RenderSelector();
        cut.Find("[data-testid='review-camera-button']").Click();

        // Act
        cut.Find($"[data-testid='review-camera-toggle-{Devices[1].Id:D}']").Change(false);

        // Assert
        cut.WaitForAssertion(() => Assert.Equal([Devices[0].Id, Devices[2].Id], ReviewState.CameraIds));
        Assert.Equal([Devices[0].Id, Devices[2].Id], ScopeState.Current.DeviceIds);
    }

    [Fact]
    public void ReviewCameraSelector_Toggle_DoesNotApplyBeforeTheDebounceElapses()
    {
        // Arrange
        AddDevices(3);
        SelectOnly(0, 1, 2);
        ReviewState.DebounceDelay = TimeSpan.FromHours(1);
        IRenderedComponent<ReviewCameraSelector> cut = RenderSelector();
        cut.Find("[data-testid='review-camera-button']").Click();

        // Act
        cut.Find($"[data-testid='review-camera-toggle-{Devices[1].Id:D}']").Change(false);

        // Assert
        Assert.Equal(3, ReviewState.CameraIds.Count);
    }

    [Fact]
    public void ReviewCameraSelector_TogglesInABurst_ApplyOnce()
    {
        // Arrange
        AddDevices(3);
        SelectOnly(0, 1, 2);
        ReviewState.DebounceDelay = TimeSpan.FromMilliseconds(150);
        IRenderedComponent<ReviewCameraSelector> cut = RenderSelector();
        cut.Find("[data-testid='review-camera-button']").Click();
        int changes = 0;
        ReviewState.OnChange += () => changes++;

        // Act
        cut.Find($"[data-testid='review-camera-toggle-{Devices[1].Id:D}']").Change(false);
        cut.Find($"[data-testid='review-camera-toggle-{Devices[2].Id:D}']").Change(false);

        // Assert
        cut.WaitForAssertion(() => Assert.Equal([Devices[0].Id], ReviewState.CameraIds));
        Thread.Sleep(300);
        Assert.Equal(1, Volatile.Read(ref changes));
    }

    [Fact]
    public void ReviewCameraSelector_TenthCamera_DoesNotApplyAndShowsTheHint()
    {
        // Arrange
        AddDevices(10);
        SelectOnly(0, 1, 2, 3, 4, 5, 6, 7, 8);
        IRenderedComponent<ReviewCameraSelector> cut = RenderSelector();
        cut.Find("[data-testid='review-camera-button']").Click();

        // Act
        cut.Find($"[data-testid='review-camera-toggle-{Devices[9].Id:D}']").Change(true);
        Thread.Sleep(150);

        // Assert
        Assert.Equal(CameraSync.MaxTiles, ReviewState.CameraIds.Count);
        Assert.DoesNotContain(Devices[9].Id, ReviewState.CameraIds);
        Assert.Equal(L["TooManyCameras", CameraSync.MaxTiles].Value, cut.Find("[data-testid='review-camera-hint']").TextContent.Trim());
    }

    [Fact]
    public void ReviewCameraSelector_NoCameraSelected_DoesNotApplyAndShowsTheHint()
    {
        // Arrange
        AddDevices(2);
        SelectOnly(0);
        IRenderedComponent<ReviewCameraSelector> cut = RenderSelector();
        cut.Find("[data-testid='review-camera-button']").Click();

        // Act
        cut.Find($"[data-testid='review-camera-toggle-{Devices[0].Id:D}']").Change(false);
        Thread.Sleep(150);

        // Assert
        Assert.Equal([Devices[0].Id], ReviewState.CameraIds);
        Assert.Equal(L["SelectAtLeastOneCamera"].Value, cut.Find("[data-testid='review-camera-hint']").TextContent.Trim());
    }

    [Fact]
    public void ReviewCameraSelector_AllCamerasToggle_SelectsEveryCameraAfterTheDebounce()
    {
        // Arrange
        AddDevices(3);
        SelectOnly(0);
        IRenderedComponent<ReviewCameraSelector> cut = RenderSelector();
        cut.Find("[data-testid='review-camera-button']").Click();

        // Act
        cut.Find("[data-testid='review-camera-all']").Change(true);

        // Assert
        cut.WaitForAssertion(() => Assert.Equal(Devices.Select(d => d.Id), ReviewState.CameraIds));
    }

    [Fact]
    public void ReviewCameraSelector_AllCamerasToggleWithTwelveCameras_SelectsTheFirstNine()
    {
        // Arrange
        AddDevices(12);
        SelectOnly(0);
        IRenderedComponent<ReviewCameraSelector> cut = RenderSelector();
        cut.Find("[data-testid='review-camera-button']").Click();

        // Act
        cut.Find("[data-testid='review-camera-all']").Change(true);

        // Assert
        cut.WaitForAssertion(() => Assert.Equal(Devices.Take(CameraSync.MaxTiles).Select(d => d.Id), ReviewState.CameraIds));
    }

    [Fact]
    public void ReviewCameraSelector_EachSwitch_TogglesItsOwnCamera()
    {
        // Arrange
        AddDevices(3);
        SelectOnly(0);
        IRenderedComponent<ReviewCameraSelector> cut = RenderSelector();
        cut.Find("[data-testid='review-camera-button']").Click();

        // Act - a real browser sends the checkbox state as a boolean change value
        cut.Find($"[data-testid='review-camera-toggle-{Devices[2].Id:D}']").Change(true);

        // Assert
        cut.WaitForAssertion(() => Assert.Equal([Devices[0].Id, Devices[2].Id], ReviewState.CameraIds));
        Assert.True(cut.Find($"[data-testid='review-camera-toggle-{Devices[2].Id:D}']").HasAttribute("checked"));
    }

    [Fact]
    public void ReviewCameraSelector_Escape_ClosesThePopup()
    {
        // Arrange
        AddDevices(2);
        IRenderedComponent<ReviewCameraSelector> cut = RenderSelector();
        cut.Find("[data-testid='review-camera-button']").Click();

        // Act
        cut.Find("[data-testid='review-camera-selector']").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });

        // Assert
        Assert.Empty(cut.FindAll("[data-testid='review-camera-popup']"));
    }
}
