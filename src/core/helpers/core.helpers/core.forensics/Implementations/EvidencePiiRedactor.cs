using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

using VideoForensics.Forensics.Interfaces;
using VideoForensics.Forensics.Models;

namespace VideoForensics.Forensics.Implementations
{
    /// <summary>
    /// Redacts personally identifiable information (PII) from forensic reports.
    /// Protects victim and handler privacy when reports are shared.
    /// </summary>
    internal class EvidencePiiRedactor : IEvidencePiiRedactor
    {
        public Task<ChainOfCustodyReport> RedactChainOfCustodyAsync(
            ChainOfCustodyReport report,
            RedactionOptions options)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            ChainOfCustodyReport redacted = DeepClone(report);
            ApplyRedaction(redacted, options);

            return Task.FromResult(redacted);
        }

        public Task<EvidenceValidationReport> RedactValidationReportAsync(
            EvidenceValidationReport report,
            RedactionOptions options)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            EvidenceValidationReport redacted = DeepClone(report);
            ApplyRedaction(redacted, options);

            return Task.FromResult(redacted);
        }

        public Task<ForensicAnalysisReport> RedactAnalysisReportAsync(
            ForensicAnalysisReport report,
            RedactionOptions options)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            ForensicAnalysisReport redacted = DeepClone(report);
            ApplyRedaction(redacted, options);

            return Task.FromResult(redacted);
        }

        public Task<SignalAnomalyReport> RedactSignalAnomalyReportAsync(
            SignalAnomalyReport report,
            RedactionOptions options)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            SignalAnomalyReport redacted = DeepClone(report);
            ApplyRedaction(redacted, options);

            return Task.FromResult(redacted);
        }

        public Task<T> GenerateAnonymizedReportAsync<T>(T report) where T : class
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            var options = new RedactionOptions { FullyAnonymize = true };
            T redacted = DeepClone(report);
            ApplyRedaction(redacted, options);

            return Task.FromResult(redacted);
        }

        public async Task<IEnumerable<T>> RedactReportsAsync<T>(
            IEnumerable<T> reports,
            RedactionOptions options) where T : class
        {
            if (reports == null)
            {
                return Enumerable.Empty<T>();
            }

            var redactedReports = new List<T>();
            foreach (T report in reports)
            {
                T redacted = DeepClone(report);
                ApplyRedaction(redacted, options);
                redactedReports.Add(redacted);
            }

            return await Task.FromResult(redactedReports);
        }

        private void ApplyRedaction<T>(T report, RedactionOptions options) where T : class
        {
            if (report == null || options == null)
            {
                return;
            }

            string replacement = options.ReplacementString ?? "[REDACTED]";

            // If fully anonymize, redact everything
            if (options.FullyAnonymize)
            {
                RedactStringProperties(report, replacement);
                return;
            }

            // Handle specific report types with targeted redaction
            if (report is ChainOfCustodyReport custodyReport)
            {
                RedactChainOfCustodyReport(custodyReport, options, replacement);
            }
            else if (report is EvidenceValidationReport validationReport)
            {
                RedactValidationReport(validationReport, options, replacement);
            }
            else if (report is ForensicAnalysisReport analysisReport)
            {
                RedactAnalysisReport(analysisReport, options, replacement);
            }
            else if (report is SignalAnomalyReport signalReport)
            {
                RedactSignalAnomalyReport(signalReport, options, replacement);
            }
            else
            {
                // For unknown types, just redact string properties if custom fields specified
                if (options.CustomSensitiveFields.Count > 0)
                {
                    RedactCustomFields(report, options.CustomSensitiveFields, replacement);
                }
            }
        }

        private void RedactChainOfCustodyReport(
            ChainOfCustodyReport report,
            RedactionOptions options,
            string replacement)
        {
            if (options.RedactHandlerNames)
            {
                report.SignedByOfficer = RedactIfNotNull(report.SignedByOfficer, replacement);
            }

            if (options.RedactTimestamps)
            {
                report.GeneratedAt = DateTime.MinValue;
                report.EvidenceInitiallyReceived = null;
                report.EvidenceLastAccessed = null;
                report.ReportSignedAt = null;
            }

            if (options.RedactAccessLogs && report.CustodyHistory != null)
            {
                foreach (ChainOfCustodyEntry entry in report.CustodyHistory)
                {
                    if (options.RedactHandlerNames)
                    {
                        entry.Handler = RedactIfNotNull(entry.Handler, replacement);
                    }

                    if (options.RedactTimestamps)
                    {
                        entry.Timestamp = DateTime.MinValue;
                    }

                    entry.Notes = RedactIfNotNull(entry.Notes, replacement);
                    entry.AccessReason = RedactIfNotNull(entry.AccessReason, replacement);
                    entry.AccessRejectionReason = RedactIfNotNull(entry.AccessRejectionReason, replacement);
                }
            }

            RedactCustomFields(report, options.CustomSensitiveFields, replacement);
            RedactMetadata(report.Metadata, replacement);
        }

        private void RedactValidationReport(
            EvidenceValidationReport report,
            RedactionOptions options,
            string replacement)
        {
            if (options.RedactHandlerNames)
            {
                report.ValidatedBy = RedactIfNotNull(report.ValidatedBy, replacement);
                report.SignedByOfficer = RedactIfNotNull(report.SignedByOfficer, replacement);
            }

            if (options.RedactTimestamps)
            {
                report.GeneratedAt = DateTime.MinValue;
                report.ReportSignedAt = null;
            }

            if (options.RedactVictimName || options.RedactPhoneNumber ||
                options.RedactAddress || options.RedactEmail)
            {
                report.CertificationStatement = RedactIfNotNull(report.CertificationStatement, replacement);
            }

            if (report.AllErrors != null)
            {
                for (int i = 0; i < report.AllErrors.Count; i++)
                {
                    report.AllErrors[i] = RedactIfNotNull(report.AllErrors[i], replacement);
                }
            }

            if (report.AllWarnings != null)
            {
                for (int i = 0; i < report.AllWarnings.Count; i++)
                {
                    report.AllWarnings[i] = RedactIfNotNull(report.AllWarnings[i], replacement);
                }
            }

            RedactCustomFields(report, options.CustomSensitiveFields, replacement);
            RedactMetadata(report.Metadata, replacement);
        }

        private void RedactAnalysisReport(
            ForensicAnalysisReport report,
            RedactionOptions options,
            string replacement)
        {
            if (options.RedactHandlerNames)
            {
                report.SignedByOfficer = RedactIfNotNull(report.SignedByOfficer, replacement);
            }

            if (options.RedactTimestamps)
            {
                report.GeneratedAt = DateTime.MinValue;
                report.ReportSignedAt = null;
            }

            if (options.RedactVictimName || options.RedactPhoneNumber ||
                options.RedactAddress || options.RedactEmail)
            {
                report.Summary = RedactIfNotNull(report.Summary, replacement);
                report.AnalysisType = RedactIfNotNull(report.AnalysisType, replacement);
            }

            if (report.Findings != null)
            {
                foreach (ForensicAnalysisResult finding in report.Findings)
                {
                    if (options.RedactVictimName || options.RedactPhoneNumber ||
                        options.RedactAddress || options.RedactEmail)
                    {
                        finding.Finding = RedactIfNotNull(finding.Finding, replacement);
                        finding.Recommendation = RedactIfNotNull(finding.Recommendation, replacement);
                    }

                    if (options.RedactTimestamps)
                    {
                        finding.AnalysisTimestamp = DateTime.MinValue;
                    }
                }
            }

            RedactCustomFields(report, options.CustomSensitiveFields, replacement);
            RedactMetadata(report.Metadata, replacement);
        }

        private void RedactSignalAnomalyReport(
            SignalAnomalyReport report,
            RedactionOptions options,
            string replacement)
        {
            if (options.RedactHandlerNames)
            {
                report.SignedByOfficer = RedactIfNotNull(report.SignedByOfficer, replacement);
            }

            if (options.RedactTimestamps)
            {
                report.GeneratedAt = DateTime.MinValue;
                report.ReportSignedAt = null;
            }

            if (options.RedactAddress || options.RedactVictimName)
            {
                report.RiskAssessment = RedactIfNotNull(report.RiskAssessment, replacement);
                report.Recommendations = RedactIfNotNull(report.Recommendations, replacement);
            }

            if (options.RedactDeviceSerialNumbers && report.PerCameraBaselineStatistics != null)
            {
                var keysToRedact = new List<string>(report.PerCameraBaselineStatistics.Keys);
                foreach (string key in keysToRedact)
                {
                    report.PerCameraBaselineStatistics[replacement] = report.PerCameraBaselineStatistics[key];
                    if (!key.Equals(replacement))
                    {
                        _ = report.PerCameraBaselineStatistics.Remove(key);
                    }
                }
            }

            if (options.RedactDeviceSerialNumbers && report.CameraProfiles != null)
            {
                foreach (CameraSignalProfile profile in report.CameraProfiles)
                {
                    profile.CameraId = RedactIfNotNull(profile.CameraId, replacement);
                }
            }

            RedactCustomFields(report, options.CustomSensitiveFields, replacement);
            RedactMetadata(report.Metadata, replacement);
        }

        private void RedactCustomFields<T>(T report, List<string> fieldNames, string replacement) where T : class
        {
            if (fieldNames == null || fieldNames.Count == 0)
            {
                return;
            }

            PropertyInfo[] properties = typeof(T).GetProperties();
            foreach (string fieldName in fieldNames)
            {
                PropertyInfo? prop = properties.FirstOrDefault(p =>
                    p.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));

                if (prop != null && prop.CanWrite && prop.PropertyType == typeof(string))
                {
                    string? currentValue = (string?)prop.GetValue(report);
                    if (currentValue != null)
                    {
                        prop.SetValue(report, replacement);
                    }
                }
            }
        }

        private void RedactStringProperties<T>(T report, string replacement) where T : class
        {
            if (report == null)
            {
                return;
            }

            PropertyInfo[] properties = typeof(T).GetProperties();
            foreach (PropertyInfo prop in properties)
            {
                if (prop.CanWrite && prop.PropertyType == typeof(string))
                {
                    string? currentValue = (string?)prop.GetValue(report);
                    if (!string.IsNullOrEmpty(currentValue))
                    {
                        prop.SetValue(report, replacement);
                    }
                }
                else if (prop.CanRead && typeof(System.Collections.IEnumerable).IsAssignableFrom(prop.PropertyType)
                    && prop.PropertyType != typeof(string))
                {
                    var collection = (System.Collections.IEnumerable?)prop.GetValue(report);
                    if (collection != null && prop.CanWrite)
                    {
                        foreach (object? item in collection)
                        {
                            RedactStringProperties(item, replacement);
                        }
                    }
                }
            }
        }

        private void RedactMetadata(Dictionary<string, object>? metadata, string replacement)
        {
            if (metadata == null)
            {
                return;
            }

            var keysToUpdate = new List<string>(metadata.Keys);
            foreach (string key in keysToUpdate)
            {
                object value = metadata[key];
                if (value is string strValue && !string.IsNullOrEmpty(strValue))
                {
                    metadata[key] = replacement;
                }
            }
        }

        private string? RedactIfNotNull(string? value, string replacement)
        {
            return string.IsNullOrEmpty(value) ? value : replacement;
        }

        private T DeepClone<T>(T obj) where T : class
        {
            if (obj == null)
            {
                return null!;
            }

            try
            {
                // Use JSON serialization for deep cloning
                string json = JsonSerializer.Serialize(obj);
                return JsonSerializer.Deserialize<T>(json) ?? obj;
            }
            catch
            {
                // Fallback: return original if cloning fails
                return obj;
            }
        }
    }
}
