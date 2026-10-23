
using Microsoft.Extensions.Localization;

using VideoForensics.Client.Ui.Shared.Layout;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

public class NavGroupsLocalizationTests
{
    // NavSectionList, MainLayout and MobileLayout render every label and section heading through
    // IStringLocalizer<SharedResources>. A label with no resource key would silently show its English
    // fallback instead of a translation, so each one must resolve in SharedResources.resx.
    [Fact]
    public void NavGroups_AllLabelsAndSections_ResolveInSharedResources()
    {
        IStringLocalizer localizer = TestLocalizer.Create();
        var labels = new List<string>();

        foreach (NavGroup group in NavGroups.All)
        {
            labels.Add(group.Text);
            foreach (NavItem item in group.Items)
            {
                CollectItem(item, labels);
            }
        }

        var missing = labels.Distinct().Where(text => localizer[text].ResourceNotFound).ToList();

        Assert.Empty(missing);
    }

    private static void CollectItem(NavItem item, List<string> labels)
    {
        labels.Add(item.Text);
        if (item.Section is not null)
        {
            labels.Add(item.Section);
        }

        if (item.Children is null)
        {
            return;
        }

        foreach (NavItem child in item.Children)
        {
            CollectItem(child, labels);
        }
    }
}
