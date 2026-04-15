using Microsoft.AspNetCore.Components;
using MPC.PlanSched.UI.ViewModel;

namespace MPC.PlanSched.UI.Shared
{
    public partial class NavigationDrawerItem
    {
        [Parameter]
        public NavigationDrawerItemModel SelectedItem { get; set; } = default!;
        [Parameter]
        public NavigationDrawerItemModel Item { get; set; } = default!;
        [Parameter]
        public Action<NavigationDrawerItemModel> SelectEventHandler { get; set; } = default!;
        private string selectedClass => Item == SelectedItem ? "k-selected" : string.Empty;
    }
}
