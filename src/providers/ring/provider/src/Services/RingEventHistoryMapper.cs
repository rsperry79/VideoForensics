using System.Globalization;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Ring.Entities;

namespace VideoForensics.Providers.Ring.Services
{
    /// <summary>An event row together with the nested objects that belong to it.</summary>
    /// <typeparam name="TEvent">The event row type.</typeparam>
    public sealed record MappedRingEvent<TEvent>(TEvent Event, RingEventDetails Details);

    /// <summary>
    /// Maps the video search, location event, location history, doorbot history and app location history payloads
    /// onto persistence entities. Each call assigns new row ids, so a caller replaces a stored event's rows rather than
    /// merging into them. Nothing is mapped that the Ring entity does not carry, and null stays null.
    /// </summary>
    public static class RingEventHistoryMapper
    {
        public static MappedRingEvent<RingVideoSearchEvent> MapVideoSearch(VideoSearchItem item, Guid ringAccountId)
        {
            ArgumentNullException.ThrowIfNull(item);

            var evt = new RingVideoSearchEvent
            {
                Id = Guid.NewGuid(),
                RingAccountId = ringAccountId,
                ProviderEventId = item.DingId,
                RingItemId = item.Id,
                CreatedAtMs = item.CreatedAtUnixMs,
                UpdatedAtMs = item.UpdatedAtUnixMs,
                Kind = item.Kind,
                State = item.State,
                DurationSeconds = item.Duration,
                Favorite = item.Favorite,
                HqUrl = item.HqUrl,
                LqUrl = item.LqUrl,
                ThumbnailUrl = item.ThumbnailUrl,
                UntranscodedUrl = item.UntranscodedUrl,
                IsE2ee = item.IsE2ee,
                ManifestId = item.ManifestId,
                PrerollDuration = item.PrerollDuration,
                HadSubscription = item.HadSubscription,
                RadarDataUrl = item.RadarDataUrl,
                OwnerId = item.OwnerId,
                SourceId = item.SourceId,
                Riid = item.Riid,
                BatteryPercentage = item.BatteryPercentage,
                BatteryStatus = item.BatteryStatus,
            };

            var details = new RingEventDetails
            {
                Doorbot = MapDoorbot(item.Doorbot),
                Properties = MapProperties(item.Properties),
                DevicePlacement = MapPlacement(item.DevicePlacement),
                Geolocation = MapGeolocation(item.Geolocation),
                SubscriptionReceipt = MapReceipt(item.SubscriptionReceipt),
                CvProperties = MapCv(item.CvProperties),
            };
            return new MappedRingEvent<RingVideoSearchEvent>(evt, details);
        }

        public static RingLocationEventPage MapLocationEventsPage(string providerLocationId, LocationEventsMeta? meta)
        {
            return new RingLocationEventPage
            {
                Id = Guid.NewGuid(),
                ProviderLocationId = providerLocationId,
                PaginationKey = meta?.PaginationKey,
            };
        }

        public static MappedRingEvent<RingLocationEvent> MapLocationEvent(LocationEvent source, string providerLocationId)
        {
            ArgumentNullException.ThrowIfNull(source);

            var evt = new RingLocationEvent
            {
                Id = Guid.NewGuid(),
                ProviderLocationId = providerLocationId,
                ProviderEventId = source.EventId,
                SourceId = source.SourceId,
                EventType = source.EventType,
                State = source.State,
                Favorite = source.Favorite,
                Recorded = source.Recorded,
                RecordingStatus = source.RecordingStatus,
                IsE2ee = source.IsE2ee,
                CreatedAt = source.CreatedAt,
                HadSubscription = source.HadSubscription,
                OwnerId = source.OwnerId,
                Riid = source.Riid,
                DoorbotId = source.DoorbotId,
                DingId = source.DingId,
                DingIdString = source.DingIdString,
                Kind = source.Kind,
                BatteryPercentage = source.BatteryPercentage,
                BatteryStatus = source.BatteryStatus,
            };

            var details = new RingEventDetails
            {
                Doorbot = MapDoorbot(source.Doorbot),
                Properties = MapProperties(source.Properties),
                DevicePlacement = MapPlacement(source.DevicePlacement),
                Geolocation = MapGeolocation(source.Geolocation),
                SubscriptionReceipt = MapReceipt(source.SubscriptionReceipt),
                LastLocation = MapLastLocation(source.LastLocation),
                Siren = MapSiren(source.Siren),
                CvProperties = MapCv(source.CvProperties),
            };
            return new MappedRingEvent<RingLocationEvent>(evt, details);
        }

