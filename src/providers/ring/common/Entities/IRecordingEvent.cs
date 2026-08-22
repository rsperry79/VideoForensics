using System;

namespace VideoForensics.Providers.Ring.Entities;

/// <summary>
/// Represents a recorded event from a Ring device.
/// </summary>
public interface IRecordingEvent
{
    /// <summary>
    /// Gets the unique identifier of this event.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the device ID that captured this event.
    /// </summary>
    string DeviceId { get; }

    /// <summary>
    /// Gets when the event was captured.
    /// </summary>
    DateTime CapturedAt { get; }

    /// <summary>
    /// Gets the kind of event (motion, doorbell, etc.).
    /// </summary>
    string Kind { get; }

    /// <summary>
    /// Gets whether this event has an associated recording.
    /// </summary>
    bool HasRecording { get; }

    /// <summary>
    /// Gets the confidence level of detection if applicable (0-100).
    /// </summary>
    int? Confidence { get; }
}
