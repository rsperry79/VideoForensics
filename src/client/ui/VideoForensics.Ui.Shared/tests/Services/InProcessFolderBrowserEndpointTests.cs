using Moq;

using System.Net.Http.Json;

using VideoForensics.Client.Ui.Shared.Services;
using VideoForensics.Core.Api.Contracts;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Services;

public class InProcessFolderBrowserEndpoint_Tests
{
    [Fact]
    public async Task InProcessFolderBrowserEndpoint_PostRead_ReturnsRootsOverJson()
    {
        Mock<IDirectoryBrowserService> browser = new();
        _ = browser.Setup(b => b.GetRoots()).Returns(new[] { new DirectoryEntry("C:\\", "C:\\") });
        using InProcessFolderBrowserEndpoint endpoint = new(browser.Object);

        HttpResponseMessage http = await endpoint.Client.PostAsJsonAsync(endpoint.Url, new FolderBrowseRequestDto("read", "/", null));

        _ = http.EnsureSuccessStatusCode();
        FolderBrowseResponseDto? body = await http.Content.ReadFromJsonAsync<FolderBrowseResponseDto>();
        Assert.NotNull(body);
        Assert.Equal("C:\\", Assert.Single(body!.Files).Name);
    }

    [Fact]
    public async Task InProcessFolderBrowserEndpoint_BrowserThrows_ReturnsErrorInsteadOfThrowing()
    {
        // A throw here would escape into the Blazor circuit and end the user's session.
        Mock<IDirectoryBrowserService> browser = new();
        _ = browser.Setup(b => b.GetRoots()).Throws(new IOException("drive not ready"));
        using InProcessFolderBrowserEndpoint endpoint = new(browser.Object);

        HttpResponseMessage http = await endpoint.Client.PostAsJsonAsync(endpoint.Url, new FolderBrowseRequestDto("read", "/C:/", null));

        _ = http.EnsureSuccessStatusCode();
        FolderBrowseResponseDto? body = await http.Content.ReadFromJsonAsync<FolderBrowseResponseDto>();
        Assert.Equal("browse-failed", body?.Error?.Code);
    }

    [Fact]
    public async Task InProcessFolderBrowserEndpoint_PostWithoutBody_ReturnsNotSupported()
    {
        Mock<IDirectoryBrowserService> browser = new();
        _ = browser.Setup(b => b.GetRoots()).Returns(Array.Empty<DirectoryEntry>());
        using InProcessFolderBrowserEndpoint endpoint = new(browser.Object);

        HttpResponseMessage http = await endpoint.Client.PostAsync(endpoint.Url, null);

        FolderBrowseResponseDto? body = await http.Content.ReadFromJsonAsync<FolderBrowseResponseDto>();
        Assert.Equal("not-supported", body?.Error?.Code);
    }
}
