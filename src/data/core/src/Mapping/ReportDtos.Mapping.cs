using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Core.Api.Contracts
{
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
                CapturedAtUtc: entity.CapturedAtUtc,
                DeviceType: entity.DeviceType,
                FirmwareVersionStatus: entity.FirmwareVersionStatus,
                BatteryPresent: entity.BatteryPresent,
                BatteryPercentageCategory: entity.BatteryPercentageCategory,
                BatteryVoltageCategory: entity.BatteryVoltageCategory,
                RssiCategory: entity.RssiCategory,
                NetworkConnectionValue: entity.NetworkConnectionValue,
                PacketLoss: entity.PacketLoss,
                PacketLossCategory: entity.PacketLossCategory,
                LatestSignalStrength: entity.LatestSignalStrength,
                LatestSignalCategory: entity.LatestSignalCategory,
                AverageSignalStrength: entity.AverageSignalStrength,
                AverageSignalCategory: entity.AverageSignalCategory,
                Firmware: entity.Firmware,
                UpdatedAtUtc: entity.UpdatedAtUtc,
                PacketLossStrength: entity.PacketLossStrength,
                WifiIsRingNetwork: entity.WifiIsRingNetwork,
                PtzConnected: entity.PtzConnected
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
                CapturedAtUtc = dto.CapturedAtUtc,
                DeviceType = dto.DeviceType,
                FirmwareVersionStatus = dto.FirmwareVersionStatus,
                BatteryPresent = dto.BatteryPresent,
                BatteryPercentageCategory = dto.BatteryPercentageCategory,
                BatteryVoltageCategory = dto.BatteryVoltageCategory,
                RssiCategory = dto.RssiCategory,
                NetworkConnectionValue = dto.NetworkConnectionValue,
                PacketLoss = dto.PacketLoss,
                PacketLossCategory = dto.PacketLossCategory,
                LatestSignalStrength = dto.LatestSignalStrength,
                LatestSignalCategory = dto.LatestSignalCategory,
                AverageSignalStrength = dto.AverageSignalStrength,
                AverageSignalCategory = dto.AverageSignalCategory,
                Firmware = dto.Firmware,
                UpdatedAtUtc = dto.UpdatedAtUtc,
                PacketLossStrength = dto.PacketLossStrength,
                WifiIsRingNetwork = dto.WifiIsRingNetwork,
                PtzConnected = dto.PtzConnected
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
                FailedVerificationCount: model.FailedVerificationCount,
                MediaProviderEventLinks: model.MediaProviderEventLinks.Select(x => new MediaProviderEventLinkDto(x.MediaItemId, x.ProviderEventId)).ToList().AsReadOnly()
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
                ChainVerificationStatus: model.ChainVerificationStatus,
                ChainEntriesChecked: model.ChainEntriesChecked,
                ChainBreak: Enum.Parse<ChainBreakKindDto>(model.ChainBreak.ToString()),
                ChainFailedEntryId: model.ChainFailedEntryId,
                ChainFailedAtUtc: model.ChainFailedAtUtc
            );
        }
    }
}
