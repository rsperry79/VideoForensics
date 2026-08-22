using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Forensics.Models;

namespace VideoForensics.Providers.Ring.Forensics
{
    /// <summary>
    /// Defines operations for extracting forensic evidence from Ring device data.
    /// Handles extraction of metadata, events, and device information suitable for
    /// forensic analysis and legal proceedings.
    /// </summary>
    public interface IEvidenceExtractor
    {
        /// <summary>
        /// Extracts all available evidence from a device event.
        /// </summary>
        /// <param name="event">The device event to analyze.</param>
        /// <returns>Extracted evidence metadata and artifacts.</returns>
        Task<EvidenceMetadata> ExtractEvidenceAsync(DoorbotHistoryEvent @event);

        /// <summary>
        /// Extracts time-series evidence spanning multiple events.
        /// </summary>
        /// <param name="events">Events to analyze in sequence.</param>
        /// <param name="options">Optional configuration for extraction.</param>
        /// <returns>Aggregated evidence with temporal relationships.</returns>
        Task<IEnumerable<EvidenceMetadata>> ExtractEvidenceTimeSeriesAsync(
            IEnumerable<DoorbotHistoryEvent> events,
            ForensicsOptions? options = null);

        /// <summary>
        /// Validates that extracted evidence maintains data integrity.
        /// </summary>
        /// <returns>Validation status and any integrity issues found.</returns>
        EvidenceIntegrityStatus ValidateExtraction();
    }
}
