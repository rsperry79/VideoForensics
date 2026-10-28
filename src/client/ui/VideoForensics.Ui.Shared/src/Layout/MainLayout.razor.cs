using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

using Syncfusion.Blazor.Layouts;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Ui.Shared.Extensions;
using VideoForensics.Client.Ui.Shared.Services.Time;

namespace VideoForensics.Client.Ui.Shared.Layout
{
    public partial class MainLayout : IDisposable
    {
        private OperatorRole? _role;
        private string _currentPath = "/";
        private string _currentQuery = string.Empty;
        private SfSplitter? _outerSplitter;
        private double _leftNavWidth = 220;
        private double _rightPanelWidth = 280;
        private bool _rightPanelHandleAttached;
        // Mirrors SessionState.PasskeySetupOnly (limited create-passkey session): hides all navigation and the settings panel.
        private bool _passkeySetupOnly;
        private DotNetObjectReference<MainLayout>? _selfRef;
        // Set when the saved left-nav collapse could not be applied yet (splitter JS not ready, e.g. after a
        // circuit reconnect). Cleared on success or by a user toggle; retried on each later render.
        private bool _leftNavRestorePending;
        // Actual collapse state of the left-nav pane. Tracked separately from LayoutPrefs, because pages such as
        // /media collapse the pane without changing the saved preference.
        private bool _leftNavPaneCollapsed;
        // Set on every location change; the pane is brought in line with the current page on the next render.
        private bool _leftNavSyncPending = true;

        private int _undismissedCount = 0;
        private bool _noticesDropdownOpen = false;
        private List<Notice> _noticesList = [];

        private bool IsAdmin => _role.HasValue && _role >= OperatorRole.Admin;

        private readonly string _rightPanelHandleId = $"vf-right-handle-{Guid.NewGuid():N}";
        private readonly string _rightPanelId = $"vf-right-panel-{Guid.NewGuid():N}";

        // Left-nav is pane 0 of the outer splitter (see MainLayout.razor). The right panel is not
        // a Splitter pane at all - see the comment above it in MainLayout.razor.
        private const int LeftPaneIndex = 0;
        private const double RightPanelMinWidth = 150;
        private const double RightPanelMaxWidth = 500;

        private string RightPanelStyle => $"flex: 0 0 {_rightPanelWidth}px; width: {_rightPanelWidth}px;";

        private IReadOnlyList<NavGroup> VisibleGroups => _passkeySetupOnly ? [] : (IReadOnlyList<NavGroup>)NavGroups.All.Where(g => g.IsVisible(BuildContext())).ToList();

        private NavGroup ActiveGroup => NavGroups.ActiveGroupFor(VisibleGroups, BuildContext(), _currentPath, _currentQuery);

        private IReadOnlyList<NavSection> ActiveGroupSections => _passkeySetupOnly ? [] : NavGroups.SectionsFor(ActiveGroup, BuildContext());

        protected override void OnInitialized()
        {
            SetCurrentLocation(Nav.Uri);
            _leftNavSyncPending = true;
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

                await LayoutPrefs.EnsureLoadedAsync();
                await ThemeService.InitializeAsync();

                // Load saved panel sizes
                if (LayoutPrefs.LeftNavWidth > 0)
                {
                    _leftNavWidth = LayoutPrefs.LeftNavWidth;
                }

                if (LayoutPrefs.RightPanelWidth > 0)
                {
                    _rightPanelWidth = LayoutPrefs.RightPanelWidth;
                }

                // Load undismissed notice count for badge
                if (SessionState.OperatorId.HasValue)
                {
                    _undismissedCount = await NoticeRepository.CountUndismissedForOperatorAsync(SessionState.OperatorId.Value, IsAdmin, CancellationToken.None);
                }

                StateHasChanged();

                // SplitterPane.Size is an initial-only value (per Syncfusion docs); once the
                // splitter's JS widget has initialized, collapse state must be driven imperatively
                // via CollapseAsync/ExpandAsync rather than by re-binding Size or a CssClass.
                _leftNavRestorePending = LayoutPrefs.LeftNavCollapsed;
            }

            if (_leftNavRestorePending && _outerSplitter is not null)
            {
                await TryRestoreLeftNavCollapseAsync(_outerSplitter);
            }

            if (_leftNavSyncPending && _outerSplitter is not null)
            {
                await SyncLeftNavForPathAsync(_outerSplitter);
            }

            await AttachRightPanelHandleIfNeededAsync();
        }

        /// <summary>
        /// Applies the saved left-nav collapse. A JS failure means the splitter's JS widget is not ready yet;
        /// that is logged and left pending for the next render. Other exceptions propagate.
        /// </summary>
        private async Task TryRestoreLeftNavCollapseAsync(SfSplitter splitter)
        {
            try
            {
                await SetLeftNavPaneCollapsedAsync(splitter, true);
                _leftNavRestorePending = false;
            }
            catch (Exception ex) when (IsSplitterNotReady(ex))
            {
                // Deliberately no exception detail in the log: keep tokens and URLs out of the log output.
                Logger.LogDebug("Left-nav collapse restore deferred: splitter not ready. Will retry on next render.");
            }
        }

        /// <summary>
        /// Pages in <see cref="AutoCollapsePaths"/> hide the left nav. Every other page shows the saved preference.
        /// The auto-collapse is transient: it changes the pane, never <see cref="LayoutPreferencesState"/>.
        /// </summary>
        private static readonly string[] AutoCollapsePaths = ["/media", "/media/review"];

