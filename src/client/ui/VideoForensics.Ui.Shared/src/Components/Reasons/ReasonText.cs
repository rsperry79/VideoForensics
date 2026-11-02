namespace VideoForensics.Client.Ui.Shared.Components.Reasons
{
    /// <summary>Builds the reason text sent to the server from a <see cref="ReasonPicker{TReason}"/> choice.</summary>
    public static class ReasonText
    {
        /// <summary>
        /// The enum name of the reason. The Other reason appends ": " and the trimmed note, so the note is stored with the reason.
        /// The same format as the flag Dismiss reason.
        /// </summary>
        /// <typeparam name="TReason">The reason enum.</typeparam>
        /// <param name="reason">The chosen reason.</param>
        /// <param name="other">The reason that needs a note.</param>
        /// <param name="note">The note typed for the Other reason. Ignored for every other reason.</param>
        /// <returns>The text stored with the reason.</returns>
        public static string Build<TReason>(TReason reason, TReason other, string? note) where TReason : struct, Enum
        {
            return EqualityComparer<TReason>.Default.Equals(reason, other)
                ? $"{reason}: {(note ?? string.Empty).Trim()}"
                : reason.ToString();
        }
    }
}
