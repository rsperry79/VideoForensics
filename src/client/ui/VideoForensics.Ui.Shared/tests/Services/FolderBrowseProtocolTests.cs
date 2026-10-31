using Moq;

using VideoForensics.Client.Ui.Shared.Services;
using VideoForensics.Core.Api.Contracts;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Services;

public class FolderBrowseProtocol_Tests
{
    private static readonly DirectoryEntry DriveC = new("C:\\", "C:\\");
    private static readonly DirectoryEntry DriveD = new("D:\\", "D:\\");
    private static readonly DirectoryEntry Videos = new("Videos", "C:\\Videos");

    private readonly Mock<IDirectoryBrowserService> _browser = new();

    public FolderBrowseProtocol_Tests()
    {
        _ = _browser.Setup(b => b.GetRoots()).Returns(new[] { DriveC, DriveD });
        _ = _browser.Setup(b => b.GetSubdirectories("C:\\")).Returns(new[] { Videos });
        _ = _browser.Setup(b => b.GetSubdirectories("C:\\Videos")).Returns(Array.Empty<DirectoryEntry>());
    }

    [Fact]
    public void FolderBrowseProtocol_ReadVirtualRoot_ListsRootsWithoutCwd()
    {
        FolderBrowseResponseDto response = FolderBrowseProtocol.Handle(new FolderBrowseRequestDto("read", "/", null), _browser.Object);

        Assert.Null(response.Error);
        Assert.Equal(new[] { "C:\\", "D:\\" }, response.Files.Select(f => f.Name));
        Assert.Equal("C:\\", response.Files[0].Id);
        Assert.All(response.Files, f => Assert.False(f.IsFile));
    }

    [Fact]
    public void FolderBrowseProtocol_ReadDrive_ListsChildFoldersWithAbsoluteIds()
    {
        FolderBrowseResponseDto response = FolderBrowseProtocol.Handle(new FolderBrowseRequestDto("read", "/C:\\/", null), _browser.Object);

        Assert.Null(response.Error);
        FolderBrowseEntryDto child = Assert.Single(response.Files);
        Assert.Equal("Videos", child.Name);
        Assert.Equal("/C:/", child.FilterPath);
        Assert.Equal("C:\\Videos", child.Id);
    }

    [Fact]
    public void FolderBrowseProtocol_ReadNestedFolder_ResolvesAbsoluteCwd()
    {
        FolderBrowseResponseDto response = FolderBrowseProtocol.Handle(new FolderBrowseRequestDto("read", "/C:/Videos/", null), _browser.Object);

        Assert.Null(response.Error);
        Assert.NotNull(response.Cwd);
        Assert.Equal("Videos", response.Cwd!.Name);
        Assert.Equal("C:\\Videos", response.Cwd.Id);
        Assert.Equal("/C:/", response.Cwd.FilterPath);
    }

    [Fact]
    public void FolderBrowseProtocol_ReadUnknownRoot_ReturnsInvalidPath()
    {
        FolderBrowseResponseDto response = FolderBrowseProtocol.Handle(new FolderBrowseRequestDto("read", "/Z:/", null), _browser.Object);

        Assert.Equal("invalid-path", response.Error?.Code);
        Assert.Empty(response.Files);
    }

    [Fact]
    public void FolderBrowseProtocol_ReadWithParentSegment_IsRejectedWithoutListing()
    {
        FolderBrowseResponseDto response = FolderBrowseProtocol.Handle(new FolderBrowseRequestDto("read", "/C:/../Windows/", null), _browser.Object);

        Assert.Equal("invalid-path", response.Error?.Code);
        _browser.Verify(b => b.GetSubdirectories(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void FolderBrowseProtocol_CreateInsideDrive_ReturnsNewFolder()
    {
        _ = _browser.Setup(b => b.CreateSubdirectory("C:\\", "Backups")).Returns("C:\\Backups");

        FolderBrowseResponseDto response = FolderBrowseProtocol.Handle(new FolderBrowseRequestDto("create", "/C:/", "Backups"), _browser.Object);

        Assert.Null(response.Error);
        FolderBrowseEntryDto created = Assert.Single(response.Files);
        Assert.Equal("Backups", created.Name);
        Assert.Equal("C:\\Backups", created.Id);
    }

    [Fact]
    public void FolderBrowseProtocol_CreateAtVirtualRoot_IsRejectedWithoutCreating()
    {
        FolderBrowseResponseDto response = FolderBrowseProtocol.Handle(new FolderBrowseRequestDto("create", "/", "Backups"), _browser.Object);

        Assert.Equal("invalid-path", response.Error?.Code);
        _browser.Verify(b => b.CreateSubdirectory(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void FolderBrowseProtocol_CreateWithInvalidName_ReturnsCreateFailed()
    {
        _ = _browser.Setup(b => b.CreateSubdirectory("C:\\", "Bad*Name")).Returns((string?)null);

        FolderBrowseResponseDto response = FolderBrowseProtocol.Handle(new FolderBrowseRequestDto("create", "/C:/", "Bad*Name"), _browser.Object);

        Assert.Equal("create-failed", response.Error?.Code);
    }

    [Theory]
    [InlineData("delete")]
    [InlineData("rename")]
    [InlineData("copy")]
    [InlineData("move")]
    [InlineData("download")]
    [InlineData("upload")]
    public void FolderBrowseProtocol_FileOperation_IsRejectedAsNotSupported(string action)
    {
        FolderBrowseResponseDto response = FolderBrowseProtocol.Handle(new FolderBrowseRequestDto(action, "/C:/Videos/", "x"), _browser.Object);

        Assert.Equal("not-supported", response.Error?.Code);
        _browser.Verify(b => b.GetSubdirectories(It.IsAny<string>()), Times.Never);
        _browser.Verify(b => b.CreateSubdirectory(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
