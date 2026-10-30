using VideoForensics.Client.Hosting.Contracts;
using VideoForensics.Client.Hosting.Services;

namespace VideoForensics.Client.Hosting.Tests
{
    public class SyncProgressStoreTests
    {
        [Fact]
        public void SyncProgressStore_NewStore_CurrentIsIdle()
        {
            ISyncProgressStore store = new SyncProgressStore();

            Assert.Equal(SyncProgress.Idle, store.Current);
        }

        [Fact]
        public void SyncProgressStore_SetThenCurrent_ReturnsSetValue()
        {
            ISyncProgressStore store = new SyncProgressStore();
            var progress = new SyncProgress(true, 5, 3, 2048L, "a.mp4", 2, 1, 4, "Garage");

            store.Set(progress);

            Assert.Equal(progress, store.Current);
        }

        [Fact]
        public void SyncProgressStore_CurrentRead_DoesNotConsumeValue()
        {
            ISyncProgressStore store = new SyncProgressStore();
            var progress = new SyncProgress(true, 5, 3, 2048L, "a.mp4", 2, 1, 4, "Garage");
            store.Set(progress);

            _ = store.Current;

            Assert.Equal(progress, store.Current);
        }

        [Fact]
        public void SyncProgressStore_Clear_CurrentIsIdle()
        {
            ISyncProgressStore store = new SyncProgressStore();
            store.Set(new SyncProgress(true, 5, 3, 2048L, "a.mp4", 2, 1, 4, "Garage"));

            store.Clear();

            Assert.Equal(SyncProgress.Idle, store.Current);
        }

        [Fact]
        public void SyncProgressStore_ConcurrentSetAndCurrent_NeverThrowsOrTears()
        {
            ISyncProgressStore store = new SyncProgressStore();

            // Each written value is internally consistent (FilesCompleted == DeviceIndex). A torn read would break that.
            Parallel.For(0, 2000, i =>
            {
                if (i % 2 == 0)
                {
                    store.Set(new SyncProgress(true, 10, i, i, "f.mp4", 1, i, 10, "Dev"));
                }
                else
                {
                    SyncProgress read = store.Current;
                    Assert.Equal(read.FilesCompleted, read.DeviceIndex);
                    Assert.Equal(read.FilesCompleted, (int)read.BytesDownloaded);
                }
            });
        }
    }
}
