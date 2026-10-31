using VideoForensics.Client.Ui.Shared.Services.Signal;
using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Client.Hosting.Signal
{
    /// <summary>
    /// Maps the bit rate analysis between its domain types and the wire DTOs (<c>ToDto</c> / <c>ToDomain</c>). The stats,
    /// bucket and deviation mappings are shared with <see cref="SignalAnalysisMapping"/>.
    /// </summary>
    public static class BitrateAnalysisMapping
    {
        public static BitrateSampleDto ToDto(this BitrateSample sample)
        {
            return new BitrateSampleDto(sample.CapturedAtUtc, sample.Kbps, sample.LightingMode, sample.Collapsed);
        }

        public static BitrateSample ToDomain(this BitrateSampleDto dto)
        {
            return new BitrateSample(dto.CapturedAtUtc, dto.Kbps, dto.LightingMode, dto.Collapsed);
        }

        /// <summary>The calendar's 168 cells, 24 hour profiles and 7 day profiles, in the order the DTO documents.</summary>
        public static BitrateDeviceAnalysisDto ToDto(this BitrateDeviceAnalysis device)
        {
            return new BitrateDeviceAnalysisDto(
                device.DeviceId,
                device.Name,
                device.ProviderName,
                device.ProviderDeviceId,
                device.AllTime.ToDto(),
                device.PeriodStats.ToDto(),
                device.Readings.Select(r => r.ToDto()).ToList(),
                device.Deviations.Select(w => w.ToDto()).ToList(),
                device.Calendar.CellList.Select(c => c.ToDto()).ToList(),
                device.Calendar.HourOfDayList.Select(c => c.ToDto()).ToList(),
                device.Calendar.DayOfWeekList.Select(c => c.ToDto()).ToList(),
                device.Hourly.Select(b => b.ToDto()).ToList(),
                device.Daily.Select(b => b.ToDto()).ToList(),
                device.Weekly.Select(b => b.ToDto()).ToList());
        }

        public static BitrateDeviceAnalysis ToDomain(this BitrateDeviceAnalysisDto dto)
        {
            return new BitrateDeviceAnalysis(
                dto.DeviceId,
                dto.Name,
                dto.ProviderName,
                dto.ProviderDeviceId,
                dto.AllTime.ToDomain(),
                dto.PeriodStats.ToDomain(),
                dto.Readings.Select(r => r.ToDomain()).ToList(),
                dto.Deviations.Select(w => w.ToDomain()).ToList(),
                new SignalCalendar(
                    dto.Cells.Select(c => c.ToDomain()).ToList(),
                    dto.HourOfDay.Select(c => c.ToDomain()).ToList(),
                    dto.DayOfWeek.Select(c => c.ToDomain()).ToList()),
                dto.Hourly.Select(b => b.ToDomain()).ToList(),
                dto.Daily.Select(b => b.ToDomain()).ToList(),
                dto.Weekly.Select(b => b.ToDomain()).ToList());
        }

        public static BitrateAnalysisDto ToDto(this BitrateAnalysis analysis)
        {
            return new BitrateAnalysisDto(analysis.Devices.Select(d => d.ToDto()).ToList());
        }

        public static BitrateAnalysis ToDomain(this BitrateAnalysisDto dto)
        {
            return new BitrateAnalysis(dto.Devices.Select(d => d.ToDomain()).ToList());
        }
    }
}
