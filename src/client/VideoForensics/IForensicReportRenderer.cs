namespace VideoForensics
{
    /// <summary>Renders forensic report tables and data</summary>
    public interface IForensicReportRenderer
    {
        /// <summary>Displays the evidence table with forensic case metadata</summary>
        Task ShowEvidenceAsync(CancellationToken ct);

        /// <summary>Displays the forensic reports summary table</summary>
        Task ShowForensicReportsAsync(CancellationToken ct);

        /// <summary>Displays signal anomalies detection results</summary>
        Task ShowSignalAnomaliesAsync(CancellationToken ct);

        /// <summary>Displays access control and permissions audit results</summary>
        Task ShowAccessControlAsync(CancellationToken ct);

        /// <summary>Displays chain of custody audit trail</summary>
        Task ShowChainOfCustodyAsync(CancellationToken ct);
    }
}
