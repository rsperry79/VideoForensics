namespace VideoForensics.Ui.Shared.Tests;

using Xunit;
using VideoForensics.Ui.Shared.Services.Inspector;

public class InspectorState_Show_Tests
{
    [Fact]
    public void Show_SetsCurrentModel()
    {
        // Arrange
        var state = new InspectorState();
        var model = new InspectorModel(Title: "TestTitle", Fields: new { Name = "Test" });

        // Act
        state.Show(model);

        // Assert
        Assert.Equal(model, state.Current);
    }

    [Fact]
    public void Show_RaisesOnChange()
    {
        // Arrange
        var state = new InspectorState();
        var model = new InspectorModel(Title: "TestTitle", Fields: new { Name = "Test" });
        var changeRaised = false;
        state.OnChange += () => changeRaised = true;

        // Act
        state.Show(model);

        // Assert
        Assert.True(changeRaised);
    }

    [Fact]
    public void Show_ReplacesCurrentModel()
    {
        // Arrange
        var state = new InspectorState();
        var model1 = new InspectorModel(Title: "Title1", Fields: new { Name = "Test1" });
        var model2 = new InspectorModel(Title: "Title2", Fields: new { Name = "Test2" });

        // Act
        state.Show(model1);
        var firstCurrent = state.Current;
        state.Show(model2);
        var secondCurrent = state.Current;

        // Assert
        Assert.Equal(model1, firstCurrent);
        Assert.Equal(model2, secondCurrent);
        Assert.NotEqual(firstCurrent, secondCurrent);
    }
}

public class InspectorState_Clear_Tests
{
    [Fact]
    public void Clear_SetsCurrentToNull()
    {
        // Arrange
        var state = new InspectorState();
        var model = new InspectorModel(Title: "TestTitle", Fields: new { Name = "Test" });
        state.Show(model);

        // Act
        state.Clear();

        // Assert
        Assert.Null(state.Current);
    }

    [Fact]
    public void Clear_RaisesOnChange_WhenCurrentNotNull()
    {
        // Arrange
        var state = new InspectorState();
        var model = new InspectorModel(Title: "TestTitle", Fields: new { Name = "Test" });
        state.Show(model);
        var changeRaised = false;
        state.OnChange += () => changeRaised = true;

        // Act
        state.Clear();

        // Assert
        Assert.True(changeRaised);
    }

    [Fact]
    public void Clear_DoesNotRaiseOnChange_WhenCurrentNull()
    {
        // Arrange
        var state = new InspectorState();
        var changeRaised = false;
        state.OnChange += () => changeRaised = true;

        // Act
        state.Clear();

        // Assert
        Assert.False(changeRaised);
    }

    [Fact]
    public void Clear_IsNoOp_WhenAlreadyNull()
    {
        // Arrange
        var state = new InspectorState();
        var changeCount = 0;
        state.OnChange += () => changeCount++;

        // Act
        state.Clear();
        state.Clear();

        // Assert
        Assert.Equal(0, changeCount);
    }
}
