using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

using VideoForensics.Data.Common.Entities;

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

        private bool _navDrawerOpen = false;
        private bool _settingsSheetOpen = false;

        private bool IsAdmin => _role.HasValue && _role >= OperatorRole.Admin;

        private IReadOnlyList<NavGroup> VisibleGroups => NavGroups.All.Where(g => g.IsVisible(BuildContext())).ToList();

        private NavGroup ActiveGroup
        {
            get
            {
                IReadOnlyList<NavGroup> visible = VisibleGroups;
                NavContext ctx = BuildContext();
                NavGroup? match = visible.FirstOrDefault(g => g.Items.Any(i => i.IsVisible(ctx) && PathMatches(i.Path)));
                return match ?? visible.FirstOrDefault() ?? NavGroups.All[0];
            }
        }

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
            _ = InvokeAsync(StateHasChanged);
        }

        private void OnAuthenticationExpired()
        {
            _ = InvokeAsync(() =>
            {
                Nav.NavigateTo("/device-signin");
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
            return new(SessionState.IsSignedIn, _role, AppLockPreferences.IsSupported);
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

        private string GetSeverityBadgeClass(VideoForensics.Providers.Common.Contracts.NoticeSeverity severity)
        {
            return severity switch
            {
                VideoForensics.Providers.Common.Contracts.NoticeSeverity.Info => "severity-badge-info",
                VideoForensics.Providers.Common.Contracts.NoticeSeverity.Warning => "severity-badge-warning",
                VideoForensics.Providers.Common.Contracts.NoticeSeverity.Alert => "severity-badge-alert",
                VideoForensics.Providers.Common.Contracts.NoticeSeverity.Critical => "severity-badge-critical",
                _ => "severity-badge-info"
            };
        }

        private string GetSeverityLabel(VideoForensics.Providers.Common.Contracts.NoticeSeverity severity)
        {
            return severity switch
            {
                VideoForensics.Providers.Common.Contracts.NoticeSeverity.Info => "Info",
                VideoForensics.Providers.Common.Contracts.NoticeSeverity.Warning => "Warning",
                VideoForensics.Providers.Common.Contracts.NoticeSeverity.Alert => "Alert",
                VideoForensics.Providers.Common.Contracts.NoticeSeverity.Critical => "Critical",
                _ => "Info"
            };
        }

        private string GetRelativeTime(DateTime utcTime)
        {
            TimeSpan elapsed = DateTime.UtcNow - utcTime;
            return elapsed.TotalSeconds < 60
                ? "just now"
                : elapsed.TotalMinutes < 60
                ? $"{(int)elapsed.TotalMinutes}m ago"
                : elapsed.TotalHours < 24
                ? $"{(int)elapsed.TotalHours}h ago"
                : elapsed.TotalDays < 7 ? $"{(int)elapsed.TotalDays}d ago" : utcTime.ToString("MMM d, yyyy");
        }

        public void Dispose()
        {
            Nav.LocationChanged -= OnLocationChanged;
            ThemeService.OnChange -= StateHasChanged;
            SessionState.AuthenticationExpired -= OnAuthenticationExpired;
        }
    }
}
