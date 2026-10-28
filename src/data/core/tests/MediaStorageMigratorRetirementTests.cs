using System.Reflection;

using VideoForensics.Data.Core.DependencyInjection;

using Xunit;

namespace VideoForensics.Data.Core.Tests
{
    /// <summary>
    /// Guards the retirement of MediaStorageMigrator. It moved media out of the user's chosen Media folder
    /// (IForensicsConfiguration.DownloadLocation) on every startup, so it must not return.
    /// </summary>
    public class MediaStorageMigratorRetirementTests
    {
        [Fact]
        public void MediaStorageMigrator_Type_IsRetired()
        {
            Assembly assembly = typeof(ServiceCollectionExtensions).Assembly;

            var leftovers = assembly.GetTypes()
                .Where(t => t.Name == "MediaStorageMigrator")
                .Select(t => t.FullName)
                .ToList();

            Assert.Empty(leftovers);
        }
    }
}
