using VideoForensics.Client.Common;

namespace VideoForensics.MauiApp.AppLock
{
    /// <summary>
    /// Native (non-Blazor) lock screen shown on cold launch and after the configured idle timeout
    /// (plan §5.9). Deliberately native rather than a Blazor page: the whole point is gating access
    /// to the BlazorWebView's content, so the gate itself can't depend on that content already
    /// being safe to render.
    /// </summary>
    public partial class AppLockPage : ContentPage
    {
        private readonly ILocalAuthGate _authGate;
        private readonly string _reason;

        /// <summary>Raised once authentication succeeds - App.xaml.cs swaps the window's page in response.</summary>
        public event EventHandler? Unlocked;

        public AppLockPage(ILocalAuthGate authGate, string reason = "Unlock VideoForensics")
        {
            InitializeComponent();
            _authGate = authGate;
            _reason = reason;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await TryUnlockAsync();
        }

        private async void OnUnlockClicked(object? sender, EventArgs e)
        {
            await TryUnlockAsync();
        }

        private async Task TryUnlockAsync()
        {
            ErrorLabel.IsVisible = false;
            UnlockButton.IsEnabled = false;
            try
            {
                if (!await _authGate.IsAvailableAsync())
                {
                    // No biometric/PIN enrolled at the OS level and no fallback available - failing
                    // open here would defeat the point of app-lock, so this stays locked with an
                    // explanation rather than silently granting access.
                    ErrorLabel.Text = "No Windows Hello or PIN is configured on this device. Set one up in Windows Settings to use VideoForensics.";
                    ErrorLabel.IsVisible = true;
                    return;
                }

                var success = await _authGate.AuthenticateAsync(_reason, CancellationToken.None);
                if (success)
                {
                    Unlocked?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    ErrorLabel.Text = "Verification failed or was cancelled.";
                    ErrorLabel.IsVisible = true;
                }
            }
            finally
            {
                UnlockButton.IsEnabled = true;
            }
        }
    }
}
