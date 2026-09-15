using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using VideoForensics.Forensics.Interfaces;
using VideoForensics.Forensics.Models;

namespace VideoForensics.Forensics.Implementations
{
    /// <summary>
    /// Performs forensic analysis on extracted evidence, detecting patterns,
    /// anomalies, and generating investigative reports.
    /// </summary>
    internal class ForensicAnalyzer : IForensicAnalyzer
    {
        /// <summary>
        /// Analyzes evidence for common forensic indicators.
        /// </summary>
        public Task<ForensicAnalysisResult> AnalyzeEvidenceAsync(EvidenceMetadata evidence)
        {
            if (evidence == null)
            {
                throw new ArgumentNullException(nameof(evidence));
            }

            var result = new ForensicAnalysisResult
            {
                EvidenceId = evidence.EvidenceId,
                Severity = AnalysisSeverity.Info,
                Tags = []
            };

            // Analyze event type for common forensic indicators
            if (!string.IsNullOrEmpty(evidence.EventType))
            {
                result.Tags.Add(evidence.EventType);

                // Simple heuristics based on event type
                if (evidence.EventType.Contains("motion", StringComparison.OrdinalIgnoreCase))
                {
                    result.Finding = "Motion event detected";
                    result.Recommendation = "Review video timeline for activity correlation";
                }
                else if (evidence.EventType.Contains("tampering", StringComparison.OrdinalIgnoreCase) ||
                         evidence.EventType.Contains("alarm", StringComparison.OrdinalIgnoreCase))
                {
                    result.Finding = "Suspicious event type detected";
                    result.Severity = AnalysisSeverity.Warning;
                    result.Recommendation = "Investigate device security and access logs";
                }
                else if (evidence.EventType.Contains("critical", StringComparison.OrdinalIgnoreCase))
                {
                    result.Finding = "Critical event detected";
                    result.Severity = AnalysisSeverity.Critical;
                    result.Recommendation = "Immediate review required";
                }
                else
                {
                    result.Finding = $"Event of type '{evidence.EventType}' recorded";
                }
            }
            else
            {
                result.Finding = "No event type specified";
            }

            // Record extracted data metadata
            if (evidence.ExtractedData != null && evidence.ExtractedData.Count > 0)
            {
                result.Tags.Add("has-extracted-data");
                result.AnalysisData["extracted-fields-count"] = evidence.ExtractedData.Count;
            }

            // Check for integrity data
            if (evidence.Checksums != null && evidence.Checksums.Count > 0)
            {
                result.Tags.Add("integrity-verified");
                result.AnalysisData["checksum-types"] = string.Join(",", evidence.Checksums.Keys);
            }

            return Task.FromResult(result);
        }

        /// <summary>
        /// Detects temporal anomalies or patterns in a sequence of events.
        /// </summary>
        public Task<IEnumerable<ForensicAnalysisResult>> DetectAnomaliesAsync(
            IEnumerable<EvidenceMetadata> evidenceSequence)
        {
            if (evidenceSequence == null)
            {
                throw new ArgumentNullException(nameof(evidenceSequence));
            }

            var results = new List<ForensicAnalysisResult>();
            var items = evidenceSequence.ToList();

            if (items.Count < 2)
            {
                // Need at least 2 items to detect patterns
                return Task.FromResult((IEnumerable<ForensicAnalysisResult>)results);
            }

            // Detect temporal clustering (events within close time windows)
            var sortedByTime = items.OrderBy(e => e.EventTimestamp ?? DateTime.MinValue).ToList();

            for (int i = 0; i < sortedByTime.Count - 1; i++)
            {
                var current = sortedByTime[i];
                var next = sortedByTime[i + 1];

                if (current.EventTimestamp.HasValue && next.EventTimestamp.HasValue)
                {
                    var timeGap = next.EventTimestamp.Value - current.EventTimestamp.Value;

                    // Detect rapid succession (< 5 seconds)
                    if (timeGap.TotalSeconds > 0 && timeGap.TotalSeconds < 5)
                    {
                        var anomaly = new ForensicAnalysisResult
                        {
                            Finding = $"Rapid event sequence detected: {timeGap.TotalSeconds:F1}s gap",
                            Severity = AnalysisSeverity.Warning,
                            Tags = ["temporal-anomaly", "rapid-succession"],
                            Recommendation = "Review for coordinated activity or device manipulation",
                            AnalysisData = new Dictionary<string, object>
                            {
                                ["gap-seconds"] = timeGap.TotalSeconds,
                                ["event-indices"] = new[] { i, i + 1 }
                            }
                        };
                        results.Add(anomaly);
                    }
                }
            }

            // Detect event type clustering
            var eventTypeGroups = items.GroupBy(e => e.EventType ?? "unknown")
                .Where(g => g.Count() > 2)
                .ToList();

            foreach (var group in eventTypeGroups)
            {
                var anomaly = new ForensicAnalysisResult
                {
                    Finding = $"Multiple events of type '{group.Key}': {group.Count()} occurrences",
                    Severity = AnalysisSeverity.Info,
                    Tags = ["event-clustering", group.Key],
                    Recommendation = "Verify if pattern indicates normal operation or suspicious activity",
                    AnalysisData = new Dictionary<string, object>
                    {
                        ["event-type"] = group.Key,
                        ["occurrence-count"] = group.Count()
                    }
                };
                results.Add(anomaly);
            }

            return Task.FromResult((IEnumerable<ForensicAnalysisResult>)results);
        }

        /// <summary>
        /// Retrieves a strongly-typed forensic analysis report from analyzed evidence.
        /// </summary>
        public Task<ForensicAnalysisReport> GetAnalysisReportAsync(
            IEnumerable<ForensicAnalysisResult> analysisResults,
            string? analysisType = null)
        {
            if (analysisResults == null)
            {
                throw new ArgumentNullException(nameof(analysisResults));
            }

            var resultList = analysisResults.ToList();

            var report = new ForensicAnalysisReport
            {
                AnalysisType = analysisType,
                Findings = resultList,
                GeneratedAt = DateTime.UtcNow
            };

            // Generate summary based on findings
            if (resultList.Count == 0)
            {
                report.Summary = "No findings identified in analysis";
            }
            else
            {
                var criticalCount = resultList.Count(r => r.Severity == AnalysisSeverity.Critical);
                var warningCount = resultList.Count(r => r.Severity == AnalysisSeverity.Warning);
                var infoCount = resultList.Count(r => r.Severity == AnalysisSeverity.Info);

                report.Summary = $"Identified {resultList.Count} findings: " +
                    $"{criticalCount} critical, {warningCount} warnings, {infoCount} informational";
            }

            // Populate metadata
            report.Metadata["total-findings"] = resultList.Count;
            report.Metadata["critical-findings"] = resultList.Count(r => r.Severity == AnalysisSeverity.Critical);
            report.Metadata["warning-findings"] = resultList.Count(r => r.Severity == AnalysisSeverity.Warning);

            return Task.FromResult(report);
        }
    }
}
