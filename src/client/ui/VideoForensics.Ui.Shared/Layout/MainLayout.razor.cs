using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Syncfusion.Blazor.Layouts;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Ui.Shared.Layout
{
    public partial class MainLayout : IDisposable
    {
        private OperatorRole? _role;
        private string _currentPath = "/";
        private SfSplitter? _splitter;
        private double _leftNavWidth = 220;
        private double _rightPanelWidth = 280;

        private IReadOnlyList<NavGroup> VisibleGroups => NavGroups.All.Where(g => g.IsVisible(BuildContext())).ToList();

        private NavGroup ActiveGroup
        {
            get
            {
                var visible = VisibleGroups;
                var ctx = BuildContext();
                var match = visible.FirstOrDefault(g => g.Items.Any(i => i.IsVisible(ctx) && PathMatches(i.Path)));
                return match ?? visible.FirstOrDefault() ?? NavGroups.All[0];
            }
        }

        private IReadOnlyList<NavItem> ActiveGroupItems
        {
            get
            {
                var ctx = BuildContext();
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

                // Load saved splitter pane sizes
                if (LayoutPrefs.LeftNavWidth > 0)
                {
                    _leftNavWidth = LayoutPrefs.LeftNavWidth;
                }
                if (LayoutPrefs.RightPanelWidth > 0)
                {
                    _rightPanelWidth = LayoutPrefs.RightPanelWidth;
                }

                StateHasChanged();
            }
        }

        private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
        {
            _currentPath = ToAppRelative(e.Location);
            InvokeAsync(StateHasChanged);
        }

        private static string ToAppRelative(string uri)
        {
            var path = new Uri(uri).AbsolutePath;
            return string.IsNullOrEmpty(path) ? "/" : path;
        }

        private bool PathMatches(string path)
        {
            if (path == "/")
            {
                return _currentPath == "/";
            }

            return _currentPath.Equals(path, StringComparison.OrdinalIgnoreCase)
                || _currentPath.StartsWith(path + "/", StringComparison.OrdinalIgnoreCase);
        }

        private NavContext BuildContext() => new(SessionState.IsSignedIn, _role, AppLockPreferences.IsSupported);

        private async Task ToggleLeftNavAsync() => await LayoutPrefs.SetLeftNavCollapsedAsync(!LayoutPrefs.LeftNavCollapsed);

        private async Task ToggleRightPanelAsync() => await LayoutPrefs.SetRightPanelCollapsedAsync(!LayoutPrefs.RightPanelCollapsed);

        public void Dispose()
        {
            Nav.LocationChanged -= OnLocationChanged;
            ThemeService.OnChange -= StateHasChanged;
        }
    }
}
