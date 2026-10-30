using VideoForensics.Client.Ui.Shared.Services;
using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Client.WebApp.Api
{
    /// <summary>
    /// Folder-only file manager endpoint used by the folder picker (storage relocation and import's media root).
    /// Serves the Syncfusion FileManager protocol through <see cref="FolderBrowseProtocol"/>, which allows only
    /// listing and creating folders. The web host answers the same requests in-process, and MAUI calls this route.
    /// </summary>
    public static class FolderBrowseEndpoints
    {
        public static void MapFolderBrowseEndpoints(this WebApplication app)
        {
            RouteGroupBuilder group = app.MapGroup("/api/v1/filesystem").RequireAuthorization(VideoForensicsPolicies.SuperAdminLocalOrPasskey);

            _ = group.MapPost("/fileoperations", FileOperations)
                .WithSummary("Folder selection file operations")
                .WithDescription("Lists child folders (read) or creates a folder (create) for the folder picker. Every other file operation is rejected.")
                .RequireRateLimiting("media");
        }

        private static IResult FileOperations(FolderBrowseRequestDto request, IDirectoryBrowserService browser)
        {
            FolderBrowseResponseDto response = FolderBrowseProtocol.Handle(request, browser);
            return Results.Ok(response);
        }
    }
}
