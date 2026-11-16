using VideoForensics.Client.WebApp.Services;
using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Services;

public class BackupUploadStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "vf-upload-tests-" + Guid.NewGuid());
    private readonly Guid _operator = Guid.NewGuid();

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Create_ReturnsIdAndSessionDirectory()
    {
        var store = new BackupUploadStore(_root);

        Guid id = store.Create(_operator);

        Assert.NotEqual(Guid.Empty, id);
        Assert.True(Directory.Exists(Path.Combine(_root, id.ToString())));
    }

    [Fact]
    public void WriteChunk_RejectsChunkLargerThanOneMegabyte()
    {
        var store = new BackupUploadStore(_root);
        Guid id = store.Create(_operator);
        var tooBig = new MemoryStream(new byte[BackupUploadStore.MaxChunkBytes + 1]);

        Assert.Throws<ArgumentException>(() => store.WriteChunk(id, _operator, 0, tooBig));
    }

    [Fact]
    public void WriteChunk_RejectsAnotherOperator()
    {
        var store = new BackupUploadStore(_root);
        Guid id = store.Create(_operator);

        Assert.Throws<UnauthorizedAccessException>(() => store.WriteChunk(id, Guid.NewGuid(), 0, new MemoryStream([1, 2, 3])));
    }

    [Fact]
    public void WriteChunk_RejectsUploadOverTotalCap()
    {
        var store = new BackupUploadStore(_root, maxTotalBytes: 10);
        Guid id = store.Create(_operator);
        store.WriteChunk(id, _operator, 0, new MemoryStream(new byte[8]));

        Assert.Throws<InvalidOperationException>(() => store.WriteChunk(id, _operator, 1, new MemoryStream(new byte[8])));
    }

    [Fact]
    public void Assemble_ConcatenatesChunksInIndexOrder()
    {
        var store = new BackupUploadStore(_root);
        Guid id = store.Create(_operator);
        store.WriteChunk(id, _operator, 1, new MemoryStream([4, 5, 6]));
        store.WriteChunk(id, _operator, 0, new MemoryStream([1, 2, 3]));

        string path = store.Assemble(id, _operator, chunkCount: 2);

        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, File.ReadAllBytes(path));
    }

    [Fact]
    public void Assemble_MissingChunk_Throws()
    {
        var store = new BackupUploadStore(_root);
        Guid id = store.Create(_operator);
        store.WriteChunk(id, _operator, 0, new MemoryStream([1]));

        Assert.Throws<InvalidOperationException>(() => store.Assemble(id, _operator, chunkCount: 2));
    }

    [Fact]
    public void Delete_RemovesSessionDirectory()
    {
        var store = new BackupUploadStore(_root);
        Guid id = store.Create(_operator);

        store.Delete(id, _operator);

        Assert.False(Directory.Exists(Path.Combine(_root, id.ToString())));
    }
}
