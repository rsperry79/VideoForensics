using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>Helper class for building test entities with sensible defaults.</summary>
    public static class TestDataBuilder
    {
        public static User BuildUser(string? providerUserKey = null, string? displayName = null, string? email = null)
        {
            return new User
            {
                Id = Guid.NewGuid(),
                ProviderUserKey = providerUserKey ?? $"user_key_{Guid.NewGuid():N}",
                DisplayName = displayName ?? $"Test User {Guid.NewGuid():N}",
                Email = email,
                CreatedUtc = DateTime.UtcNow
            };
        }

        public static ProviderAccount BuildProviderAccount(Guid? userId = null, string? providerName = null)
        {
            return new ProviderAccount
            {
                Id = Guid.NewGuid(),
                UserId = userId ?? Guid.NewGuid(),
                ProviderName = providerName ?? "Ring",
                LinkedUtc = DateTime.UtcNow,
                IsActive = true
            };
        }

        public static Location BuildLocation(string? providerLocationId = null, string? name = null)
        {
            return new Location
            {
                Id = Guid.NewGuid(),
                ProviderLocationId = providerLocationId ?? $"loc_{Guid.NewGuid():N}",
                Name = name ?? $"Test Location {Guid.NewGuid():N}",
                Address = "123 Test St"
            };
        }

        public static Device BuildDevice(Guid? locationId = null, string? providerDeviceId = null, string? name = null)
        {
            return new Device
            {
                Id = Guid.NewGuid(),
                LocationId = locationId ?? Guid.NewGuid(),
                ProviderDeviceId = providerDeviceId ?? $"dev_{Guid.NewGuid():N}",
                Name = name ?? $"Test Camera {Guid.NewGuid():N}",
                Type = "Camera",
                IsOnline = true,
                TimeZoneId = "America/New_York"
            };
        }

        public static MediaItem BuildMediaItem(Guid? deviceId = null, Guid? downloadEventId = null, string? fileName = null, string? hash = null)
        {
            return new MediaItem
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId ?? Guid.NewGuid(),
                DownloadEventId = downloadEventId,
                FileName = fileName ?? $"video_{Guid.NewGuid():N}.mp4",
                FilePath = $"/videos/{Guid.NewGuid():N}.mp4",
                MediaFormat = "MP4",
                FileSizeBytes = 1024 * 1024,
                RecordedAtUtc = DateTime.UtcNow,
                DownloadedAtUtc = DateTime.UtcNow,
                Sha256Hash = hash ?? $"{Guid.NewGuid():N}{Guid.NewGuid():N}",
                IntegrityVerified = true
            };
        }

        public static DownloadEvent BuildDownloadEvent(Guid? deviceId = null, string? providerEventId = null, bool success = true)
        {
            DateTime now = DateTime.UtcNow;
            return new DownloadEvent
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId ?? Guid.NewGuid(),
                ProviderEventId = providerEventId ?? $"evt_{Guid.NewGuid():N}",
                EventType = "Motion",
                Answered = false,
                Favorite = false,
                EventOccurredAtUtc = now.AddHours(-1),
                RecordingStatus = "Ready",
                DownloadStartedUtc = now,
                DownloadCompletedUtc = success ? now : null,
                Success = success,
                AttemptCount = 1,
                AppVersion = "1.0.0"
            };
        }

        public static Credential BuildCredential(Guid? accountId = null, string? credentialType = null)
        {
            return new Credential
            {
                Id = Guid.NewGuid(),
                ProviderAccountId = accountId ?? Guid.NewGuid(),
                CredentialType = credentialType ?? "Password",
                EncryptedValue = "encrypted_placeholder",
                EncryptionProvider = "DataProtection",
                CreatedUtc = DateTime.UtcNow
            };
        }

        public static ActionLogEntry BuildActionLogEntry(string? actor = null, string? action = null, string? entityType = null, string? hash = null)
        {
            return new ActionLogEntry
            {
                Id = Guid.NewGuid(),
                Actor = actor ?? "TestActor",
                ActorType = ActorType.Human,
                Action = action ?? "TestAction",
                EntityType = entityType ?? "TestEntity",
                EntityId = Guid.NewGuid(),
                DetailsJson = null,
                TimestampUtc = DateTime.UtcNow,
                PreviousEntryHash = null,
                EntryHash = hash ?? $"{Guid.NewGuid():N}{Guid.NewGuid():N}"
            };
        }

        public static Event BuildEvent(Guid? deviceId = null, string? providerEventId = null)
        {
            return new Event
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId ?? Guid.NewGuid(),
                ProviderEventId = providerEventId ?? $"evt_{Guid.NewGuid():N}",
                EventType = "Motion",
                OccurredAtUtc = DateTime.UtcNow,
                DiscoveredAtUtc = DateTime.UtcNow
            };
        }

        public static DeviceConfigSnapshot BuildDeviceConfigSnapshot(Guid? deviceId = null)
        {
            return new DeviceConfigSnapshot
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId ?? Guid.NewGuid(),
                MotionDetectionEnabled = true,
                MotionSensitivity = "50",
                RecordingMode = "On",
                CapturedAtUtc = DateTime.UtcNow,
                Source = DeviceConfigSource.Fetched
            };
        }

        public static DeviceHealth BuildDeviceHealth(Guid? deviceId = null)
        {
            return new DeviceHealth
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId ?? Guid.NewGuid(),
                BatteryPercentage = 80m,
                WifiSignalRssi = -50,
                WifiName = "TestNetwork",
                IsOnline = true,
                FirmwareVersion = "1.0.0",
                CapturedAtUtc = DateTime.UtcNow
            };
        }

        public static ProviderReconciliationRecord BuildProviderReconciliationRecord(Guid? deviceId = null, string? providerEventId = null)
        {
            return new ProviderReconciliationRecord
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId ?? Guid.NewGuid(),
                RanAtUtc = DateTime.UtcNow,
                ProviderEventId = providerEventId ?? $"evt_{Guid.NewGuid():N}",
                DiscrepancyType = DiscrepancyType.MetadataChanged,
                FieldName = "EventType",
                StoredValue = "Motion",
                ProviderValue = "Person"
            };
        }

        public static ExportRecord BuildExportRecord(string? exportedByUserName = null)
        {
            return new ExportRecord
            {
                Id = Guid.NewGuid(),
                ExportedAtUtc = DateTime.UtcNow,
                ExportedByUserName = exportedByUserName ?? "TestUser",
                CaseReference = "CASE-2026-001",
                RecipientDescription = "Law Enforcement",
                ArchiveFileName = $"export_{Guid.NewGuid():N}.zip",
                ArchiveSha256Hash = $"{Guid.NewGuid():N}{Guid.NewGuid():N}",
                WasEncrypted = false,
                ItemCount = 5,
                AppVersion = "1.0.0"
            };
        }

        public static ExportRecordItem BuildExportRecordItem(Guid? exportRecordId = null, Guid? mediaItemId = null)
        {
            return new ExportRecordItem
            {
                Id = Guid.NewGuid(),
                ExportRecordId = exportRecordId ?? Guid.NewGuid(),
                MediaItemId = mediaItemId ?? Guid.NewGuid(),
                MediaItemSha256HashAtExport = $"{Guid.NewGuid():N}{Guid.NewGuid():N}"
            };
        }

        public static AccessAuditLogEntity BuildAccessAuditLog(Guid? evidenceId = null, string? userId = null, string? action = null)
        {
            return new AccessAuditLogEntity
            {
                Id = Guid.NewGuid(),
                EvidenceId = evidenceId ?? Guid.NewGuid(),
                UserId = userId ?? $"user_{Guid.NewGuid():N}",
                AccessedAtUtc = DateTime.UtcNow,
                Action = action ?? "View",
                IpAddress = "127.0.0.1",
                Purpose = "Forensic Investigation"
            };
        }

        public static ExportAuditRecordEntity BuildExportAuditRecord(Guid? locationId = null, string? exportedBy = null)
        {
            return new ExportAuditRecordEntity
            {
                Id = Guid.NewGuid(),
                LocationId = locationId ?? Guid.NewGuid(),
                ExportedAtUtc = DateTime.UtcNow,
                ExportedBy = exportedBy ?? "TestUser",
                EventsExported = 10,
                ExportFormat = "AES256Archive",
                Purpose = "CaseFile"
            };
        }

        public static JammingIncidentRecord BuildJammingIncident(
            Guid? deviceId = null,
            DateTime? startUtc = null,
            DateTime? endUtc = null,
            double confidence = 0.75,
            int affectedEventCount = 5)
        {
            DateTime start = startUtc ?? DateTime.UtcNow;
            return new JammingIncidentRecord
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId ?? Guid.NewGuid(),
                StartUtc = start,
                EndUtc = endUtc ?? start.AddMinutes(15),
                AffectedEventCount = affectedEventCount,
                AverageDegradationDb = confidence * 20,
                Confidence = (JammingConfidenceLevel)(int)(confidence * 3),
                DetectedAtUtc = DateTime.UtcNow,
                Notes = "Test jamming incident",
                Source = JammingIncidentSource.AutoDetected
            };
        }

        public static JammingStatsSummary BuildJammingStats(Guid? deviceId = null, int incidentCount = 5)
        {
            return new JammingStatsSummary
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId ?? Guid.NewGuid(),
                IncidentCount = incidentCount,
                TotalJammedDurationMinutes = incidentCount * 15,
                AverageDegradationDb = 10.0,
                MaxDegradationDb = 18.5,
                LowConfidenceCount = 1,
                MediumConfidenceCount = 2,
                HighConfidenceCount = incidentCount - 3,
                DefiniteConfidenceCount = 0,
                FirstIncidentUtc = DateTime.UtcNow.AddHours(-24),
                LastIncidentUtc = DateTime.UtcNow,
                LastUpdatedUtc = DateTime.UtcNow
            };
        }
    }
}
