using VideoForensics.Client.Ui.Shared.Services.Signal;
using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Client.Hosting.Signal
{
    /// <summary>Maps the signal analysis between its domain types and the wire DTOs (<c>ToDto</c> / <c>ToDomain</c>).</summary>
    public static class SignalAnalysisMapping
    {
        public static SignalStatsDto ToDto(this SignalStats stats)
        {
            return new SignalStatsDto(stats.Count, stats.Min, stats.Max, stats.Average, stats.Latest, stats.PercentWeak, stats.StdDev);
        }

        public static SignalStats ToDomain(this SignalStatsDto dto)
        {
            return new SignalStats(dto.Count, dto.Min, dto.Max, dto.Average, dto.Latest, dto.PercentWeak, dto.StdDev);
        }

        public static SignalSampleDto ToDto(this SignalSample sample)
        {
            return new SignalSampleDto(sample.CapturedAtUtc, sample.RssiDbm);
        }

        public static SignalSample ToDomain(this SignalSampleDto dto)
        {
            return new SignalSample(dto.CapturedAtUtc, dto.RssiDbm);
        }

        public static SignalPeriodDto ToDto(this SignalPeriod period)
        {
            return new SignalPeriodDto(period.Kind.ToString(), period.StartUtc, period.EndUtc);
        }

        public static SignalPeriod ToDomain(this SignalPeriodDto dto)
        {
            return new SignalPeriod(Enum.Parse<SignalPeriodKind>(dto.Kind), dto.StartUtc, dto.EndUtc);
        }

        public static SignalDeviationDto ToDto(this SignalDeviationWindow window)
        {
            return new SignalDeviationDto(window.HourStartUtc, window.Level.ToString(), window.FlaggedReadings, window.WorstRssiDbm);
        }

        public static SignalDeviationWindow ToDomain(this SignalDeviationDto dto)
        {
            return new SignalDeviationWindow(dto.HourStartUtc, Enum.Parse<SignalDeviationLevel>(dto.Level), dto.FlaggedReadings, dto.WorstRssiDbm);
        }

        public static SignalBucketDto ToDto(this SignalBucketStats bucket)
        {
            return new SignalBucketDto(bucket.StartUtc, bucket.Stats.ToDto(), bucket.NoSignalCount);
        }

        public static SignalBucketStats ToDomain(this SignalBucketDto dto)
        {
            return new SignalBucketStats(dto.StartUtc, dto.Stats.ToDomain(), dto.NoSignalCount);
        }

        /// <summary>The calendar's 168 cells, 24 hour profiles and 7 day profiles, in the order the DTO documents.</summary>
        public static SignalDeviceAnalysisDto ToDto(this SignalDeviceAnalysis device)
        {
            return new SignalDeviceAnalysisDto(
                device.DeviceId,
                device.Name,
                device.ProviderName,
                device.ProviderDeviceId,
                device.AllTime.ToDto(),
                device.PeriodStats.ToDto(),
                device.Readings.Select(r => r.ToDto()).ToList(),
                device.Periods.Select(p => p.ToDto()).ToList(),
                device.Deviations.Select(w => w.ToDto()).ToList(),
                device.Calendar.CellList.Select(c => c.ToDto()).ToList(),
                device.Calendar.HourOfDayList.Select(c => c.ToDto()).ToList(),
                device.Calendar.DayOfWeekList.Select(c => c.ToDto()).ToList(),
                device.Hourly.Select(b => b.ToDto()).ToList(),
                device.Daily.Select(b => b.ToDto()).ToList(),
                device.Weekly.Select(b => b.ToDto()).ToList());
        }

        public static SignalDeviceAnalysis ToDomain(this SignalDeviceAnalysisDto dto)
        {
            return new SignalDeviceAnalysis(
                dto.DeviceId,
                dto.Name,
                dto.ProviderName,
                dto.ProviderDeviceId,
                dto.AllTime.ToDomain(),
                dto.PeriodStats.ToDomain(),
                dto.Readings.Select(r => r.ToDomain()).ToList(),
                dto.Periods.Select(p => p.ToDomain()).ToList(),
                dto.Deviations.Select(w => w.ToDomain()).ToList(),
                new SignalCalendar(
                    dto.Cells.Select(c => c.ToDomain()).ToList(),
                    dto.HourOfDay.Select(c => c.ToDomain()).ToList(),
                    dto.DayOfWeek.Select(c => c.ToDomain()).ToList()),
                dto.Hourly.Select(b => b.ToDomain()).ToList(),
                dto.Daily.Select(b => b.ToDomain()).ToList(),
                dto.Weekly.Select(b => b.ToDomain()).ToList());
        }

        public static SignalAnalysisDto ToDto(this SignalAnalysis analysis)
        {
            return new SignalAnalysisDto(analysis.Devices.Select(d => d.ToDto()).ToList());
        }

        public static SignalAnalysis ToDomain(this SignalAnalysisDto dto)
        {
            return new SignalAnalysis(dto.Devices.Select(d => d.ToDomain()).ToList());
        }
    }
}
