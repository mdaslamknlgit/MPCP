using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MPC.PlanSched.UI.Services;
using MPC.PlanSched.UI.ViewModel;

namespace MPC.PlanSched.UI.Shared
{
    public partial class NavigationDrawer : INavigationDrawer
    {
        [Inject]
        private NavigationManager NavigationManager { get; set; } = default!;
        [Inject]
        private INavigationMenuService NavigationMenuService { get; set; } = default!;
        [CascadingParameter(Name = "SelectedRole")]
        private string SelectedRole { get; set; } = default!;
        [Inject]
        private IJSRuntime JsRuntime { get; set; } = default!;

        public NavigationDrawerItemModel? SelectedItem { get; set; }
        public List<NavigationDrawerItemModel> DrawerData { get; set; } = [];
        public bool IsDrawerExpanded { get; set; } = true;

        protected override async Task OnInitializedAsync()
        {
            DrawerData = await NavigationMenuService.GetMenuItemsForCurrentUserAsync(SelectedRole);
            SetSelectedDrawerItem();

            await base.OnInitializedAsync();
        }

        private async void SelectedItemChangedHandlerAsync(NavigationDrawerItemModel drawerItem)
        {
            drawerItem.Expanded = !drawerItem.Expanded;

            if (drawerItem.Url == "#") return;

            if (!string.IsNullOrEmpty(drawerItem.Target))
            {
                await JsRuntime.InvokeVoidAsync("window.open", drawerItem.Url, drawerItem.Target);
                return;
            }

            SelectedItem = drawerItem;
            await InvokeAsync(StateHasChanged);
            NavigationManager.NavigateTo(drawerItem.Url, true);
        }

        public void Toggle() => SetExpanded(!IsDrawerExpanded);

        public void SetExpanded(bool isExpanded)
        {
            IsDrawerExpanded = isExpanded;
            InvokeAsync(StateHasChanged);
        }

        private void SetSelectedDrawerItem() => SetActiveMenuItemByUrl(NavigationManager.ToBaseRelativePath(NavigationManager.Uri));

        public void SetActiveMenuItemByUrl(string url) => SelectedItem = DrawerData.Select(x => x.GetMatchingDrawerItem(url)).FirstOrDefault(x => x != null);
    }
}