        public static RingLocationHistoryPage MapLocationHistoryPage(string providerLocationId, string? paginationKey)
        {
            return new RingLocationHistoryPage
            {
                Id = Guid.NewGuid(),
                ProviderLocationId = providerLocationId,
                PaginationKey = paginationKey,
            };
        }

        public static MappedRingEvent<RingLocationHistoryEvent> MapLocationHistoryItem(LocationHistoryItem source, string providerLocationId)
        {
            ArgumentNullException.ThrowIfNull(source);

            var evt = new RingLocationHistoryEvent
            {
                Id = Guid.NewGuid(),
                ProviderLocationId = providerLocationId,
                ProviderEventId = source.EventId,
                Schema = source.Schema,
                SourceId = source.SourceId,
                StartTime = source.StartTime,
                EndTime = source.EndTime,
                EventType = source.EventType,
                SourceType = source.SourceType,
                DurationMs = source.DurationMs,
                SessionDurationMs = source.SessionDuration,
                State = source.State,
                HadSubscription = source.HadSubscription,
                IsFavorite = source.IsFavorite,
                RecordingStatus = source.RecordingStatus,
                Origin = source.Origin,
                OriginDisplayName = source.OriginDisplayName,
                ErrorMessage = source.ErrorMessage,
                UpdatedAt = source.UpdatedAt,
                OwnerId = source.OwnerId,
                DeleteInProgress = source.DeleteInProgress,
                LowConfidence = source.LowConfidence,
                Similarity = source.Similarity,
                Riid = source.Riid,
                UserSpecifiedRecordingDisabled = source.UserSpecifiedRecordingDisabled,
                IsWatched = source.IsWatched,
                IsPropertyViewEnabled = source.IsPropertyViewEnabled,
                IsActive = source.IsActive,
                UserActionDisabled = source.UserActionDisabled,
                Mission = source.Mission,
            };

            if (source.Visualizations is { } visualizations)
            {
                AddVisualization(evt, visualizations.CloudMediaVisualization, "cloud");
                AddVisualization(evt, visualizations.LocalMediaVisualization, "local");
            }

            var details = new RingEventDetails
            {
                Doorbot = MapDoorbot(source.Device),
                Properties = MapProperties(source.Properties),
                CvProperties = MapLocationHistoryCv(source.Cv),
            };
            return new MappedRingEvent<RingLocationHistoryEvent>(evt, details);
        }

        public static MappedRingEvent<RingDoorbotHistoryEvent> MapDoorbotHistoryEvent(DoorbotHistoryEvent source)
        {
            ArgumentNullException.ThrowIfNull(source);

            var evt = new RingDoorbotHistoryEvent
            {
                Id = Guid.NewGuid(),
                ProviderEventId = source.Id?.ToString(CultureInfo.InvariantCulture),
                CreatedAt = source.CreatedAt,
                Answered = source.Answered,
                Kind = source.Kind,
                Favorite = source.Favorite,
                SnapshotUrl = source.SnapshotUrl,
                Duration = source.Duration,
                IsE2ee = source.IsE2ee,
                HadSubscription = source.HadSubscription,
                OwnerId = source.OwnerId,
                Riid = source.Riid,
            };

            // The events array (source.Events) is always empty in captures and its element shape is unknown, so it is not stored.
            var details = new RingEventDetails
            {
                Recording = source.Recording is null ? null : new RingEventRecording { Id = Guid.NewGuid(), Status = source.Recording.Status },
                Doorbot = MapDoorbot(source.Doorbot),
                Properties = MapProperties(source.Properties),
                DevicePlacement = MapPlacement(source.DevicePlacement),
                Geolocation = MapGeolocation(source.Geolocation),
                LastLocation = MapLastLocation(source.LastLocation),
                Siren = MapSiren(source.Siren),
                CvProperties = MapCv(source.CvProperties),
            };
            return new MappedRingEvent<RingDoorbotHistoryEvent>(evt, details);
        }

