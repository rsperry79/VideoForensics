using System.IO;

namespace Ring.Api
{
    public class DownloadHelper
    {
        public DownloadHelper()
        {

        }

        /// <summary>
        /// Returns true if a file already exists at expectedFileName and, when expectedSize is
        /// known, its length matches expectedSize.
        /// </summary>
        public bool ValidateMediaExists(string expectedFileName, long? expectedSize)
        {
            if (string.IsNullOrEmpty(expectedFileName) || !File.Exists(expectedFileName))
                return false;

            if (expectedSize.HasValue)
                return new FileInfo(expectedFileName).Length == expectedSize.Value;

            return new FileInfo(expectedFileName).Length > 0;
        }
    }
}
