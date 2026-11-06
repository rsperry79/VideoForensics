using System;
using System.IO;

using VideoForensics.Core.Helpers.Platform;

using Xunit;

namespace VideoForensics.Core.Helpers.Tests.Platform
{
    [Xunit.Collection("storage-static")]
    public class DatabaseLocationPointerStore_Tests : IDisposable
    {
        private readonly IDatabaseLocationPointerStore _previous = DatabaseLocationPointerStore.Current;
        private readonly string _dataRoot = Path.Combine(Path.GetTempPath(), "vf-dbpointer-store-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            DatabaseLocationPointerStore.Current = _previous;
            if (Directory.Exists(_dataRoot))
            {
                Directory.Delete(_dataRoot, recursive: true);
            }
        }

        [Fact]
        public void FileStore_WriteThenRead_ReturnsSameRoot()
        {
            var store = new FileDatabaseLocationPointerStore(_dataRoot);

            store.Write(@"D:\Relocated\Database");

            Assert.Equal(@"D:\Relocated\Database", store.Read());
        }

        [Fact]
        public void StorageLocationProvider_CurrentStoreHasPointer_ResolvesDatabaseFromPointer()
        {
            DatabaseLocationPointerStore.Current = new FixedPointerStore(@"E:\Pointed\Database");

            string root = new StorageLocationProvider().GetDefaultRoot(StorageCategory.Database);

            Assert.Equal(@"E:\Pointed\Database", root);
        }

        private sealed class FixedPointerStore(string? root) : IDatabaseLocationPointerStore
        {
            public string? Read()
            {
                return root;
            }

            public void Write(string databaseRoot)
            {
            }
        }
    }
}
