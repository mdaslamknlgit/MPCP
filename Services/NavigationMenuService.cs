using MPC.PlanSched.UI.ViewModel;

namespace MPC.PlanSched.UI.Services
{
    public class NavigationMenuService : INavigationMenuService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<NavigationMenuService> _logger;

        public NavigationMenuService(IConfiguration configuration, ILogger<NavigationMenuService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Get navigation drawer menu items based on user role from configuration.
        /// </summary>
        public Task<List<NavigationDrawerItemModel>> GetMenuItemsForCurrentUserAsync(string selectedRole)
        {
            var menuPath = selectedRole switch
            {
                "dpo" => "DistributionPlanningView",
                "rvco" => "RegionalPlanningView",
                "manager" => "ManagerView",
                _ => "DefaultView"
            };

            var menu = _configuration.GetSection($"NavigationDrawerMenu:{menuPath}").Get<List<NavigationDrawerItemModel>>();

            if (menu == null || menu.Count == 0)
            {
                _logger.LogWarning("No menu items found for path '{MenuPath}'. Falling back to DefaultView.", menuPath);
                menu = _configuration.GetSection("NavigationDrawerMenu:DefaultView").Get<List<NavigationDrawerItemModel>>() ?? [];
            }

            if (!string.IsNullOrEmpty(selectedRole))
                menu.ForEach(item => item.SetUrlRole(selectedRole));

            return Task.FromResult(menu);
        }

        public List<TopNavMenuItemModel> GetTopNavigationSet(string selectedRole) => [];

        public List<TopNavMenuItemModel> GetDpTopNavigationSet(string selectedRole, int businessCaseId, bool isHistoricalPlan) => [];

        public List<TopNavMenuItemModel> GetRpTopNavigationSet(string selectedRole, int businessCaseId, bool isHistoricalPlan) => [];

        public List<TopNavMenuItemModel> GetBackcastingTopNavigationSet(string selectedRole, int businessCaseId, bool isHistoricalPlan) => [];
    }
}
