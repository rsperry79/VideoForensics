using VideoForensics.Core.Helpers.Platform;
using VideoForensics.Data.Core.Services;

using Xunit;

namespace VideoForensics.Data.Core.Tests
{
    public class KeysLocationPinnerTests : IDisposable
    {
        private readonly string _dataRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        private readonly string _defaultKeys = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "Keys");

        public void Dispose()
        {
            foreach (string dir in new[] { _dataRoot, Path.GetDirectoryName(_defaultKeys)! })
            {
                try
                { Directory.Delete(dir, recursive: true); }
                catch { }
            }
        }

        [Fact]
        public void Pin_KeysStillOnTheDataRoot_PinsThemThereBeforeTheDefaultChanges()
        {
            string dataKeys = Path.Combine(_dataRoot, "Keys");
            _ = Directory.CreateDirectory(dataKeys);
            File.WriteAllText(Path.Combine(dataKeys, "master.key"), "secret");

            KeysLocationPinner.Pin(_dataRoot, _defaultKeys);

            Assert.Equal(dataKeys, KeysLocationPointer.Read(_dataRoot));
        }

        [Fact]
        public void Pin_PointerAlreadyExists_LeavesItAlone()
        {
            string dataKeys = Path.Combine(_dataRoot, "Keys");
            _ = Directory.CreateDirectory(dataKeys);
            File.WriteAllText(Path.Combine(dataKeys, "master.key"), "secret");
            KeysLocationPointer.Write(_dataRoot, @"D:\Keys");

            KeysLocationPinner.Pin(_dataRoot, _defaultKeys);

            Assert.Equal(@"D:\Keys", KeysLocationPointer.Read(_dataRoot));
        }

        [Fact]
        public void Pin_NoKeysOnTheDataRoot_WritesNoPointer()
        {
            KeysLocationPinner.Pin(_dataRoot, _defaultKeys);

            Assert.Null(KeysLocationPointer.Read(_dataRoot));
        }
    }
}
