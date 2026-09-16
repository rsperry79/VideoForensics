using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

using Syncfusion.Blazor.Layouts;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Services;

namespace VideoForensics.Ui.Shared.Layout
{
    public partial class MainLayout : IDisposable
    {
        private OperatorRole? _role;
        private string _currentPath = "/";
        private SfSplitter? _outerSplitter;
        private double _leftNavWidth = 220;
        private double _rightPanelWidth = 280;
        private bool _rightPanelHandleAttached;
        private DotNetObjectReference<MainLayout>? _selfRef;

        private int _undismissedCount = 0;
        private bool _noticesDropdownOpen = false;
        private List<Notice> _noticesList = new();

        private readonly string _rightPanelHandleId = $"vf-right-handle-{Guid.NewGuid():N}";
        private readonly string _rightPanelId = $"vf-right-panel-{Guid.NewGuid():N}";

        // Left-nav is pane 0 of the outer splitter (see MainLayout.razor). The right panel is not
        // a Splitter pane at all - see the comment above it in MainLayout.razor.
        private const int LeftPaneIndex = 0;
        private const double RightPanelMinWidth = 150;
        private const double RightPanelMaxWidth = 500;

        private string RightPanelStyle => $"flex: 0 0 {_rightPanelWidth}px; width: {_rightPanelWidth}px;";

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
            _currentPath = ToAppRelative(Nav.Uri);
            Nav.LocationChanged += OnLocationChanged;
            ThemeService.OnChange += StateHasChanged;
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                // IJSRuntime is unavailable during Blazor's static prerender pass, so session/layout
                // state can only be resolved once the circuit is connected.
                await SessionState.EnsureLoadedAsync();
                _role = SessionState.Role is not null && Enum.TryParse<OperatorRole>(SessionState.Role, out OperatorRole role) ? role : null;

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
                    _undismissedCount = await NoticeRepository.CountUndismissedForOperatorAsync(SessionState.OperatorId.Value, CancellationToken.None);
                }

                StateHasChanged();

                // SplitterPane.Size is an initial-only value (per Syncfusion docs); once the
                // splitter's JS widget has initialized, collapse state must be driven imperatively
                // via CollapseAsync/ExpandAsync rather than by re-binding Size or a CssClass.
                if (LayoutPrefs.LeftNavCollapsed && _outerSplitter is not null)
                {
                    await _outerSplitter.CollapseAsync(LeftPaneIndex);
                }
            }

            await AttachRightPanelHandleIfNeededAsync();
        }

        private async Task AttachRightPanelHandleIfNeededAsync()
        {
            if (_rightPanelHandleAttached || LayoutPrefs.RightPanelCollapsed)
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
            _currentPath = ToAppRelative(e.Location);
            _ = InvokeAsync(StateHasChanged);
        }

        private static string ToAppRelative(string uri)
        {
            string path = new Uri(uri).AbsolutePath;
            return string.IsNullOrEmpty(path) ? "/" : path;
        }

        private bool PathMatches(string path)
        {
            return path == "/"
                ? _currentPath == "/"
                : _currentPath.Equals(path, StringComparison.OrdinalIgnoreCase)
                || _currentPath.StartsWith(path + "/", StringComparison.OrdinalIgnoreCase);
        }

        private NavContext BuildContext()
        {
            return new(SessionState.IsSignedIn, _role, AppLockPreferences.IsSupported);
        }

        private async Task ToggleLeftNavAsync()
        {
            bool collapsed = !LayoutPrefs.LeftNavCollapsed;
            await LayoutPrefs.SetLeftNavCollapsedAsync(collapsed);

            if (_outerSplitter is null)
            {
                return;
            }

            if (collapsed)
            {
                await _outerSplitter.CollapseAsync(LeftPaneIndex);
            }
            else
            {
                await _outerSplitter.ExpandAsync(LeftPaneIndex);
            }
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

        private async Task ToggleNoticesAsync()
        {
            _noticesDropdownOpen = !_noticesDropdownOpen;
            if (_noticesDropdownOpen && SessionState.OperatorId.HasValue)
            {
                await LoadNoticesForDropdownAsync();
            }
            else if (!_noticesDropdownOpen && SessionState.OperatorId.HasValue)
            {
                _undismissedCount = await NoticeRepository.CountUndismissedForOperatorAsync(SessionState.OperatorId.Value, CancellationToken.None);
            }
            StateHasChanged();
        }

        private async Task LoadNoticesForDropdownAsync()
        {
            if (!SessionState.OperatorId.HasValue)
            {
                return;
            }

            _noticesList = (await NoticeRepository.ListForOperatorAsync(SessionState.OperatorId.Value, includeDismissed: false, CancellationToken.None))
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
            _undismissedCount = await NoticeRepository.CountUndismissedForOperatorAsync(SessionState.OperatorId.Value, CancellationToken.None);
            StateHasChanged();
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
            if (elapsed.TotalSeconds < 60)
                return "just now";
            if (elapsed.TotalMinutes < 60)
                return $"{(int)elapsed.TotalMinutes}m ago";
            if (elapsed.TotalHours < 24)
                return $"{(int)elapsed.TotalHours}h ago";
            if (elapsed.TotalDays < 7)
                return $"{(int)elapsed.TotalDays}d ago";
            return utcTime.ToString("MMM d, yyyy");
        }

        public void Dispose()
        {
            Nav.LocationChanged -= OnLocationChanged;
            ThemeService.OnChange -= StateHasChanged;
            _selfRef?.Dispose();
        }
    }
}
