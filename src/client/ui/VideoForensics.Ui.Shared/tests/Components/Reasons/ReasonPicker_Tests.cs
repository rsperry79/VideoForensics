using VideoForensics.Core.Api.Contracts;
using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using Syncfusion.Blazor.DropDowns;

using VideoForensics.Client.Ui.Shared.Components.Reasons;
using VideoForensics.Client.Ui.Shared.Resources;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Components.Reasons;

/// <summary>
/// The reusable reason picker: the dropdown lists every enum value in declaration order, the note box appears only for the
/// Other value, and the picker reports whether its reason is complete.
/// </summary>
public class ReasonPicker_Tests : BunitContext
{
    private const string NoteTestId = "[data-testid='reason-picker-note']";

    public ReasonPicker_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddLocalization();
        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
    }

    private IRenderedComponent<ReasonPicker<MediaFlagDismissReasonDto>> RenderPicker(
        MediaFlagDismissReasonDto? value,
        string? note,
        List<bool> validity)
    {
        return Render<ReasonPicker<MediaFlagDismissReasonDto>>(p => p
            .Add(c => c.Value, value)
            .Add(c => c.OtherValue, MediaFlagDismissReasonDto.Other)
            .Add(c => c.Note, note)
            .Add(c => c.LabelText, "Reason")
            .Add(c => c.ValidityChanged, EventCallback.Factory.Create<bool>(this, validity.Add)));
    }

    [Fact]
    public void ReasonPicker_DropDown_ListsAllValuesInDeclarationOrder()
    {
        // Arrange and Act
        IRenderedComponent<ReasonPicker<MediaFlagDismissReasonDto>> cut = RenderPicker(null, null, []);

        // Assert
        IRenderedComponent<SfDropDownList<MediaFlagDismissReasonDto?, ReasonOption<MediaFlagDismissReasonDto>>> dropdown =
            cut.FindComponent<SfDropDownList<MediaFlagDismissReasonDto?, ReasonOption<MediaFlagDismissReasonDto>>>();
        var values = dropdown.Instance.DataSource!.Select(option => option.Value).ToList();
        Assert.Equal(Enum.GetValues<MediaFlagDismissReasonDto>(), values);
    }

    [Fact]
    public void ReasonPicker_NoteBox_AppearsOnlyForOther()
    {
        // Arrange and Act
        IRenderedComponent<ReasonPicker<MediaFlagDismissReasonDto>> plain = RenderPicker(MediaFlagDismissReasonDto.Duplicate, null, []);
        IRenderedComponent<ReasonPicker<MediaFlagDismissReasonDto>> other = RenderPicker(MediaFlagDismissReasonDto.Other, null, []);

        // Assert
        Assert.Empty(plain.FindAll(NoteTestId));
        Assert.NotEmpty(other.FindAll(NoteTestId));
    }

    [Fact]
    public void ReasonPicker_NoValue_ShowsPlaceholderAndIsInvalid()
    {
        // Arrange
        var validity = new List<bool>();
        string expectedPlaceholder = Services.GetRequiredService<IStringLocalizer<SharedResources>>()["ReasonPickerPlaceholder"].Value;

        // Act
        IRenderedComponent<ReasonPicker<MediaFlagDismissReasonDto>> cut = RenderPicker(null, null, validity);

        // Assert
        IRenderedComponent<SfDropDownList<MediaFlagDismissReasonDto?, ReasonOption<MediaFlagDismissReasonDto>>> dropdown =
            cut.FindComponent<SfDropDownList<MediaFlagDismissReasonDto?, ReasonOption<MediaFlagDismissReasonDto>>>();
        Assert.Equal(expectedPlaceholder, dropdown.Instance.Placeholder);
        Assert.Equal(new[] { false }, validity);
    }

    [Fact]
    public void ReasonPicker_Validity_NoValue_IsFalse()
    {
        // Arrange
        var validity = new List<bool>();

        // Act
        _ = RenderPicker(null, null, validity);

        // Assert
        Assert.Equal(new[] { false }, validity);
    }

    [Fact]
    public void ReasonPicker_Validity_OtherWithBlankNote_IsFalse()
    {
        // Arrange
        var validity = new List<bool>();

        // Act
        _ = RenderPicker(MediaFlagDismissReasonDto.Other, "   ", validity);

        // Assert
        Assert.Equal(new[] { false }, validity);
    }

    [Fact]
    public void ReasonPicker_Validity_OtherWithNote_IsTrue()
    {
        // Arrange
        var validity = new List<bool>();

        // Act
        _ = RenderPicker(MediaFlagDismissReasonDto.Other, "camera was moved", validity);

        // Assert
        Assert.Equal(new[] { true }, validity);
    }

    [Fact]
    public void ReasonPicker_Validity_PlainValue_IsTrue()
    {
        // Arrange
        var validity = new List<bool>();

        // Act
        _ = RenderPicker(MediaFlagDismissReasonDto.FalsePositive, null, validity);

        // Assert
        Assert.Equal(new[] { true }, validity);
    }
}
