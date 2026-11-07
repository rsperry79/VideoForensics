using VideoForensics.Client.Hosting.BackgroundServices;

using Xunit;

namespace VideoForensics.Client.Hosting.Tests.BackgroundServices
{
    /// <summary>
    /// Startup cleanup of partial time-lapse files: only "*.building.mp4" exactly three folders below the media root,
    /// and only when older than the process start, so a build running in this process is never touched.
    /// </summary>
    public sealed class BuildingFileCleanupTests : IDisposable
    {
        private static readonly DateTime ProcessStart = new(2026, 11, 6, 10, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime Earlier = ProcessStart.AddHours(-2);

        private readonly string _root = Path.Combine(Path.GetTempPath(), "vf-cleanup-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        private string CreateFile(DateTime lastWriteUtc, params string[] relativeParts)
        {
            string path = Path.Combine([_root, .. relativeParts]);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "x");
            File.SetLastWriteTimeUtc(path, lastWriteUtc);
            return path;
        }

        [Fact]
        public void BuildingFileCleanup_OldBuildingFileAtDepthThree_DeletesOnlyThatFile()
        {
            string partial = CreateFile(Earlier, "ring", "loc", "dev", "clip.building.mp4");
            string finished = CreateFile(Earlier, "ring", "loc", "dev", "clip.mp4");
            string image = CreateFile(Earlier, "ring", "loc", "dev", "still.jpg");

            int deleted = BuildingFileCleanup.Delete(_root, ProcessStart);

            Assert.Equal(1, deleted);
            Assert.False(File.Exists(partial));
            Assert.True(File.Exists(finished));
            Assert.True(File.Exists(image));
        }

        [Fact]
        public void BuildingFileCleanup_BuildingFileAtDepthTwoOrFour_LeavesIt()
        {
            string shallow = CreateFile(Earlier, "ring", "loc", "clip.building.mp4");
            string deep = CreateFile(Earlier, "ring", "loc", "dev", "extra", "clip.building.mp4");

            int deleted = BuildingFileCleanup.Delete(_root, ProcessStart);

            Assert.Equal(0, deleted);
            Assert.True(File.Exists(shallow));
            Assert.True(File.Exists(deep));
        }

        [Fact]
        public void BuildingFileCleanup_BuildingFileWrittenAfterProcessStart_LeavesIt()
        {
            string live = CreateFile(ProcessStart.AddMinutes(5), "ring", "loc", "dev", "clip.building.mp4");

            int deleted = BuildingFileCleanup.Delete(_root, ProcessStart);

            Assert.Equal(0, deleted);
            Assert.True(File.Exists(live));
        }

        [Fact]
        public void BuildingFileCleanup_MissingRoot_ReturnsZero()
        {
            int deleted = BuildingFileCleanup.Delete(_root, ProcessStart);

            Assert.Equal(0, deleted);
        }
    }
}
