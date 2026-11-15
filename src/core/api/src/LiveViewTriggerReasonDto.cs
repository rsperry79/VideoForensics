namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>Why a live-view session was started, as sent on the wire. Members match the data-layer enum by name.</summary>
    public enum LiveViewTriggerReasonDto
    {
        Manual,
        JammingSuspected,
        JammingConfirmed,
        Calibration
    }
}
