namespace VideoForensics.Client.WebApp.Services;

/// <summary>
/// Holds the chunks of one backup upload until the client asks for them to be assembled. Each session is a
/// folder under the store root, owned by the operator who created it. Chunks are at most
/// <see cref="MaxChunkBytes"/> each, and a session may not exceed the total cap.
/// </summary>
public sealed class BackupUploadStore
{
    /// <summary>Largest single chunk the client may send, in bytes.</summary>
    public const int MaxChunkBytes = 1024 * 1024;

    /// <summary>Largest total backup the store accepts, in bytes (4 GB).</summary>
    public const long DefaultMaxTotalBytes = 4L * 1024 * 1024 * 1024;

    private const string OwnerFile = "owner.txt";
    private const string AssembledFile = "backup.zip";

    private readonly string _root;
    private readonly long _maxTotalBytes;

    public BackupUploadStore(string rootDirectory, long maxTotalBytes = DefaultMaxTotalBytes)
    {
        _root = rootDirectory;
        _maxTotalBytes = maxTotalBytes;
    }

    /// <summary>Starts a new upload owned by <paramref name="operatorId"/> and returns its id.</summary>
    public Guid Create(Guid operatorId)
    {
        Guid id = Guid.NewGuid();
        string dir = SessionDirectory(id);
        _ = Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, OwnerFile), operatorId.ToString());
        return id;
    }

    /// <summary>Stores one chunk. Rewriting the same index replaces the earlier copy.</summary>
    public void WriteChunk(Guid id, Guid operatorId, int index, Stream data)
    {
        string dir = RequireOwnedSession(id, operatorId);
        if (index < 0)
        {
            throw new ArgumentException("Chunk index must not be negative.", nameof(index));
        }

        byte[] buffer = new byte[MaxChunkBytes + 1];
        int length = ReadUpTo(data, buffer);
        if (length > MaxChunkBytes)
        {
            throw new ArgumentException($"A chunk may not be larger than {MaxChunkBytes} bytes.", nameof(data));
        }

        string chunkPath = ChunkPath(dir, index);
        long existing = File.Exists(chunkPath) ? new FileInfo(chunkPath).Length : 0;
        long total = TotalBytes(dir) - existing + length;
        if (total > _maxTotalBytes)
        {
            throw new InvalidOperationException($"The backup upload may not exceed {_maxTotalBytes} bytes.");
        }

        File.WriteAllBytes(chunkPath, buffer.AsSpan(0, length).ToArray());
    }

    /// <summary>Joins chunks 0 to <paramref name="chunkCount"/> - 1 into one file and returns its path.</summary>
    public string Assemble(Guid id, Guid operatorId, int chunkCount)
    {
        string dir = RequireOwnedSession(id, operatorId);
        string target = Path.Combine(dir, AssembledFile);
        using FileStream output = File.Create(target);
        for (int i = 0; i < chunkCount; i++)
        {
            string chunkPath = ChunkPath(dir, i);
            if (!File.Exists(chunkPath))
            {
                throw new InvalidOperationException($"Chunk {i} of {chunkCount} is missing.");
            }

            using FileStream input = File.OpenRead(chunkPath);
            input.CopyTo(output);
        }

        return target;
    }

    /// <summary>Removes the upload and its chunks.</summary>
    public void Delete(Guid id, Guid operatorId)
    {
        string dir = RequireOwnedSession(id, operatorId);
        Directory.Delete(dir, recursive: true);
    }

    private string SessionDirectory(Guid id) => Path.Combine(_root, id.ToString());

    private static string ChunkPath(string dir, int index) => Path.Combine(dir, $"chunk-{index:D6}.bin");

    private string RequireOwnedSession(Guid id, Guid operatorId)
    {
        string dir = SessionDirectory(id);
        string ownerPath = Path.Combine(dir, OwnerFile);
        if (!File.Exists(ownerPath))
        {
            throw new KeyNotFoundException("Backup upload not found.");
        }

        if (File.ReadAllText(ownerPath) != operatorId.ToString())
        {
            throw new UnauthorizedAccessException("This backup upload belongs to another operator.");
        }

        return dir;
    }

    private static long TotalBytes(string dir)
    {
        long total = 0;
        foreach (string chunk in Directory.EnumerateFiles(dir, "chunk-*.bin"))
        {
            total += new FileInfo(chunk).Length;
        }

        return total;
    }

    private static int ReadUpTo(Stream source, byte[] buffer)
    {
        int read = 0;
        while (read < buffer.Length)
        {
            int n = source.Read(buffer, read, buffer.Length - read);
            if (n == 0)
            {
                break;
            }

            read += n;
        }

        return read;
    }
}
