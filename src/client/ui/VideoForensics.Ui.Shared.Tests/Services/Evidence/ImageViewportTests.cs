namespace VideoForensics.Ui.Shared.Tests.Services.Evidence;

using Xunit;
using VideoForensics.Ui.Shared.Services.Evidence;

public class ImageViewport_Initialization_Tests
{
    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        // Act
        var viewport = new ImageViewport();

        // Assert
        Assert.Equal(1, viewport.Scale);
        Assert.Equal(0, viewport.OffsetX);
        Assert.Equal(0, viewport.OffsetY);
    }
}

public class ImageViewport_ZoomIn_Tests
{
    [Fact]
    public void ZoomIn_MultipliesScaleBy1Point25()
    {
        // Arrange
        var viewport = new ImageViewport();

        // Act
        var result = viewport.ZoomIn();

        // Assert
        Assert.Equal(1.25, result.Scale);
        Assert.Equal(0, result.OffsetX);
        Assert.Equal(0, result.OffsetY);
    }

    [Fact]
    public void ZoomIn_ClampsMaxScaleTo8()
    {
        // Arrange
        var viewport = new ImageViewport { Scale = 7 };

        // Act
        var result = viewport.ZoomIn();

        // Assert
        Assert.Equal(8, result.Scale);
    }

    [Fact]
    public void ZoomIn_DoesNotExceedMax()
    {
        // Arrange
        var viewport = new ImageViewport { Scale = 8 };

        // Act
        var result = viewport.ZoomIn();

        // Assert
        Assert.Equal(8, result.Scale);
    }
}

public class ImageViewport_ZoomOut_Tests
{
    [Fact]
    public void ZoomOut_DividesScaleBy1Point25()
    {
        // Arrange
        var viewport = new ImageViewport { Scale = 2.5 };

        // Act
        var result = viewport.ZoomOut();

        // Assert
        Assert.Equal(2, result.Scale, 0.01);
    }

    [Fact]
    public void ZoomOut_ClampsMinScaleTo1()
    {
        // Arrange
        var viewport = new ImageViewport { Scale = 1.1 };

        // Act
        var result = viewport.ZoomOut();

        // Assert
        Assert.Equal(1, result.Scale, 0.01);
    }

    [Fact]
    public void ZoomOut_DoesNotBelowMin()
    {
        // Arrange
        var viewport = new ImageViewport { Scale = 1 };

        // Act
        var result = viewport.ZoomOut();

        // Assert
        Assert.Equal(1, result.Scale);
    }
}

public class ImageViewport_Fit_Tests
{
    [Fact]
    public void Fit_ResetsScaleAndOffset()
    {
        // Arrange
        var viewport = new ImageViewport
        {
            Scale = 3,
            OffsetX = 100,
            OffsetY = 50
        };

        // Act
        var result = viewport.Fit();

        // Assert
        Assert.Equal(1, result.Scale);
        Assert.Equal(0, result.OffsetX);
        Assert.Equal(0, result.OffsetY);
    }
}

public class ImageViewport_PanBy_Tests
{
    [Fact]
    public void PanBy_UpdatesOffsetWhenScaleGreaterThan1()
    {
        // Arrange
        var viewport = new ImageViewport { Scale = 2, OffsetX = 10, OffsetY = 20 };

        // Act
        var result = viewport.PanBy(5, -10);

        // Assert
        Assert.Equal(15, result.OffsetX);
        Assert.Equal(10, result.OffsetY);
        Assert.Equal(2, result.Scale);
    }

    [Fact]
    public void PanBy_IgnoredWhenScaleEquals1()
    {
        // Arrange
        var viewport = new ImageViewport { Scale = 1, OffsetX = 0, OffsetY = 0 };

        // Act
        var result = viewport.PanBy(100, 100);

        // Assert
        Assert.Equal(0, result.OffsetX);
        Assert.Equal(0, result.OffsetY);
        Assert.Equal(1, result.Scale);
    }

    [Fact]
    public void PanBy_NegativeMovement()
    {
        // Arrange
        var viewport = new ImageViewport { Scale = 2, OffsetX = 50, OffsetY = 50 };

        // Act
        var result = viewport.PanBy(-20, -30);

        // Assert
        Assert.Equal(30, result.OffsetX);
        Assert.Equal(20, result.OffsetY);
    }
}

public class ImageViewport_ToCssTransform_Tests
{
    [Fact]
    public void ToCssTransform_DefaultValues_ReturnsNoTransform()
    {
        // Arrange
        var viewport = new ImageViewport();

        // Act
        var transform = viewport.ToCssTransform();

        // Assert
        Assert.Equal("translate(0px, 0px) scale(1)", transform);
    }

    [Fact]
    public void ToCssTransform_WithScaleAndOffset()
    {
        // Arrange
        var viewport = new ImageViewport { Scale = 2, OffsetX = 10, OffsetY = 15 };

        // Act
        var transform = viewport.ToCssTransform();

        // Assert
        Assert.Equal("translate(10px, 15px) scale(2)", transform);
    }

    [Fact]
    public void ToCssTransform_WithDecimalScale()
    {
        // Arrange
        var viewport = new ImageViewport { Scale = 1.25, OffsetX = 5, OffsetY = -5 };

        // Act
        var transform = viewport.ToCssTransform();

        // Assert
        // Should use invariant culture for decimal separator
        Assert.Equal("translate(5px, -5px) scale(1.25)", transform);
    }
}

public class ImageViewport_Immutability_Tests
{
    [Fact]
    public void ZoomIn_ReturnsNewInstance()
    {
        // Arrange
        var viewport = new ImageViewport();

        // Act
        var result = viewport.ZoomIn();

        // Assert
        Assert.NotSame(viewport, result);
        Assert.Equal(1, viewport.Scale); // Original unchanged
        Assert.Equal(1.25, result.Scale);
    }

    [Fact]
    public void PanBy_ReturnsNewInstance()
    {
        // Arrange
        var viewport = new ImageViewport { Scale = 2 };

        // Act
        var result = viewport.PanBy(10, 20);

        // Assert
        Assert.NotSame(viewport, result);
        Assert.Equal(0, viewport.OffsetX); // Original unchanged
        Assert.Equal(10, result.OffsetX);
    }
}
