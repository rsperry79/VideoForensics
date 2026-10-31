using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Hosting.Signal
{
    /// <summary>
    /// Maps media flags between their domain types and the wire DTOs (<c>ToDto</c> / <c>ToDomain</c>). Enums cross the wire as
    /// their names; an unknown name is an <see cref="ArgumentException"/> so the server can answer 400 and a client can fail loudly.
    /// </summary>
    public static class MediaFlagMapping
    {
        public static MediaFlagDto ToDto(this MediaFlag flag)
        {
            ArgumentNullException.ThrowIfNull(flag);
            return new MediaFlagDto(
                flag.Id,
                flag.FlagType.ToString(),
                flag.DeviceId,
                flag.MediaItemId,
                flag.WindowStartUtc,
                flag.WindowEndUtc,
                flag.RaisedAtUtc,
                flag.Severity.ToString(),
                flag.Status.ToString(),
                flag.ReviewedBy,
                flag.ReviewedAtUtc,
                flag.ReviewNote,
                flag.NoticeId,
                flag.RowVersion);
        }

        public static MediaFlag ToDomain(this MediaFlagDto dto)
        {
            ArgumentNullException.ThrowIfNull(dto);
            return new MediaFlag
            {
                Id = dto.Id,
                FlagType = ParseEnum<MediaFlagType>(dto.FlagType),
                DeviceId = dto.DeviceId,
                MediaItemId = dto.MediaItemId,
                WindowStartUtc = dto.WindowStartUtc,
                WindowEndUtc = dto.WindowEndUtc,
                RaisedAtUtc = dto.RaisedAtUtc,
                Severity = ParseEnum<MediaFlagSeverity>(dto.Severity),
                Status = ParseEnum<MediaFlagStatus>(dto.Status),
                ReviewedBy = dto.ReviewedBy,
                ReviewedAtUtc = dto.ReviewedAtUtc,
                ReviewNote = dto.ReviewNote,
                NoticeId = dto.NoticeId,
                RowVersion = dto.RowVersion,
            };
        }

        public static MediaFlagPageDto ToDto(this MediaFlagPage page)
        {
            ArgumentNullException.ThrowIfNull(page);
            return new MediaFlagPageDto(page.Items.Select(f => f.ToDto()).ToList(), page.TotalCount);
        }

        public static MediaFlagPage ToDomain(this MediaFlagPageDto dto)
        {
            ArgumentNullException.ThrowIfNull(dto);
            return new MediaFlagPage(dto.Items.Select(f => f.ToDomain()).ToList(), dto.TotalCount);
        }

        public static MediaFlagFilterDto ToDto(this MediaFlagFilter filter)
        {
            ArgumentNullException.ThrowIfNull(filter);
            return new MediaFlagFilterDto(
                filter.Types?.Select(t => t.ToString()).ToList(),
                filter.Statuses?.Select(s => s.ToString()).ToList(),
                filter.DeviceIds?.ToList(),
                filter.FromUtc,
                filter.ToUtc);
        }

        /// <summary>Builds the domain filter. Throws <see cref="ArgumentException"/> on an unknown flag type or status name.</summary>
        public static MediaFlagFilter ToDomain(this MediaFlagFilterDto dto)
        {
            ArgumentNullException.ThrowIfNull(dto);
            return new MediaFlagFilter
            {
                Types = dto.Types?.Select(ParseEnum<MediaFlagType>).ToList(),
                Statuses = dto.Statuses?.Select(ParseEnum<MediaFlagStatus>).ToList(),
                DeviceIds = dto.DeviceIds?.ToList(),
                FromUtc = dto.FromUtc,
                ToUtc = dto.ToUtc,
            };
        }

        public static FlagStatusConflictDto ToDto(this FlagStatusConflictException ex)
        {
            ArgumentNullException.ThrowIfNull(ex);
            return new FlagStatusConflictDto(
                ex.ExpectedStatus.ToString(),
                ex.ActualStatus.ToString(),
                ex.ReviewedBy,
                ex.ReviewedAtUtc);
        }

        /// <summary>Rebuilds the conflict on the client, using the flag id the caller asked about.</summary>
        public static FlagStatusConflictException ToDomain(this FlagStatusConflictDto dto, Guid flagId)
        {
            ArgumentNullException.ThrowIfNull(dto);
            return new FlagStatusConflictException(
                flagId,
                ParseEnum<MediaFlagStatus>(dto.ExpectedStatus),
                ParseEnum<MediaFlagStatus>(dto.ActualStatus),
                dto.ReviewedBy,
                dto.ReviewedAtUtc);
        }

        private static T ParseEnum<T>(string value) where T : struct, Enum
        {
            if (!Enum.TryParse(value, ignoreCase: false, out T parsed) || !Enum.IsDefined(parsed))
            {
                throw new ArgumentException($"Unknown {typeof(T).Name} value '{value}'.");
            }

            return parsed;
        }
    }
}
