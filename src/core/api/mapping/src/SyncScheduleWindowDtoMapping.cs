using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>Maps jamming schedule windows between the data layer and their DTO.</summary>
    public static class JammingScheduleWindowDtoMapping
    {
        /// <summary>Converts a JammingScheduleWindow entity to its DTO.</summary>
        public static JammingScheduleWindowDto ToDto(this JammingScheduleWindow entity) =>
            new(entity.Id, entity.DayOfWeek, entity.StartMinuteOfDay, entity.EndMinuteOfDay, entity.SnapshotRssiIntervalMinutes);

        /// <summary>Converts a JammingScheduleWindowDto to an entity owned by the given sync schedule.</summary>
        public static JammingScheduleWindow ToDomain(this JammingScheduleWindowDto dto, Guid syncScheduleId) => new()
        {
            Id = dto.Id,
            SyncScheduleId = syncScheduleId,
            DayOfWeek = dto.DayOfWeek,
            StartMinuteOfDay = dto.StartMinuteOfDay,
            EndMinuteOfDay = dto.EndMinuteOfDay,
            SnapshotRssiIntervalMinutes = dto.SnapshotRssiIntervalMinutes,
        };
    }
}
