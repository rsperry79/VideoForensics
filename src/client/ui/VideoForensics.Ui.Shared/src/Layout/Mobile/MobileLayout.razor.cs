using VideoForensics.Ui.Shared.Services.Time;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Extensions;

namespace VideoForensics.Ui.Shared.Layout.Mobile
{
    public partial class MobileLayout : IDisposable
    {
        private OperatorRole? _role;
        private string _currentPath = "/";
        private string _currentQuery = "";

        private int _undismissedCount = 0;
        private bool _noticesDropdownOpen = false;
        private List<Notice> _noticesList = [];

        // Mirrors SessionState.PasskeySetupOnly (limited create-passkey session): hides all navigation and the settings sheet.
        private bool _passkeySetupOnly;
        private bool _navDrawerOpen = false;
        private bool _settingsSheetOpen = false;

        private bool IsAdmin => _role.HasValue && _role >= OperatorRole.Admin;

        private IReadOnlyList<NavGroup> VisibleGroups
        {
            get
            {
                if (_passkeySetupOnly)
                {
                    return [];
                }

                return NavGroups.All.Where(g => g.IsVisible(BuildContext())).ToList();
            }
        }

        private NavGroup ActiveGroup => NavGroups.ActiveGroupFor(VisibleGroups, BuildContext(), _currentPath, _currentQuery);

        private IReadOnlyList<NavItem> ActiveGroupItems
        {
            get
            {
                NavContext ctx = BuildContext();
                return ActiveGroup.Items.Where(i => i.IsVisible(ctx)).ToList();
            }
        }

        protected override void OnInitialized()
        {
            SetCurrentLocation(Nav.Uri);
            _passkeySetupOnly = SessionState.PasskeySetupOnly;
            Nav.LocationChanged += OnLocationChanged;
            ThemeService.OnChange += StateHasChanged;
            SessionState.AuthenticationExpired += OnAuthenticationExpired;
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                // IJSRuntime is unavailable during Blazor's static prerender pass, so session/layout
                // state can only be resolved once the circuit is connected.
                await SessionState.EnsureLoadedAsync();
                _role = SessionState.Role is not null && Enum.TryParse<OperatorRole>(SessionState.Role, out OperatorRole role) ? role : null;
                _passkeySetupOnly = SessionState.PasskeySetupOnly;

                await ThemeService.InitializeAsync();

                // Load undismissed notice count for badge
                if (SessionState.OperatorId.HasValue)
                {
                    _undismissedCount = await NoticeRepository.CountUndismissedForOperatorAsync(SessionState.OperatorId.Value, IsAdmin, CancellationToken.None);
                }

                StateHasChanged();
            }
        }

        private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
        {
            SetCurrentLocation(e.Location);
            _passkeySetupOnly = SessionState.PasskeySetupOnly;
            _ = InvokeAsync(StateHasChanged);
        }

        private void OnAuthenticationExpired()
        {
            _ = InvokeAsync(() =>
            {
                Nav.NavigateTo(Nav.SignInPathWithReturnUrl());
                StateHasChanged();
            });
        }

        private void SetCurrentLocation(string uri)
        {
            var parsed = new Uri(uri);
            _currentPath = string.IsNullOrEmpty(parsed.AbsolutePath) ? "/" : parsed.AbsolutePath;
            _currentQuery = parsed.Query;
        }

        private bool PathMatches(string path) => NavPathMatcher.Matches(_currentPath, _currentQuery, path);

        private NavContext BuildContext()
        {
            return new(SessionState.IsSignedIn, _role, AppLockPreferences.IsSupported,
                IsLocalTier: SessionState.IsLocalTier, IsPasskeySession: SessionState.IsPasskeySession);
        }

        private async Task ToggleNavDrawerAsync()
        {
            _navDrawerOpen = !_navDrawerOpen;
            await Task.CompletedTask;
            StateHasChanged();
        }

        private async Task CloseNavDrawerAsync()
        {
            _navDrawerOpen = false;
            await Task.CompletedTask;
            StateHasChanged();
        }

        private async Task ToggleSettingsSheetAsync()
        {
            _settingsSheetOpen = !_settingsSheetOpen;
            await Task.CompletedTask;
            StateHasChanged();
        }

        private async Task CloseSettingsSheetAsync()
        {
            _settingsSheetOpen = false;
            await Task.CompletedTask;
            StateHasChanged();
        }

        private async Task SignOutAsync()
        {
            await SessionState.ClearAsync();
            Nav.NavigateTo("/signin");
        }