        public static RingAppLocationHistoryMessage MapAppLocationHistory(AppLocationHistoryMessage source)
        {
            ArgumentNullException.ThrowIfNull(source);

            AppLocationHistoryContext? context = source.Context;
            var message = new RingAppLocationHistoryMessage
            {
                Id = Guid.NewGuid(),
                Seq = source.Seq,
                Msg = source.Msg,
                DataType = source.DataType,
                Src = source.Src,
                EventId = context?.EventId,
                EventPublishedTsMs = context?.EventPublishedTsMs,
                RequestSeq = context?.RequestSeq,
                AffectedEntityId = context?.AffectedEntityId,
                DeviceFamily = context?.DeviceFamily,
                InitiatingEntityType = context?.InitiatingEntityType,
                AffectedEntityType = context?.AffectedEntityType,
                ForwarderTopic = context?.ForwarderTopic,
                AccountId = context?.AccountId,
                RequestSessionId = context?.RequestSessionId,
                EventLevel = context?.EventLevel,
                InitiatingEntityId = context?.InitiatingEntityId,
                EventOccurredTsMs = context?.EventOccurredTsMs,
                AffectedEntityName = context?.AffectedEntityName,
                ProgramId = context?.ProgramId,
            };
            message.Bodies = (source.Body ?? []).Select((body, index) => MapAppBody(body, index, message.Id)).ToList();
            return message;
        }

        private static RingAppLocationHistoryBody MapAppBody(AppLocationHistoryBody source, int position, Guid messageId)
        {
            AppLocationHistoryGeneralV2? general = source.General?.V2;
            AppLocationHistoryBodyContextV1? context = source.Context?.V1;
            var body = new RingAppLocationHistoryBody
            {
                Id = Guid.NewGuid(),
                RingAppLocationHistoryMessageId = messageId,
                Position = position,
                DeviceType = general?.DeviceType,
                CatalogId = general?.CatalogId,
                Zid = general?.Zid,
                LastUpdate = general?.LastUpdate,
                AdapterType = general?.AdapterType,
                BatteryStatus = context?.BatteryStatus,
                SubCategoryId = context?.SubCategoryId,
                DeviceName = context?.DeviceName,
                CategoryId = context?.CategoryId,
                RoomName = context?.RoomName,
            };

            AppLocationHistoryDeviceV1? device = context?.Device?.V1;
            if (device is not null)
            {
                var state = new RingAppLocationHistoryDeviceState
                {
                    Id = Guid.NewGuid(),
                    RingAppLocationHistoryBodyId = body.Id,
                    Mode = device.Mode,
                    SirenState = device.Siren?.State,
                };
                AddMode(state, "all", device.Modes?.All);
                AddMode(state, "some", device.Modes?.Some);
                AddMode(state, "none", device.Modes?.None);
                body.DeviceState = state;
            }

            body.Impulses = (source.Impulse?.V1 ?? []).Select((impulse, index) => new RingAppLocationHistoryImpulse
            {
                Id = Guid.NewGuid(),
                RingAppLocationHistoryBodyId = body.Id,
                Position = index,
                ImpulseType = impulse.ImpulseType,
                CommandType = impulse.Data?.CommandType,
                DataMode = impulse.Data?.Data?.Mode,
            }).ToList();
            return body;
        }

        private static void AddMode(RingAppLocationHistoryDeviceState state, string modeName, AppLocationHistoryModeSettings? settings)
        {
            if (settings is null)
            {
                return;
            }

            state.Modes.Add(new RingAppLocationHistoryDeviceMode
            {
                Id = Guid.NewGuid(),
                RingAppLocationHistoryDeviceStateId = state.Id,
                ModeName = modeName,
                TransitionDelay = settings.TransitionDelay,
                Editable = settings.Editable,
                Removable = settings.Removable,
                ChirpsEnabled = settings.ChirpsEnabled,
                EntryDelay = settings.EntryDelay,
            });
        }