        private static bool IsAutoCollapsePath(string path) => AutoCollapsePaths.Contains(path, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Brings the left-nav pane in line with the current page. On an auto-collapse page the pane is collapsed;
        /// elsewhere it follows the saved preference. A JS failure means the splitter is not ready; the sync stays
        /// pending and runs again on the next render.
        /// </summary>
        private async Task SyncLeftNavForPathAsync(SfSplitter splitter)
        {
            bool wantCollapsed = IsAutoCollapsePath(_currentPath) || LayoutPrefs.LeftNavCollapsed;
            try
            {
                if (wantCollapsed != _leftNavPaneCollapsed)
                {
                    await SetLeftNavPaneCollapsedAsync(splitter, wantCollapsed);
                }

                _leftNavSyncPending = false;
            }
            catch (Exception ex) when (IsSplitterNotReady(ex))
            {
                Logger.LogDebug("Left-nav page sync deferred: splitter not ready. Will retry on next render.");
            }
        }

        private async Task SetLeftNavPaneCollapsedAsync(SfSplitter splitter, bool collapsed)
        {
            if (collapsed)
            {
                await splitter.CollapseAsync(LeftPaneIndex);
            }
            else
            {
                await splitter.ExpandAsync(LeftPaneIndex);
            }

            _leftNavPaneCollapsed = collapsed;
        }

        /// <summary>
        /// Syncfusion's SfBaseUtils.InvokeMethod wraps a JSException from a synchronous splitter call in
        /// InvalidOperationException, so the JSException is matched both directly and as that wrapper's inner exception.
        /// </summary>
        private static bool IsSplitterNotReady(Exception ex)
        {
            return ex is JSException || (ex is InvalidOperationException && ex.InnerException is JSException);
        }

        private async Task AttachRightPanelHandleIfNeededAsync()
        {
            if (_rightPanelHandleAttached || _passkeySetupOnly || LayoutPrefs.RightPanelCollapsed)
            {
                return;
            }

            try
            {
                _selfRef ??= DotNetObjectReference.Create(this);
                var options = new { min = RightPanelMinWidth, max = RightPanelMaxWidth };
                await JS.InvokeVoidAsync("vfPanelResize.attach", _rightPanelHandleId, _rightPanelId, options, _selfRef);
                _rightPanelHandleAttached = true;
            }
            catch (JSException)
            {
                // Best-effort - script not loaded on this host / pre-render pass.
            }
        }

        [JSInvokable]
        public async Task OnRightPanelResized(double width)
        {
            _rightPanelWidth = width;
            await LayoutPrefs.SetRightPanelWidthAsync(width);
            StateHasChanged();
        }

        private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
        {
            SetCurrentLocation(e.Location);
            _leftNavSyncPending = true;
            _passkeySetupOnly = SessionState.PasskeySetupOnly;
            InspectorState.Clear();
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

        private bool PathMatches(string path)
        {
            return NavPathMatcher.Matches(_currentPath, _currentQuery, path);
        }

        private NavContext BuildContext()
        {
            return new(SessionState.IsSignedIn, _role, AppLockPreferences.IsSupported,
                IsLocalTier: SessionState.IsLocalTier, IsPasskeySession: SessionState.IsPasskeySession);
        }

        private async Task ToggleLeftNavAsync()
        {
            // An explicit user toggle supersedes any restore still waiting on the splitter.
            _leftNavRestorePending = false;

            // On an auto-collapse page the toggle only flips the pane for this visit. The saved preference
            // stays put, and leaving the page brings the pane back in line with it.
            if (IsAutoCollapsePath(_currentPath))
            {
                if (_outerSplitter is not null)
                {
                    await SetLeftNavPaneCollapsedAsync(_outerSplitter, !_leftNavPaneCollapsed);
                }

                return;
            }

            bool collapsed = !LayoutPrefs.LeftNavCollapsed;
            await LayoutPrefs.SetLeftNavCollapsedAsync(collapsed);

            if (_outerSplitter is null)
            {
                return;
            }

            await SetLeftNavPaneCollapsedAsync(_outerSplitter, collapsed);
        }

        private async Task ToggleRightPanelAsync()
        {
            bool collapsed = !LayoutPrefs.RightPanelCollapsed;
            await LayoutPrefs.SetRightPanelCollapsedAsync(collapsed);

            if (collapsed)
            {
                // The handle/panel divs are unmounted entirely (see MainLayout.razor) - the JS
                // listener attached to them goes with them, so the next expand must re-attach.
                try
                {
                    await JS.InvokeVoidAsync("vfPanelResize.detach", _rightPanelHandleId);
                }
                catch (JSException)
                {
                    // Best-effort cleanup.
                }

                _rightPanelHandleAttached = false;
            }
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
            IReadOnlyList<Notice> allUndismissed = await NoticeRepository.ListForOperatorAsync(SessionState.OperatorId.Value, IsAdmin, includeDismissed: false, CancellationToken.None);
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
            _ = ReleaseRightPanelHandleAsync();
        }

        /// <summary>
        /// Detaches the JS listener before releasing the reference it calls back through. Disposing the reference first
        /// would leave the listener calling a dead object ("no tracked object with id").
        /// </summary>
        private async Task ReleaseRightPanelHandleAsync()
        {
            try
            {
                if (_rightPanelHandleAttached)
                {
                    await JS.InvokeVoidAsync("vfPanelResize.detach", _rightPanelHandleId);
                }
            }
            catch (Exception ex) when (ex is JSException or JSDisconnectedException or ObjectDisposedException)
            {
                // Circuit already gone or script missing - nothing left to detach.
            }
            finally
            {
                _rightPanelHandleAttached = false;
                _selfRef?.Dispose();
                _selfRef = null;
            }
        }
    }
}
