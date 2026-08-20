using System;
using System.Threading.Tasks;

namespace Ring.Api
{
    /// <summary>
    /// Everything <see cref="RingVideoService"/> needs to surface to a user, abstracted so this
    /// library never takes a dependency on a console/terminal rendering library. Implementations
    /// live in the host application (e.g. a console UI wrapping Spectre.Console).
    /// </summary>
    public interface IDownloadReporter
    {
        void Info(string message);
        void Warning(string message);
        void Error(string message);
        void Highlight(string message);

        /// <summary>Reserves a persistent row for one in-flight item (e.g. a download) and returns an opaque handle for it.</summary>
        object BeginItem(string initialMessage);
        /// <summary>Sets the item's prefix/label text (e.g. filename), distinct from its status - see <see cref="UpdateItem"/>.</summary>
        void WriteItem(object item, string message);
        /// <summary>Sets the item's status, shown after its prefix/label text set via <see cref="WriteItem"/>.</summary>
        void UpdateItem(object item, string message);
        void CompleteItem(object item, string message);
        void ErrorItem(object item, string message);
        void WarnItem(object item, string message);
        /// <summary>Marks an item as finished and frees its row for reuse.</summary>
        void ReleaseItem(object item);
        void UpdateFooter(string message);
        /// <summary>Hints at how many concurrent item rows are about to be needed.</summary>
        void EnsureCapacity(int expectedItemCount);
        void ClearItems();

        /// <summary>
        /// Runs <paramref name="operation"/> under a status/spinner display seeded with
        /// <paramref name="initialMessage"/>; the operation can update the displayed text via the
        /// callback it's given.
        /// </summary>
        Task<T> RunWithStatusAsync<T>(string initialMessage, Func<Func<string, Task>, Task<T>> operation);

        /// <summary>Non-generic overload of <see cref="RunWithStatusAsync{T}"/>.</summary>
        Task RunWithStatusAsync(string initialMessage, Func<Func<string, Task>, Task> operation);

        /// <summary>Prompts the user for a two-factor authentication code and returns what they entered.</summary>
        Task<string> PromptTwoFactorCodeAsync();
    }
}
