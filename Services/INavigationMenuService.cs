using MPC.PlanSched.UI.ViewModel;

namespace MPC.PlanSched.UI.Services
{
    public interface INavigationMenuService
    {
        List<TopNavMenuItemModel> GetTopNavigationSet(string selectedRole);
        List<TopNavMenuItemModel> GetDpTopNavigationSet(string selectedRole, int businessCaseId, bool isHistoricalPlan);
        List<TopNavMenuItemModel> GetRpTopNavigationSet(string selectedRole, int businessCaseId, bool isHistoricalPlan);
        List<TopNavMenuItemModel> GetBackcastingTopNavigationSet(string selectedRole, int businessCaseId, bool isHistoricalPlan);
        Task<List<NavigationDrawerItemModel>> GetMenuItemsForCurrentUserAsync(string selectedRole);
    }
}