        private static void AddVisualization(RingLocationHistoryEvent evt, LocationHistoryMediaVisualization? visualization, string kind)
        {
            if (visualization is null)
            {
                return;
            }

            var row = new RingLocationHistoryVisualization
            {
                Id = Guid.NewGuid(),
                RingLocationHistoryEventId = evt.Id,
                Kind = kind,
                Schema = visualization.Schema,
            };
            foreach (LocationHistoryMedia media in visualization.Media ?? [])
            {
                row.Media.Add(MapMedia(media, row.Id));
            }

            evt.Visualizations.Add(row);
        }

        private static RingLocationHistoryMedia MapMedia(LocationHistoryMedia source, Guid visualizationId)
        {
            var media = new RingLocationHistoryMedia
            {
                Id = Guid.NewGuid(),
                RingLocationHistoryVisualizationId = visualizationId,
                Schema = source.Schema,
                Url = source.Url,
                StartTime = source.StartTime,
                EndTime = source.EndTime,
                IsE2ee = source.IsE2ee,
                ManifestId = source.ManifestId,
                FileType = source.FileType,
                FileFamily = source.FileFamily,
                PrerollDurationMs = source.PrerollDurationMs,
                PlaybackDuration = source.PlaybackDuration,
                Source = source.Source,
            };

            if (source.CustomMetadata is { } metadata)
            {
                var metadataRow = new RingLocationHistoryMediaMetadata
                {
                    Id = Guid.NewGuid(),
                    RingLocationHistoryMediaId = media.Id,
                    Recording24x7Enabled = metadata.Recording24x7Enabled,
                    DingId = metadata.DingId,
                    AudioEncoding = metadata.AudioEncoding,
                    VideoEncoding = metadata.VideoEncoding,
                    TbaTranscodingEnabled = metadata.TbaTranscodingEnabled,
                };
                int position = 0;
                foreach (LocationHistoryMediaTrack track in metadata.Tracks ?? [])
                {
                    metadataRow.Tracks.Add(new RingLocationHistoryMediaTrack
                    {
                        Id = Guid.NewGuid(),
                        RingLocationHistoryMediaMetadataId = metadataRow.Id,
                        Position = position++,
                        TrackId = track.Id,
                        Codec = track.Codec,
                        Type = track.Type,
                    });
                }

                media.Metadata = metadataRow;
            }

            return media;
        }

        private static RingEventDoorbot? MapDoorbot(Doorbot? source)
        {
            return source is null ? null : new RingEventDoorbot
            {
                Id = Guid.NewGuid(),
                DoorbotId = source.Id,
                Description = source.Description,
                Type = source.Type,
            };
        }

        private static RingEventDoorbot? MapDoorbot(LocationEventDoorbot? source)
        {
            return source is null ? null : new RingEventDoorbot
            {
                Id = Guid.NewGuid(),
                DoorbotId = source.Id,
                Description = source.Description,
                Type = source.Type,
            };
        }

        private static RingEventProperties? MapProperties(VideoSearchProperties? source)
        {
            return source is null ? null : new RingEventProperties
            {
                Id = Guid.NewGuid(),
                IsAlexa = source.IsAlexa,
                IsSidewalk = source.IsSidewalk,
                IsAutoreply = source.IsAutoreply,
                StarkReviewed = source.StarkReviewed,
                PackagePickupDetected = source.PackagePickupDetected,
                DetectionConfidence = source.DetectionConfidence,
            };
        }

        private static RingEventProperties? MapProperties(LocationEventProperties? source)
        {
            return source is null ? null : new RingEventProperties
            {
                Id = Guid.NewGuid(),
                IsAlexa = source.IsAlexa,
                IsSidewalk = source.IsSidewalk,
                IsAutoreply = source.IsAutoreply,
                StarkReviewed = source.StarkReviewed,
                PackagePickupDetected = source.PackagePickupDetected,
                DetectionConfidence = source.DetectionConfidence,
            };
        }

