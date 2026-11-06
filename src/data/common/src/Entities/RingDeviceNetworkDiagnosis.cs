namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// Ring-only network diagnosis: <c>network_settings.network_diagnosis</c> (1:1 with the device, replaced on each sync that carries the object).
    /// Every value was null in every captured sample, so every column type below is a GUESS (noted per property). A non-null value whose JSON type
    /// does not fit its column is not stored and is logged as an unstored note.
    /// </summary>
    public class RingDeviceNetworkDiagnosis
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }

        /// <summary>network_diagnosis.channel_analysis. Type is a GUESS (boolean); null in every sample.</summary>
        public bool? ChannelAnalysis { get; set; }

        /// <summary>network_diagnosis.periodic_diagnosis. Type is a GUESS (boolean); null in every sample.</summary>
        public bool? PeriodicDiagnosis { get; set; }

        /// <summary>network_diagnosis.lan_performance. Type is a GUESS (boolean); null in every sample.</summary>
        public bool? LanPerformance { get; set; }

        /// <summary>network_diagnosis.tcp_iperf. Type is a GUESS (boolean); null in every sample.</summary>
        public bool? TcpIperf { get; set; }

        /// <summary>network_diagnosis.udp_iperf. Type is a GUESS (boolean); null in every sample.</summary>
        public bool? UdpIperf { get; set; }

        /// <summary>network_diagnosis.tcp_host. Type is a GUESS (text); null in every sample.</summary>
        public string? TcpHost { get; set; }

        /// <summary>network_diagnosis.udp_host. Type is a GUESS (text); null in every sample.</summary>
        public string? UdpHost { get; set; }

        /// <summary>network_diagnosis.tcp_port. Type is a GUESS (integer); null in every sample.</summary>
        public int? TcpPort { get; set; }

        /// <summary>network_diagnosis.udp_port. Type is a GUESS (integer); null in every sample.</summary>
        public int? UdpPort { get; set; }

        /// <summary>network_diagnosis.udp_bandwidth. Type is a GUESS (integer, unit not documented by Ring); null in every sample.</summary>
        public long? UdpBandwidth { get; set; }

        /// <summary>UTC time this row was written by a sync (not a Ring value).</summary>
        public DateTime UpdatedAtUtc { get; set; }
    }
}
