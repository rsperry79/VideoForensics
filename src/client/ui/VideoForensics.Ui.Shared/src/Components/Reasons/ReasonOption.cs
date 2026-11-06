namespace VideoForensics.Client.Ui.Shared.Components.Reasons
{
    /// <summary>One row of a <see cref="ReasonPicker{TReason}"/> dropdown: the enum value and its localized text.</summary>
    /// <param name="Value">The enum value the row stands for.</param>
    /// <param name="Text">The localized text shown for the value.</param>
    public sealed record ReasonOption<TReason>(TReason Value, string Text);
}
