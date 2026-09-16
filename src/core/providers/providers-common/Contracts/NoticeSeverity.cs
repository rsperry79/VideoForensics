namespace VideoForensics.Providers.Common.Contracts
{
    /// <summary>Severity level of a notice event. Mapping: Critical=account/credential failures (e.g. decryption-failure notices), Alert=detection-type issues like possible jamming, Warning=other general issues, Info=default/informational.</summary>
    public enum NoticeSeverity { Info = 0, Warning = 1, Alert = 2, Critical = 3 }
}
