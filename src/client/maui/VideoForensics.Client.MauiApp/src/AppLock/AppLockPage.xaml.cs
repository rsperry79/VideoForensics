using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.Client.MauiApp.AppLock
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
                    // No Windows Hello or PIN on this device, so the OS cannot verify the user here. The lock
                    // steps aside and username/password sign-in becomes the gate. With Hello set up, the lock applies as before.
                    StartupTrace.Mark("app lock skipped: no Windows Hello or PIN on this device");
                    Unlocked?.Invoke(this, EventArgs.Empty);
                    return;
                }

                bool success = await _authGate.AuthenticateAsync(_reason, CancellationToken.None);
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
