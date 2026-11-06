using System;
using System.IO;

namespace VideoForensics.Core.Helpers.Platform
{
    /// <summary>
    /// Where the bootstrap pointer to the Database folder is kept. The Database location is also saved in the database, but
    /// startup has to find the database before it can read anything, so this pointer must live outside it. Windows keeps it
    /// in the registry; other platforms keep it in a file under the data root.
    /// </summary>
    public interface IDatabaseLocationPointerStore
    {
        /// <summary>Returns the recorded Database root, or null if there is no usable pointer.</summary>
        string? Read();

        /// <summary>Records the Database root.</summary>
        void Write(string databaseRoot);
    }

    /// <summary>Keeps the Database pointer in <c>database-location.json</c> under a data root.</summary>
    public sealed class FileDatabaseLocationPointerStore(string dataRoot) : IDatabaseLocationPointerStore
    {
        /// <inheritdoc />
        public string? Read()
        {
            return DatabaseLocationPointer.Read(dataRoot);
        }

        /// <inheritdoc />
        public void Write(string databaseRoot)
        {
            DatabaseLocationPointer.Write(dataRoot, databaseRoot);
        }
    }

    /// <summary>The pointer store the running host uses. Hosts on Windows replace the default with the registry store at startup.</summary>
    public static class DatabaseLocationPointerStore
    {
        /// <summary>The fixed machine data root, the same folder the storage provider uses before any pointer is read.</summary>
        private static string DefaultDataRoot => OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "VideoForensics")
            : "/var/lib/videoforensics";

        /// <summary>The active pointer store.</summary>
        public static IDatabaseLocationPointerStore Current { get; set; } = new FileDatabaseLocationPointerStore(DefaultDataRoot);
    }
}
