using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using VideoForensics.Forensics.Interfaces;
using VideoForensics.Forensics.KeyManagement;
using VideoForensics.Forensics.Models;

namespace VideoForensics.Forensics.Implementations
{
    /// <summary>
    /// Cryptographically signs forensic reports using RSA-2048 with SHA-256 hashing.
    /// Signs can be verified to prove reports haven't been tampered with.
    /// </summary>
    internal class ForensicReportSigner : IForensicReportSigner
    {
        private readonly IKeyStorageProvider _keyStorageProvider;

        public ForensicReportSigner(IKeyStorageProvider keyStorageProvider)
        {
            _keyStorageProvider = keyStorageProvider ?? throw new ArgumentNullException(nameof(keyStorageProvider));
        }

        public async Task<ChainOfCustodyReport> SignChainOfCustodyReportAsync(
            ChainOfCustodyReport report,
            string keyId,
            string signingOfficer)
        {
            ArgumentNullException.ThrowIfNull(report);
            ArgumentNullException.ThrowIfNullOrWhiteSpace(keyId);
            ArgumentNullException.ThrowIfNullOrWhiteSpace(signingOfficer);

            return await SignReportAsync(report, keyId, signingOfficer);
        }

        public async Task<EvidenceValidationReport> SignValidationReportAsync(
            EvidenceValidationReport report,
            string keyId,
            string signingOfficer)
        {
            ArgumentNullException.ThrowIfNull(report);
            ArgumentNullException.ThrowIfNullOrWhiteSpace(keyId);
            ArgumentNullException.ThrowIfNullOrWhiteSpace(signingOfficer);

            return await SignReportAsync(report, keyId, signingOfficer);
        }

        public async Task<ForensicAnalysisReport> SignAnalysisReportAsync(
            ForensicAnalysisReport report,
            string keyId,
            string signingOfficer)
        {
            ArgumentNullException.ThrowIfNull(report);
            ArgumentNullException.ThrowIfNullOrWhiteSpace(keyId);
            ArgumentNullException.ThrowIfNullOrWhiteSpace(signingOfficer);

            return await SignReportAsync(report, keyId, signingOfficer);
        }

        public async Task<SignalAnomalyReport> SignSignalAnomalyReportAsync(
            SignalAnomalyReport report,
            string keyId,
            string signingOfficer)
        {
            ArgumentNullException.ThrowIfNull(report);
            ArgumentNullException.ThrowIfNullOrWhiteSpace(keyId);
            ArgumentNullException.ThrowIfNullOrWhiteSpace(signingOfficer);

            return await SignReportAsync(report, keyId, signingOfficer);
        }

        public async Task<bool> VerifyReportSignatureAsync<T>(T report, string certificateThumbprint) where T : class
        {
            ArgumentNullException.ThrowIfNull(report);
            ArgumentNullException.ThrowIfNullOrWhiteSpace(certificateThumbprint);

            try
            {
                string? signature = GetReportSignature(report);
                if (string.IsNullOrEmpty(signature))
                {
                    return false;
                }

                byte[] reportData = SerializeReportForVerification(report);
                bool isValid = await _keyStorageProvider.VerifySignatureAsync(
                    certificateThumbprint,
                    reportData,
                    signature);

                return isValid;
            }
            catch
            {
                return false;
            }
        }

        public async Task<IEnumerable<T>> SignReportsAsync<T>(
            IEnumerable<T> reports,
            string keyId,
            string signingOfficer) where T : class
        {
            ArgumentNullException.ThrowIfNull(reports);
            ArgumentNullException.ThrowIfNullOrWhiteSpace(keyId);
            ArgumentNullException.ThrowIfNullOrWhiteSpace(signingOfficer);

            var signedReports = new List<T>();
            foreach (T report in reports)
            {
                T signedReport = await SignReportAsync(report, keyId, signingOfficer);
                signedReports.Add(signedReport);
            }

            return signedReports;
        }

        private async Task<T> SignReportAsync<T>(T report, string keyId, string signingOfficer) where T : class
        {
            byte[] reportData = SerializeReportForSigning(report);
            string signature = await _keyStorageProvider.SignDataAsync(keyId, reportData);
            KeyMetadata metadata = await _keyStorageProvider.GetKeyMetadataAsync(keyId);

            SetReportSignature(report, signature, signingOfficer, metadata.CertificateThumbprint);

            return report;
        }

        private byte[] SerializeReportForSigning<T>(T report) where T : class
        {
            string json = JsonSerializer.Serialize(report);
            return Encoding.UTF8.GetBytes(json);
        }

        private byte[] SerializeReportForVerification<T>(T report) where T : class
        {
            string json = JsonSerializer.Serialize(report);
            return Encoding.UTF8.GetBytes(json);
        }

        private static string? GetReportSignature<T>(T report) where T : class
        {
            Type type = typeof(T);
            PropertyInfo? signatureProperty = type.GetProperty("DigitalSignature");
            if (signatureProperty == null)
            {
                return null;
            }

            object? value = signatureProperty.GetValue(report);
            return value as string;
        }

        private static void SetReportSignature<T>(
            T report,
            string signature,
            string signingOfficer,
            string certificateThumbprint) where T : class
        {
            Type type = typeof(T);

            SetPropertyValue(type, report, "DigitalSignature", signature);
            SetPropertyValue(type, report, "SignedByOfficer", signingOfficer);
            SetPropertyValue(type, report, "SigningCertificateThumbprint", certificateThumbprint);
            SetPropertyValue(type, report, "ReportSignedAt", DateTime.UtcNow);
        }

        private static void SetPropertyValue<T>(Type type, T report, string propertyName, object? value) where T : class
        {
            PropertyInfo? property = type.GetProperty(propertyName);
            if (property?.CanWrite == true)
            {
                property.SetValue(report, value);
            }
        }
    }
}