        private async Task ToggleNoticesAsync()
        {
            _noticesDropdownOpen = !_noticesDropdownOpen;
            if (_noticesDropdownOpen && SessionState.OperatorId.HasValue)
            {
                await LoadNoticesForDropdownAsync();
            }
            else if (!_noticesDropdownOpen && SessionState.OperatorId.HasValue)
            {
                _undismissedCount = await NoticeRepository.CountUndismissedForOperatorAsync(SessionState.OperatorId.Value, IsAdmin, CancellationToken.None);
            }

            StateHasChanged();
        }

        private async Task LoadNoticesForDropdownAsync()
        {
            if (!SessionState.OperatorId.HasValue)
            {
                return;
            }

            _noticesList = (await NoticeRepository.ListForOperatorAsync(SessionState.OperatorId.Value, IsAdmin, includeDismissed: false, CancellationToken.None))
                .OrderByDescending(n => n.TimestampUtc)
                .Take(5)
                .ToList();
        }

        private async Task DismissNoticeAsync(Guid noticeId)
        {
            if (!SessionState.OperatorId.HasValue)
            {
                return;
            }

            await NoticeDismissalRepository.DismissAsync(noticeId, SessionState.OperatorId.Value, CancellationToken.None);
            await LoadNoticesForDropdownAsync();
            _undismissedCount = await NoticeRepository.CountUndismissedForOperatorAsync(SessionState.OperatorId.Value, IsAdmin, CancellationToken.None);
            StateHasChanged();
        }

        private async Task DismissAllNoticesAsync()
        {
            if (!SessionState.OperatorId.HasValue)
            {
                return;
            }

            // Dismiss every undismissed notice, not just the 5 shown in this dropdown.
            var allUndismissed = await NoticeRepository.ListForOperatorAsync(SessionState.OperatorId.Value, IsAdmin, includeDismissed: false, CancellationToken.None);
            await NoticeDismissalRepository.DismissAllAsync(allUndismissed.Select(n => n.Id), SessionState.OperatorId.Value, CancellationToken.None);
            await LoadNoticesForDropdownAsync();
            _undismissedCount = await NoticeRepository.CountUndismissedForOperatorAsync(SessionState.OperatorId.Value, IsAdmin, CancellationToken.None);
            StateHasChanged();
        }

        private async Task GoToAllNoticesAsync()
        {
            _noticesDropdownOpen = false;
            Nav.NavigateTo("/notices");
            await Task.CompletedTask;
        }

        private string GetSeverityBadgeClass(VideoForensics.Core.Providers.Common.Contracts.NoticeSeverity severity)
        {
            return severity switch
            {
                VideoForensics.Core.Providers.Common.Contracts.NoticeSeverity.Info => "severity-badge-info",
                VideoForensics.Core.Providers.Common.Contracts.NoticeSeverity.Warning => "severity-badge-warning",
                VideoForensics.Core.Providers.Common.Contracts.NoticeSeverity.Alert => "severity-badge-alert",
                VideoForensics.Core.Providers.Common.Contracts.NoticeSeverity.Critical => "severity-badge-critical",
                _ => "severity-badge-info"
            };
        }

        private string GetSeverityLabel(VideoForensics.Core.Providers.Common.Contracts.NoticeSeverity severity)
        {
            return severity switch
            {
                VideoForensics.Core.Providers.Common.Contracts.NoticeSeverity.Info => "Info",
                VideoForensics.Core.Providers.Common.Contracts.NoticeSeverity.Warning => "Warning",
                VideoForensics.Core.Providers.Common.Contracts.NoticeSeverity.Alert => "Alert",
                VideoForensics.Core.Providers.Common.Contracts.NoticeSeverity.Critical => "Critical",
                _ => "Info"
            };
        }

        [CascadingParameter]
        public UserTimeZone? UserTz { get; set; }

        private UserTimeZone Zone => UserTz ?? new UserTimeZone(TimeZoneInfo.Local);

        private string GetRelativeTime(DateTime utcTime)
        {
            TimeSpan elapsed = DateTime.UtcNow - utcTime;
            return elapsed.TotalSeconds < 60
                ? L["RelativeJustNow"].Value
                : elapsed.TotalMinutes < 60
                ? L["RelativeMinutesAgo", (int)elapsed.TotalMinutes].Value
                : elapsed.TotalHours < 24
                ? L["RelativeHoursAgo", (int)elapsed.TotalHours].Value
                : elapsed.TotalDays < 7 ? L["RelativeDaysAgo", (int)elapsed.TotalDays].Value : Zone.ToLocal(utcTime).ToString("MMM d, yyyy", System.Globalization.CultureInfo.CurrentUICulture);
        }

        public void Dispose()
        {
            Nav.LocationChanged -= OnLocationChanged;
            ThemeService.OnChange -= StateHasChanged;
            SessionState.AuthenticationExpired -= OnAuthenticationExpired;
        }
    }
}