        private static RingEventDevicePlacement? MapPlacement(VideoSearchDevicePlacement? source)
        {
            return source is null ? null : new RingEventDevicePlacement { Id = Guid.NewGuid(), Room = source.Room, Area = source.Area, ZoneId = source.ZoneId };
        }

        private static RingEventDevicePlacement? MapPlacement(LocationEventDevicePlacement? source)
        {
            return source is null ? null : new RingEventDevicePlacement { Id = Guid.NewGuid(), Room = source.Room, Area = source.Area, ZoneId = source.ZoneId };
        }

        private static RingEventGeolocation? MapGeolocation(VideoSearchGeolocation? source)
        {
            return source is null ? null : new RingEventGeolocation
            {
                Id = Guid.NewGuid(),
                Latitude = source.Latitude,
                Longitude = source.Longitude,
                Accuracy = source.Accuracy,
                TimestampMs = source.Timestamp,
                LocationSource = source.Source,
            };
        }

        private static RingEventGeolocation? MapGeolocation(LocationEventGeolocation? source)
        {
            return source is null ? null : new RingEventGeolocation
            {
                Id = Guid.NewGuid(),
                Latitude = source.Latitude,
                Longitude = source.Longitude,
                Accuracy = source.Accuracy,
                TimestampMs = source.Timestamp,
                LocationSource = source.Source,
            };
        }

        private static RingEventSubscriptionReceipt? MapReceipt(VideoSearchSubscriptionReceipt? source)
        {
            return source is null ? null : new RingEventSubscriptionReceipt
            {
                Id = Guid.NewGuid(),
                SubscriptionId = source.SubscriptionId,
                SubscriptionType = source.SubscriptionType,
                CapturedAt = source.CapturedAt,
                ExpiresAt = source.ExpiresAt,
            };
        }

        private static RingEventSubscriptionReceipt? MapReceipt(LocationEventSubscriptionReceipt? source)
        {
            return source is null ? null : new RingEventSubscriptionReceipt
            {
                Id = Guid.NewGuid(),
                SubscriptionId = source.SubscriptionId,
                SubscriptionType = source.SubscriptionType,
                CapturedAt = source.CapturedAt,
                ExpiresAt = source.ExpiresAt,
            };
        }

        private static RingEventLastLocation? MapLastLocation(LocationEventLocation? source)
        {
            return source is null ? null : new RingEventLastLocation
            {
                Id = Guid.NewGuid(),
                Latitude = source.Latitude,
                Longitude = source.Longitude,
                Accuracy = source.Accuracy,
                TimestampMs = source.Timestamp,
                Name = source.Name,
            };
        }

        private static RingEventSiren? MapSiren(LocationEventSirenInfo? source)
        {
            return source is null ? null : new RingEventSiren
            {
                Id = Guid.NewGuid(),
                Armed = source.Armed,
                TriggeredAt = source.TriggeredAt,
                TriggerType = source.TriggerType,
                AlarmState = source.AlarmState,
            };
        }

        private static RingEventCvProperties? MapCv(CvProperties? source)
        {
            if (source is null)
            {
                return null;
            }

            RingEventCvProperties root = CreateCvRoot(source.PersonDetected, source.StreamBroken, source.DetectionType, source.FullDescription, source.ShortDescription, source.Similarity, source.Anomaly, source.DetectionTypes, source.SecurityAlerts, source.Profiles, source.Tags);
            if (source.DetectionDetails is { } details)
            {
                var detailRow = new RingEventCvDetectionDetail
                {
                    Id = Guid.NewGuid(),
                    RingEventCvPropertiesId = root.Id,
                    Position = 0,
                    Confidence = details.Confidence,
                    ModelVersion = details.ModelVersion,
                };
                foreach (CvZone zone in details.Zones ?? [])
                {
                    detailRow.Zones.Add(new RingEventCvDetectionZone
                    {
                        Id = Guid.NewGuid(),
                        RingEventCvDetectionDetailId = detailRow.Id,
                        Position = detailRow.Zones.Count,
                        ZoneId = zone.Id,
                        Name = zone.Name,
                        Confidence = zone.Confidence,
                    });
                }

                root.DetectionDetails.Add(detailRow);
            }

            return root;
        }

