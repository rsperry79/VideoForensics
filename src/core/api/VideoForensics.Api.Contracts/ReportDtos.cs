namespace VideoForensics.Api.Contracts
{
    /// <summary>
    /// Data transfer object for a device health/connectivity telemetry metric.
    /// </summary>
    /// <param name="Id">Unique identifier for this health record.</param>
    /// <param name="DeviceId">The device this health record is associated with.</param>
    /// <param name="BatteryPercentage">Device battery level as a percentage (0-100), if applicable. Null for wired devices or unknown.</param>
    /// <param name="BatteryVoltageValue">Device battery voltage, if applicable. Null for wired devices or unknown.</param>
    /// <param name="WifiSignalRssi">Signal strength (RSSI) in dBm, if applicable. Null for wired devices or unknown.</param>
    /// <param name="WifiName">SSID of the connected WiFi network, if applicable. Null for non-WiFi or unknown.</param>
    /// <param name="IsExternalPowerConnected">True if external power is connected, if applicable. Null if unknown.</param>
    /// <param name="OtaStatus">Over-the-air update status, if applicable. Null if unknown.</param>
    /// <param name="IsOnline">True if the device was online at capture time, False if offline, Null if unknown.</param>
    /// <param name="LastHeartbeatUtc">Timestamp of the last known heartbeat, in UTC. Null if unknown.</param>
    /// <param name="FirmwareVersion">Device firmware version at time of capture. Null if unknown.</param>
    /// <param name="CapturedAtUtc">Timestamp when this health metric was captured, in UTC.</param>
    public record DeviceHealthDto(
        Guid Id,
        Guid DeviceId,
        decimal? BatteryPercentage,
        decimal? BatteryVoltageValue,
        int? WifiSignalRssi,
        string? WifiName,
        bool? IsExternalPowerConnected,
        string? OtaStatus,
        bool? IsOnline,
        DateTime? LastHeartbeatUtc,
        string? FirmwareVersion,
        DateTime CapturedAtUtc
    );

    /// <summary>
    /// Data transfer object for an entry in the hash-chained action log.
    /// </summary>
    /// <param name="Id">Unique identifier for this log entry.</param>
    /// <param name="Actor">Identifier of the actor (user, service, or system) that performed the action.</param>
    /// <param name="ActorType">Classification of the actor (Human, System, McpTool).</param>
    /// <param name="Action">Description of the action performed (e.g., "MediaDownloaded", "ReportGenerated").</param>
    /// <param name="EntityType">Type of entity affected by the action (e.g., "MediaItem", "Device").</param>
    /// <param name="EntityId">Unique identifier of the entity affected, if applicable. Null for system-wide actions.</param>
    /// <param name="DetailsJson">Additional action details in JSON format. Null if no additional context.</param>
    /// <param name="TimestampUtc">Timestamp when the action was recorded, in UTC.</param>
    /// <param name="PreviousEntryHash">SHA-256 hash of the previous log entry for chain-of-custody verification. Null for the first entry.</param>
    /// <param name="EntryHash">SHA-256 hash of this log entry for chain-of-custody verification.</param>
    public record ActionLogEntryDto(
        Guid Id,
        string Actor,
        string ActorType,
        string Action,
        string EntityType,
        Guid? EntityId,
        string? DetailsJson,
        DateTime TimestampUtc,
        string? PreviousEntryHash,
        string EntryHash
    );

    /// <summary>
    /// Data transfer object for an evidence review report summarizing media and integrity status.
    /// </summary>
    /// <param name="GeneratedAtUtc">Timestamp when this report was generated, in UTC.</param>
    /// <param name="ReportFromUtc">Start of the report time window, in UTC.</param>
    /// <param name="ReportToUtc">End of the report time window, in UTC.</param>
    /// <param name="MediaItems">List of media items in the report period.</param>
    /// <param name="IntegrityRecords">List of integrity verification records for media items.</param>
    /// <param name="TotalItemCount">Total number of media items in the report period.</param>
    /// <param name="VerifiedItemCount">Number of media items with passing integrity verification.</param>
    /// <param name="FailedVerificationCount">Number of media items with failed integrity verification.</param>
    public record EvidenceReviewReportDto(
        DateTime GeneratedAtUtc,
        DateTime ReportFromUtc,
        DateTime ReportToUtc,
        IReadOnlyList<MediaItemDto> MediaItems,
        IReadOnlyList<IntegrityRecordDto> IntegrityRecords,
        int TotalItemCount,
        int VerifiedItemCount,
        int FailedVerificationCount
    );

    /// <summary>
    /// Data transfer object for a forensic analysis report combining evidence, health metrics, and actions.
    /// </summary>
    /// <param name="GeneratedAtUtc">Timestamp when this report was generated, in UTC.</param>
    /// <param name="ReportFromUtc">Start of the report time window, in UTC.</param>
    /// <param name="ReportToUtc">End of the report time window, in UTC.</param>
    /// <param name="EvidenceItems">Media items included in the forensic analysis.</param>
    /// <param name="AnomalousHealthSnapshots">Device health snapshots indicating anomalies or degradation.</param>
    /// <param name="SignificantActions">Notable actions recorded in the action log during the report period.</param>
    /// <param name="Summary">Free-text summary of key findings and observations. Null if no summary generated.</param>
    public record ForensicAnalysisReportDto(
        DateTime GeneratedAtUtc,
        DateTime ReportFromUtc,
        DateTime ReportToUtc,
        IReadOnlyList<MediaItemDto> EvidenceItems,
        IReadOnlyList<DeviceHealthDto> AnomalousHealthSnapshots,
        IReadOnlyList<ActionLogEntryDto> SignificantActions,
        string? Summary
    );

    /// <summary>
    /// Data transfer object for a signal anomaly report analyzing device health and connectivity issues.
    /// </summary>
    /// <param name="GeneratedAtUtc">Timestamp when this report was generated, in UTC.</param>
    /// <param name="ReportFromUtc">Start of the report time window, in UTC.</param>
    /// <param name="ReportToUtc">End of the report time window, in UTC.</param>
    /// <param name="AnomaliesByDevice">List of anomaly findings grouped by device.</param>
    /// <param name="JammingByDevice">List of signal jamming incidents grouped by device.</param>
    public record SignalAnomalyReportDto(
        DateTime GeneratedAtUtc,
        DateTime ReportFromUtc,
        DateTime ReportToUtc,
        IReadOnlyList<AnomalyFindingsDto> AnomaliesByDevice,
        IReadOnlyList<JammingSummaryEntryDto> JammingByDevice
    );

    /// <summary>Data transfer object for anomaly findings for a single device.</summary>
    /// <param name="DeviceId">Unique identifier for the device.</param>
    /// <param name="DeviceName">User-friendly name of the device.</param>
    /// <param name="Anomalies">List of signal anomalies detected on this device.</param>
    public record AnomalyFindingsDto(
        Guid DeviceId,
        string DeviceName,
        IReadOnlyList<SignalAnomalyDto> Anomalies
    );

    /// <summary>Data transfer object for a single signal anomaly incident.</summary>
    /// <param name="OccurredAtUtc">Timestamp when the anomaly occurred, in UTC.</param>
    /// <param name="AnomalyType">Classification of the anomaly (e.g., "DegradedSignal", "ConnectionLoss").</param>
    /// <param name="Description">Human-readable description of the anomaly. Null if no description available.</param>
    /// <param name="RssiValue">Signal strength (RSSI) at the time of anomaly, in dBm. Null if not measured.</param>
    public record SignalAnomalyDto(
        DateTime OccurredAtUtc,
        string AnomalyType,
        string? Description,
        int? RssiValue
    );

    /// <summary>Data transfer object for signal jamming summary for a device.</summary>
    /// <param name="DeviceId">Unique identifier for the device.</param>
    /// <param name="DeviceName">User-friendly name of the device.</param>
    /// <param name="IncidentCount">Total number of jamming incidents detected.</param>
    /// <param name="TotalJammedDurationMinutes">Total duration of all jamming incidents in minutes.</param>
    /// <param name="AverageDegradationDb">Average signal degradation in dB across all jamming incidents.</param>
    /// <param name="MaxDegradationDb">Maximum signal degradation in dB observed during any jamming incident.</param>
    /// <param name="FirstIncidentUtc">Timestamp of the first jamming incident, in UTC. Null if no incidents.</param>
    /// <param name="LastIncidentUtc">Timestamp of the most recent jamming incident, in UTC. Null if no incidents.</param>
    public record JammingSummaryEntryDto(
        Guid DeviceId,
        string DeviceName,
        int IncidentCount,
        double TotalJammedDurationMinutes,
        double AverageDegradationDb,
        double MaxDegradationDb,
        DateTime? FirstIncidentUtc,
        DateTime? LastIncidentUtc
    );

    /// <summary>
    /// Data transfer object for an access control report showing evidence access and export audit trail.
    /// </summary>
    /// <param name="GeneratedAtUtc">Timestamp when this report was generated, in UTC.</param>
    /// <param name="ReportFromUtc">Start of the report time window, in UTC.</param>
    /// <param name="ReportToUtc">End of the report time window, in UTC.</param>
    /// <param name="AccessEvents">List of evidence access events during the report period.</param>
    /// <param name="ExportEvents">List of evidence export events during the report period.</param>
    public record AccessControlReportDto(
        DateTime GeneratedAtUtc,
        DateTime ReportFromUtc,
        DateTime ReportToUtc,
        IReadOnlyList<AccessEventDto> AccessEvents,
        IReadOnlyList<ExportEventDto> ExportEvents
    );

    /// <summary>Data transfer object for an evidence access event.</summary>
    /// <param name="AccessedAtUtc">Timestamp when the evidence was accessed, in UTC.</param>
    /// <param name="Actor">Identifier of the operator who accessed the evidence.</param>
    /// <param name="Action">Description of the access action performed.</param>
    /// <param name="EntityType">Type of entity accessed (e.g., "MediaItem", "Report").</param>
    /// <param name="EntityId">Unique identifier of the accessed entity. Null for collection-level access.</param>
    /// <param name="Details">Additional context about the access action. Null if no details recorded.</param>
    public record AccessEventDto(
        DateTime AccessedAtUtc,
        string Actor,
        string Action,
        string EntityType,
        Guid? EntityId,
        string? Details
    );

    /// <summary>Data transfer object for an evidence export event.</summary>
    /// <param name="ExportedAtUtc">Timestamp when evidence was exported, in UTC.</param>
    /// <param name="ExportedByUserName">Identifier of the operator who performed the export.</param>
    /// <param name="CaseReference">Reference identifier for the associated legal case, if applicable. Null otherwise.</param>
    /// <param name="RecipientDescription">Description of the intended recipient or purpose of the export. Null if not specified.</param>
    /// <param name="ItemCount">Number of evidence items included in this export.</param>
    public record ExportEventDto(
        DateTime ExportedAtUtc,
        string ExportedByUserName,
        string? CaseReference,
        string? RecipientDescription,
        int ItemCount
    );

    /// <summary>
    /// Data transfer object for a chain of custody report showing the complete audit trail.
    /// </summary>
    /// <param name="GeneratedAtUtc">Timestamp when this report was generated, in UTC.</param>
    /// <param name="ReportFromUtc">Start of the report time window, in UTC.</param>
    /// <param name="ReportToUtc">End of the report time window, in UTC.</param>
    /// <param name="AuditTrail">Hash-chained sequence of all actions affecting evidence.</param>
    /// <param name="ChainIntegrityVerified">True if the entire chain-of-custody chain was verified successfully, False otherwise.</param>
    /// <param name="ChainVerificationStatus">Human-readable status of chain verification. Null if no verification performed.</param>
    public record ChainOfCustodyReportDto(
        DateTime GeneratedAtUtc,
        DateTime ReportFromUtc,
        DateTime ReportToUtc,
        IReadOnlyList<ActionLogEntryDto> AuditTrail,
        bool ChainIntegrityVerified,
        string? ChainVerificationStatus
    );

    /// <summary>Extension methods for mapping domain report models to ReportDtos.</summary>
    public static class ReportDtoMapping
    {
        /// <summary>Converts a DeviceHealth entity to a DeviceHealthDto.</summary>
        public static DeviceHealthDto ToDto(this VideoForensics.Data.Common.Entities.DeviceHealth entity)
        {
            return new DeviceHealthDto(
                Id: entity.Id,
                DeviceId: entity.DeviceId,
                BatteryPercentage: entity.BatteryPercentage,
                BatteryVoltageValue: entity.BatteryVoltageValue,
                WifiSignalRssi: entity.WifiSignalRssi,
                WifiName: entity.WifiName,
                IsExternalPowerConnected: entity.IsExternalPowerConnected,
                OtaStatus: entity.OtaStatus,
                IsOnline: entity.IsOnline,
                LastHeartbeatUtc: entity.LastHeartbeatUtc,
                FirmwareVersion: entity.FirmwareVersion,
                CapturedAtUtc: entity.CapturedAtUtc
            );
        }

        /// <summary>Converts a DeviceHealthDto back to a DeviceHealth entity.</summary>
        public static VideoForensics.Data.Common.Entities.DeviceHealth ToDomain(this DeviceHealthDto dto)
        {
            return new VideoForensics.Data.Common.Entities.DeviceHealth
            {
                Id = dto.Id,
                DeviceId = dto.DeviceId,
                BatteryPercentage = dto.BatteryPercentage,
                BatteryVoltageValue = dto.BatteryVoltageValue,
                WifiSignalRssi = dto.WifiSignalRssi,
                WifiName = dto.WifiName,
                IsExternalPowerConnected = dto.IsExternalPowerConnected,
                OtaStatus = dto.OtaStatus,
                IsOnline = dto.IsOnline,
                LastHeartbeatUtc = dto.LastHeartbeatUtc,
                FirmwareVersion = dto.FirmwareVersion,
                CapturedAtUtc = dto.CapturedAtUtc
            };
        }

        /// <summary>Converts an ActionLogEntry entity to an ActionLogEntryDto.</summary>
        public static ActionLogEntryDto ToDto(this VideoForensics.Data.Common.Entities.ActionLogEntry entity)
        {
            return new ActionLogEntryDto(
                Id: entity.Id,
                Actor: entity.Actor,
                ActorType: entity.ActorType.ToString(),
                Action: entity.Action,
                EntityType: entity.EntityType,
                EntityId: entity.EntityId,
                DetailsJson: entity.DetailsJson,
                TimestampUtc: entity.TimestampUtc,
                PreviousEntryHash: entity.PreviousEntryHash,
                EntryHash: entity.EntryHash
            );
        }

        /// <summary>Converts an EvidenceReviewReport model to an EvidenceReviewReportDto.</summary>
        public static EvidenceReviewReportDto ToDto(this VideoForensics.Data.Core.Models.EvidenceReviewReport model)
        {
            return new EvidenceReviewReportDto(
                GeneratedAtUtc: model.GeneratedAtUtc,
                ReportFromUtc: model.ReportFromUtc,
                ReportToUtc: model.ReportToUtc,
                MediaItems: model.MediaItems.Select(x => x.ToDto()).ToList().AsReadOnly(),
                IntegrityRecords: model.IntegrityRecords.Select(x => x.ToDto()).ToList().AsReadOnly(),
                TotalItemCount: model.TotalItemCount,
                VerifiedItemCount: model.VerifiedItemCount,
                FailedVerificationCount: model.FailedVerificationCount
            );
        }

        /// <summary>Converts a ForensicAnalysisReport model to a ForensicAnalysisReportDto.</summary>
        public static ForensicAnalysisReportDto ToDto(this VideoForensics.Data.Core.Models.ForensicAnalysisReport model)
        {
            return new ForensicAnalysisReportDto(
                GeneratedAtUtc: model.GeneratedAtUtc,
                ReportFromUtc: model.ReportFromUtc,
                ReportToUtc: model.ReportToUtc,
                EvidenceItems: model.EvidenceItems.Select(x => x.ToDto()).ToList().AsReadOnly(),
                AnomalousHealthSnapshots: model.AnomalousHealthSnapshots.Select(x => x.ToDto()).ToList().AsReadOnly(),
                SignificantActions: model.SignificantActions.Select(x => x.ToDto()).ToList().AsReadOnly(),
                Summary: model.Summary
            );
        }

        /// <summary>Converts a SignalAnomalyReport model to a SignalAnomalyReportDto.</summary>
        public static SignalAnomalyReportDto ToDto(this VideoForensics.Data.Core.Models.SignalAnomalyReport model)
        {
            return new SignalAnomalyReportDto(
                GeneratedAtUtc: model.GeneratedAtUtc,
                ReportFromUtc: model.ReportFromUtc,
                ReportToUtc: model.ReportToUtc,
                AnomaliesByDevice: model.AnomaliesByDevice.Select(x => x.ToDto()).ToList().AsReadOnly(),
                JammingByDevice: model.JammingByDevice.Select(x => x.ToDto()).ToList().AsReadOnly()
            );
        }

        private static AnomalyFindingsDto ToDto(this VideoForensics.Data.Core.Models.SignalAnomalyReport.AnomalyFindings model)
        {
            return new AnomalyFindingsDto(
                DeviceId: model.DeviceId,
                DeviceName: model.DeviceName,
                Anomalies: model.Anomalies.Select(x => x.ToDto()).ToList().AsReadOnly()
            );
        }

        private static SignalAnomalyDto ToDto(this VideoForensics.Data.Core.Models.SignalAnomalyReport.SignalAnomaly model)
        {
            return new SignalAnomalyDto(
                OccurredAtUtc: model.OccurredAtUtc,
                AnomalyType: model.AnomalyType,
                Description: model.Description,
                RssiValue: model.RssiValue
            );
        }

        private static JammingSummaryEntryDto ToDto(this VideoForensics.Data.Core.Models.SignalAnomalyReport.JammingSummaryEntry model)
        {
            return new JammingSummaryEntryDto(
                DeviceId: model.DeviceId,
                DeviceName: model.DeviceName,
                IncidentCount: model.IncidentCount,
                TotalJammedDurationMinutes: model.TotalJammedDurationMinutes,
                AverageDegradationDb: model.AverageDegradationDb,
                MaxDegradationDb: model.MaxDegradationDb,
                FirstIncidentUtc: model.FirstIncidentUtc,
                LastIncidentUtc: model.LastIncidentUtc
            );
        }

        /// <summary>Converts an AccessControlReport model to an AccessControlReportDto.</summary>
        public static AccessControlReportDto ToDto(this VideoForensics.Data.Core.Models.AccessControlReport model)
        {
            return new AccessControlReportDto(
                GeneratedAtUtc: model.GeneratedAtUtc,
                ReportFromUtc: model.ReportFromUtc,
                ReportToUtc: model.ReportToUtc,
                AccessEvents: model.AccessEvents.Select(x => x.ToDto()).ToList().AsReadOnly(),
                ExportEvents: model.ExportEvents.Select(x => x.ToDto()).ToList().AsReadOnly()
            );
        }

        private static AccessEventDto ToDto(this VideoForensics.Data.Core.Models.AccessControlReport.AccessEvent model)
        {
            return new AccessEventDto(
                AccessedAtUtc: model.AccessedAtUtc,
                Actor: model.Actor,
                Action: model.Action,
                EntityType: model.EntityType,
                EntityId: model.EntityId,
                Details: model.Details
            );
        }

        private static ExportEventDto ToDto(this VideoForensics.Data.Core.Models.AccessControlReport.ExportEvent model)
        {
            return new ExportEventDto(
                ExportedAtUtc: model.ExportedAtUtc,
                ExportedByUserName: model.ExportedByUserName,
                CaseReference: model.CaseReference,
                RecipientDescription: model.RecipientDescription,
                ItemCount: model.ItemCount
            );
        }

        /// <summary>Converts a ChainOfCustodyReport model to a ChainOfCustodyReportDto.</summary>
        public static ChainOfCustodyReportDto ToDto(this VideoForensics.Data.Core.Models.ChainOfCustodyReport model)
        {
            return new ChainOfCustodyReportDto(
                GeneratedAtUtc: model.GeneratedAtUtc,
                ReportFromUtc: model.ReportFromUtc,
                ReportToUtc: model.ReportToUtc,
                AuditTrail: model.AuditTrail.Select(x => x.ToDto()).ToList().AsReadOnly(),
                ChainIntegrityVerified: model.ChainIntegrityVerified,
                ChainVerificationStatus: model.ChainVerificationStatus
            );
        }
    }
}
