namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// Provider-agnostic network settings of a device: the <c>network_settings</c> object of a settings block (1:1 with the device, replaced
    /// on each sync that carries it). Null means the provider did not send the value. The network diagnosis is in
    /// <see cref="RingDeviceNetworkDiagnosis"/>.
    /// </summary>
    public class DeviceNetworkSettings
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }

        /// <summary>network_settings.mac_address_ble. Stored as Ring sends it.</summary>
        public string? MacAddressBle { get; set; }

        /// <summary>network_settings.mac_address_wifi_24. Stored as Ring sends it.</summary>
        public string? MacAddressWifi24 { get; set; }

        /// <summary>network_settings.mac_address_wifi_5. Stored as Ring sends it.</summary>
        public string? MacAddressWifi5 { get; set; }

        /// <summary>
        /// network_settings.max_dynamic_listen_interval. Null in every captured sample, so the text type is a GUESS. Confirm against a real capture
        /// before relying on the column type.
        /// </summary>
        public string? MaxDynamicListenInterval { get; set; }

        /// <summary>network_settings.multi_net_pref.</summary>
        public int? MultiNetPref { get; set; }

        /// <summary>
        /// network_settings.data_restriction_enabled. Null in every captured sample, so the boolean type is a GUESS. Confirm against a real capture
        /// before relying on the column type.
        /// </summary>
        public bool? DataRestrictionEnabled { get; set; }

        /// <summary>UTC time this row was written by a sync (not a Ring value).</summary>
        public DateTime UpdatedAtUtc { get; set; }
    }
}
