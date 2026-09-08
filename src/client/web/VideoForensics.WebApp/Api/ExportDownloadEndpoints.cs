namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// Streams a backup-export archive to the browser as a file download, resolved via a short-lived,
    /// single-use opaque token (see ExportDownloadTokenStore) rather than accepting a raw filesystem path
    /// from the client. Unauthenticated by design: the token itself is unguessable, single-use, and
    /// expires quickly, and this only ever serves a file the export step just wrote for the same local
    /// operator - same rationale as MediaApiEndpoints' current pairing-only auth model.
    /// </summary>
    public static class ExportDownloadEndpoints
    {
        public static void MapExportDownloadEndpoints(this WebApplication app)
        {
            _ = app.MapGet("/api/export-download/{token:guid}", (
                Guid token,
                string? fileName,
                VideoForensics.WebApp.Services.ExportDownloadTokenStore tokenStore) =>
            {
                var filePath = tokenStore.TryConsume(token);
                if (filePath is null || !File.Exists(filePath))
                {
                    return Results.NotFound();
                }

                var downloadName = string.IsNullOrWhiteSpace(fileName) ? Path.GetFileName(filePath) : fileName;
                return Results.File(filePath, "application/octet-stream", downloadName);
            });
        }
    }
}
