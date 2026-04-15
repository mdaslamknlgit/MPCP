using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MPC.PlanSched.Service;
using MPC.PlanSched.UI.Services;
using MPC.PlanSched.UI.ViewModel;
using Telerik.Blazor.Components;

namespace MPC.PlanSched.UI.Shared
{
    public partial class MainLayout
    {
        [Inject]
        public CookieService CookieService { get; set; }
        [Inject]
        public ISessionService SessionService { get; set; }
        [Inject]
        public IJSRuntime JsRuntime { get; set; }
        [Inject]
        private IActiveUser ActiveUser { get; set; } = default!;
        public NavigationDrawer NavDrawer { get; set; } = default!;
        public string? PlanType { get; set; }
        public ErrorBoundary? ErrorBoundary { get; set; }
        public string SelectedRole { get; set; } = string.Empty;
        public string UserView { get; set; } = string.Empty;
        public string? UserName { get; set; }
        public string? UserEmailId { get; set; }
        public IEnumerable<TreeItem> ThemeOptions { get; set; } = [];
        public IEnumerable<object> SelectedItems { get; set; } = [];
        public IEnumerable<object> ExpandedItems { get; set; } = [];

        protected override async Task OnInitializedAsync()
        {
            SetSelectedRole();
            ExpandedItems = ThemeOptions = GetThemeOptions();
            var userThemePreference = CookieService.GetThemePreference() ?? Theme.Light.ToString();
            SelectedItems = new List<object>() { ThemeOptions.FirstOrDefault(x => x.Text.ToLower() == userThemePreference.ToLower()) };

            UserName = await ActiveUser.GetNameAsync();
            UserEmailId = await ActiveUser.GetEmailAddressAsync();
            PlanType = SessionService.GetPlanType() ?? null;
        }

        private void SetSelectedRole()
        {
            if ((Body?.Target as RouteView)?.RouteData.RouteValues?.TryGetValue("selectedRole", out var roleVal) == true && roleVal is string role && !string.IsNullOrEmpty(role))
                SelectedRole = role;

            UserView = SelectedRole switch
            {
                PlanNSchedConstant.DPOEngineer => PlanNSchedConstant.DistributionPlanning,
                PlanNSchedConstant.RVCOEngineer => PlanNSchedConstant.RegionalPlanning,
                PlanNSchedConstant.Manager => PlanNSchedConstant.ManagementView,
                _ => string.Empty
            };
        }

        #region FunctionalEvents

        private TelerikAnimationContainer AnimationContainer { get; set; }

        private async Task ToggleAnimationContainerAsync()
        {
            await AnimationContainer.ToggleAsync();
        }

        private List<TreeItem> GetThemeOptions()
        {
            return new List<TreeItem>
            {
                new TreeItem { Id = 1, Text = "Theme Preference", ParentId = null, HasChildren = true },
                new TreeItem { Id = 2, Text = Theme.Light.ToString(), ParentId = 1, HasChildren = false },
                new TreeItem { Id = 3, Text = Theme.Dark.ToString(), ParentId = 1, HasChildren = false }
            };
        }

        private async Task ChangeThemeAsync(TreeViewItemClickEventArgs args)
        {
            var themePreference = (args.Item as TreeItem).Text;
            var newThemeUrl = themePreference == Theme.Light.ToString() ? "css/themes/marathon-light.css" : "css/themes/marathon-dark.css";

            await JsRuntime.InvokeAsync<object>("WriteCookie.WriteCookie", "userThemePreference", themePreference.ToLower(), DateTime.Now.AddDays(30));
            await JsRuntime.InvokeVoidAsync("themeChanger.changeCss", newThemeUrl);
        }
        #endregion NavigationMethods
    }
}