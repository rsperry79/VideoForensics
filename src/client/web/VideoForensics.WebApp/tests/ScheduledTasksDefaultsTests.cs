using Microsoft.Extensions.Configuration;

using Xunit;

namespace VideoForensics.WebApp.Tests
{
    /// <summary>
    /// Guards the shipped scheduler default. An empty <c>ScheduledTasks:EnabledProviders</c> makes
    /// AddVideoForensicsScheduledSync register nothing, so no scheduled sync ever runs.
    /// </summary>
    public class ScheduledTasksDefaultsTests
    {
        [Fact]
        public void ShippedAppSettings_ScheduledTasks_EnablesRing()
        {
            string path = FindShippedAppSettings();
            IConfiguration config = new ConfigurationBuilder().AddJsonFile(path).Build();

            string[] enabled = config.GetSection("ScheduledTasks:EnabledProviders").Get<string[]>() ?? [];

            Assert.Contains("Ring", enabled);
        }

        private static string FindShippedAppSettings()
        {
            // Test binaries run from bin/, so walk up until the WebApp source folder is found.
            DirectoryInfo? dir = new(AppContext.BaseDirectory);
            while (dir is not null)
            {
                string candidate = Path.Combine(dir.FullName, "src", "appsettings.json");
                if (File.Exists(candidate) && dir.Name == "VideoForensics.WebApp")
                {
                    return candidate;
                }

                dir = dir.Parent;
            }

            throw new FileNotFoundException("VideoForensics.WebApp/src/appsettings.json not found above the test output folder.");
        }
    }
}
