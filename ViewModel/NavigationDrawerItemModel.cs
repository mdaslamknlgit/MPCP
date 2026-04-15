using System.Diagnostics.CodeAnalysis;

namespace MPC.PlanSched.UI.ViewModel
{
    /// <summary>
    /// Represents an item for a Telerik Drawer component.
    /// This is used to customize appearance and behavior of the menu item with its text, icons, and additional properties.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class NavigationDrawerItemModel
    {
        public string Text { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Url { get; set; } = "#";
        public string Target { get; set; } = string.Empty;
        public bool Expanded { get; set; }
        public int Level { get; set; } = 0;
        public List<NavigationDrawerItemModel> Children { get; set; } = [];
        public string? Region { get; set; }

        public void SetUrlRole(string userSelectedRole)
        {
            Url = string.Format(Url, userSelectedRole);
            Children.ForEach(child => child.SetUrlRole(userSelectedRole));
        }

        public NavigationDrawerItemModel? GetMatchingDrawerItem(string relativeUrl)
        {
            var matchingItem = relativeUrl.Equals(Url, StringComparison.OrdinalIgnoreCase)
                                            ? this
                                            : Children.Select(x => x.GetMatchingDrawerItem(relativeUrl)).FirstOrDefault(x => x != null);

            Expanded = matchingItem != null;

            return matchingItem;
        }
    }
}
