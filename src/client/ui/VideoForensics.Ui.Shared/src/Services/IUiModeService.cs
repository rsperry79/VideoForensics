namespace VideoForensics.Ui.Shared.Services
{
    /// <summary>
    /// Manages the active UI layout mode (Standard/Simple) for the signed-in operator,
    /// persisted in OperatorPreferences. Before sign-in, mode defaults to Standard.
    /// </summary>
    public interface IUiModeService
    {
        /// <summary>
        /// Gets the current UI mode ("Standard" or "Simple").
        /// </summary>
        string Mode { get; }

        /// <summary>
        
        /// <summary>
        /// Gets whether the UI mode is locked by an administrator.
        /// When locked, SetModeAsync will throw InvalidOperationException if an attempt is made to change the mode.
        /// </summary>
        bool IsLocked { get; }/// Fired when the UI mode changes or is initialized.
        /// </summary>
        event Action? OnChange;

        /// <summary>
        /// Initializes the UI mode service, loading the operator's stored mode preference
        /// if signed in, or using the default "Standard" mode if not.
        /// This method is idempotent and safe to call multiple times.
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Sets the UI mode to the specified value ("Standard" or "Simple")
        /// and persists it to OperatorPreferences if signed in.
        /// </summary>
        Task SetModeAsync(string mode);
    }
}
