namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>Returned when a chunked backup upload is started.</summary>
    /// <param name="UploadId">Id to send with each chunk and with the complete call.</param>
    public sealed record BackupUploadCreatedDto(Guid UploadId);

    /// <summary>Asks the server to assemble the uploaded chunks and import them.</summary>
    /// <param name="ChunkCount">How many chunks the client sent, numbered from 0.</param>
    /// <param name="MediaRootPath">Root path where imported media files will be stored.</param>
    public sealed record CompleteBackupUploadRequest(int ChunkCount, string MediaRootPath);
}