        private static RingEventCvProperties? MapLocationHistoryCv(LocationHistoryCv? source)
        {
            if (source is null)
            {
                return null;
            }

            RingEventCvProperties root = CreateCvRoot(source.PersonDetected, source.StreamBroken, source.DetectionType, source.FullDescription, source.ShortDescription, source.Similarity, source.Anomaly, source.DetectionTypes, source.SecurityAlerts, source.Profiles, source.Tags);
            foreach (LocationHistoryDetectionDetail detail in source.DetectionDetails ?? [])
            {
                root.DetectionDetails.Add(new RingEventCvDetectionDetail
                {
                    Id = Guid.NewGuid(),
                    RingEventCvPropertiesId = root.Id,
                    Position = root.DetectionDetails.Count,
                    TimestampMs = detail.Timestamp,
                    Type = detail.Type,
                });
            }

            return root;
        }

        private static RingEventCvProperties CreateCvRoot(
            bool? personDetected,
            bool? streamBroken,
            string? detectionType,
            string? fullDescription,
            string? shortDescription,
            double? similarity,
            double? anomaly,
            IEnumerable<CvDetectionType>? detectionTypes,
            CvSecurityAlerts? securityAlerts,
            IEnumerable<CvProfile>? profiles,
            IEnumerable<string>? tags)
        {
            var root = new RingEventCvProperties
            {
                Id = Guid.NewGuid(),
                PersonDetected = personDetected,
                StreamBroken = streamBroken,
                DetectionType = detectionType,
                FullDescription = fullDescription,
                ShortDescription = shortDescription,
                Similarity = similarity,
                Anomaly = anomaly,
            };

            foreach (CvDetectionType type in detectionTypes ?? [])
            {
                var typeRow = new RingEventCvDetectionType
                {
                    Id = Guid.NewGuid(),
                    RingEventCvPropertiesId = root.Id,
                    Position = root.DetectionTypes.Count,
                    DetectionType = type.DetectionType,
                };
                foreach (long timestamp in type.VerifiedTimestamps ?? [])
                {
                    typeRow.VerifiedTimestamps.Add(new RingEventCvVerifiedTimestamp
                    {
                        Id = Guid.NewGuid(),
                        RingEventCvDetectionTypeId = typeRow.Id,
                        Position = typeRow.VerifiedTimestamps.Count,
                        TimestampMs = timestamp,
                    });
                }

                root.DetectionTypes.Add(typeRow);
            }

            if (securityAlerts is { } alerts)
            {
                var alertRow = new RingEventCvSecurityAlert
                {
                    Id = Guid.NewGuid(),
                    RingEventCvPropertiesId = root.Id,
                    Severity = alerts.Severity,
                };
                foreach (string text in alerts.Alerts ?? [])
                {
                    alertRow.Alerts.Add(new RingEventCvSecurityAlertItem
                    {
                        Id = Guid.NewGuid(),
                        RingEventCvSecurityAlertId = alertRow.Id,
                        Position = alertRow.Alerts.Count,
                        AlertText = text,
                    });
                }

                root.SecurityAlerts.Add(alertRow);
            }

            foreach (CvProfile profile in profiles ?? [])
            {
                root.Profiles.Add(new RingEventCvProfile
                {
                    Id = Guid.NewGuid(),
                    RingEventCvPropertiesId = root.Id,
                    Position = root.Profiles.Count,
                    ProfileId = profile.Id,
                    Name = profile.Name,
                    Confidence = profile.Confidence,
                    ThumbnailUrl = profile.ThumbnailUrl,
                });
            }

            foreach (string tag in tags ?? [])
            {
                root.Tags.Add(new RingEventCvTag
                {
                    Id = Guid.NewGuid(),
                    RingEventCvPropertiesId = root.Id,
                    Position = root.Tags.Count,
                    Tag = tag,
                });
            }

            return root;
        }
    }
}
