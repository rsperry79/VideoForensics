namespace VideoForensics.Ui.Shared.Services
{
    /// <summary>
    /// Thrown when a destructive change needs a passkey, but the session can only confirm with a password. The operator
    /// is sent to passkey setup; the action is not performed.
    /// </summary>
    public sealed class PasskeyUpgradeRequiredException : InvalidOperationException
    {
        public PasskeyUpgradeRequiredException()
            : base("Your role requires a passkey. Add one in Settings, then try again.")
        {
        }
    }
}
