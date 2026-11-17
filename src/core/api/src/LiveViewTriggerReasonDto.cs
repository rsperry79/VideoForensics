namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>Why a live-view session was started, as sent on the wire. Members match the data-layer enum by name.</summary>
    public enum LiveViewTriggerReasonDto
    {
        /// <summary>Started manually by an operator.</summary>
        Manual,
        /// <summary>Started because jamming is suspected.</summary>
        JammingSuspected,
        /// <summary>Started because jamming is confirmed.</summary>
        JammingConfirmed,
        /// <summary>Started for calibration.</summary>
        Calibration
    }
}
