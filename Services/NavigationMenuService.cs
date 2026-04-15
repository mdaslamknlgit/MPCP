using Microsoft.AspNetCore.Components;
using Microsoft.FeatureManagement;
using MPC.PlanSched.Service;
using MPC.PlanSched.Shared.Common.Extensions;
using MPC.PlanSched.UI.ViewModel;
using Telerik.Blazor.Components.Menu;

namespace MPC.PlanSched.UI.Services
{
    public class NavigationMenuService : INavigationMenuService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;
        private readonly IFeatureManager _featureManager;
        private readonly NavigationManager _navigationManager;
        private readonly IActiveUser _activeUser;

        private bool IsMidterm => _configuration.GetValue<bool>("AppSettings:IsMidtermEnabled");


        public NavigationMenuService(IConfiguration configuration, ILogger<NavigationMenuService> logger, IFeatureManager featureManager, NavigationManager navigationManager, IActiveUser activeUser)
        {
            _configuration = configuration;
            _logger = logger;
            _featureManager = featureManager;
            _navigationManager = navigationManager;
            _activeUser = activeUser;
        }

        /// <summary>
        /// Get navigation menu items based on user role and set visibility based on policies
        /// </summary>
        /// <param name="userSelectedRole"></param>
        /// <param name="rolePolicies"></param>
        /// <returns></returns>
        public async Task<List<NavigationDrawerItemModel>> GetMenuItemsForCurrentUserAsync(string userSelectedRole)
        {
            if (string.IsNullOrEmpty(userSelectedRole))
            {
                _logger.LogWarning("User role is not set. Defaulting to default navigation menu.");
                return _configuration.GetSection("NavigationDrawerMenu:DefaultView").Get<List<NavigationDrawerItemModel>>() ?? throw new Exception("No menu items found");
            }

            var menu = GetMenuItems(userSelectedRole);
            var authorizedRegions = await GetAuthorizedRegionsAsync();
            menu = menu.Where(menuItem => ShowMenuItem(menuItem, authorizedRegions)).ToList();

            if (IsMidterm)
            {
                _logger.LogInformation($"Applying Midterm menu configuration");
                menu.RemoveAll(x => x.Text == ApplicationArea.refineryplanning.Description());
                menu.RemoveAll(x => x.Text == ApplicationArea.regionalbackcasting.Description());
            }
            else
            {
                _logger.LogInformation($"Removing Shared Plans menu item (IsMidterm is false)");
                foreach (var menuItem in menu)
                {
                    if (menuItem.Children != null && menuItem.Children.Any())
                    {
                        menuItem.Children.RemoveAll(x => x.Text == "Shared Plans");
                    }
                }
            }
            /// User can see Backcasting/Forecasting drawer menu based on assigned Role
            if (!await _activeUser.IsAuthorizedAsync($"ViewPIMS"))
                menu.RemoveAll(x => x.Text == ApplicationArea.regionalplanning.Description());
            if (!await _activeUser.IsAuthorizedAsync($"ViewPIMSBackcasting"))
                menu.RemoveAll(x => x.Text == ApplicationArea.regionalbackcasting.Description());
            if (!await _activeUser.IsAuthorizedAsync($"ViewPPIMS"))
                menu.RemoveAll(x => x.Text == ApplicationArea.refineryplanning.Description());

            if (!await _featureManager.IsEnabledAsync(FeatureFlags.RegionalBackcasting))
                menu.RemoveAll(x => x.Text == ApplicationArea.regionalbackcasting.Description());

            return menu;
        }

        private List<NavigationDrawerItemModel> GetMenuItems(string userSelectedRole)
        {
            var menuPath = userSelectedRole switch
            {
                PlanNSchedConstant.DPOEngineer => "DistributionPlanningView",
                PlanNSchedConstant.RVCOEngineer => "RegionalPlanningView",
                PlanNSchedConstant.Manager => "ManagerView",
                _ => "DefaultView"
            };

            var menu = _configuration.GetSection($"NavigationDrawerMenu:{menuPath}").Get<List<NavigationDrawerItemModel>>()
                ?? throw new Exception($"No menu items found for role '{userSelectedRole}'");

            menu.ForEach(menuItem => menuItem.SetUrlRole(userSelectedRole));

            return menu;
        }

        private bool ShowMenuItem(NavigationDrawerItemModel menuItem, List<string> authorizedRegions)
        {
            if (!(string.IsNullOrEmpty(menuItem.Region) || authorizedRegions.Contains(menuItem.Region, StringComparer.OrdinalIgnoreCase)))
                return false;

            menuItem.Children = menuItem.Children.Where(x => ShowMenuItem(x, authorizedRegions)).ToList();

            return true;
        }

        /// <summary>
        /// Returns a list of regions for which the user is authorized to view
        /// </summary>
        /// <returns></returns>
        private async Task<List<string>> GetAuthorizedRegionsAsync()
        {
            var regions = new[] { PlanNSchedConstant.NorthRegion, PlanNSchedConstant.SouthRegion, PlanNSchedConstant.WestRegion };
            var authorizedRegions = new List<string>();
            foreach (var region in regions)
            {
                var authorized = await _activeUser.IsAuthorizedAsync($"View{region}PIMS") || await _activeUser.IsAuthorizedAsync($"View{region}DPO");
                if (authorized)
                    authorizedRegions.Add(region);
            }

            return authorizedRegions;
        }

        public List<TopNavMenuItemModel> GetTopNavigationSet(string selectedRole)
        {
            var url = _navigationManager.ToBaseRelativePath(_navigationManager.Uri);
            try
            {
                var splitUri = url.Split('/');
                var businessCaseId = int.Parse(splitUri[splitUri.Count() - 2]);
                var isHistoricalPlan = bool.Parse(splitUri[splitUri.Count() - 1]);

                return url switch
                {
                    var page when page.Contains("productdemandpricerp") || page.Contains("productsupplycostrp") || page.Contains("inventoryrp") || page.Contains("regionalpremise")
                        => GetRpTopNavigationSet(selectedRole, businessCaseId, isHistoricalPlan),
                    var page when page.Contains("refinerypremise") || page.Contains("refineryplanningsell") || page.Contains("refineryplanningbuy")
                   => GetRefineryPremiseTopNavigationSet(selectedRole, businessCaseId, isHistoricalPlan),
                    var page when page.Contains("productdemandpricedp") || page.Contains("productsupplycostdp") || page.Contains("inventorydp") || page.Contains("transportationcostdp") || page.Contains("intransitdp")
                        => GetDpTopNavigationSet(selectedRole, businessCaseId, isHistoricalPlan),
                    var page when page.Contains("backcastingproductdemandprice") || page.Contains("backcastingproductsupplycost") || page.Contains("backcastingoptimizationavailscost") || page.Contains("backcastinginventory") || page.Contains("backcastingtransfercost")
                        => GetBackcastingTopNavigationSet(selectedRole, businessCaseId, isHistoricalPlan),
                    _ => []
                };
            }
            catch
            {
                return [];
            }
        }

        public List<TopNavMenuItemModel> GetRpTopNavigationSet(string selectedRole, int businessCaseId, bool isHistoricalPlan)
        {
            var pageUri = _navigationManager.ToBaseRelativePath(_navigationManager.Uri);
            var IsAuthorizedPimsRegionalPremiseUser = _activeUser.IsAuthorizedAsync(PlanNSchedConstant.ViewPimsRegionalPremise).GetAwaiter().GetResult();
            var topNavMenuItemModels = new List<TopNavMenuItemModel>
            {
                MakeBreadcrumb(PlanNSchedConstant.URL_DEMAND_PRICE_RP, PlanNSchedConstant.MENU_TEXT_DEMAND,
                    PlanNSchedConstant.TITLE_DEMAND_RP, PlanNSchedConstant.MENU_ICON_DEMAND, pageUri, selectedRole, businessCaseId, isHistoricalPlan),
                MakeBreadcrumb(PlanNSchedConstant.URL_SUPPLY_COST_RP, PlanNSchedConstant.MENU_TEXT_SUPPLY,
                    PlanNSchedConstant.TITLE_SUPPLY_RP, PlanNSchedConstant.MENU_ICON_SUPPLY, pageUri, selectedRole, businessCaseId, isHistoricalPlan),           
            };

            if (!IsMidterm)
            {
                topNavMenuItemModels.Add(
                    MakeBreadcrumb(PlanNSchedConstant.URL_OPENING_INVENTORY_RP, PlanNSchedConstant.MENU_TEXT_INVENTORY,
                        PlanNSchedConstant.TITLE_INVENTORY_RP, PlanNSchedConstant.MENU_ICON_INVENTORY, pageUri, selectedRole, businessCaseId, isHistoricalPlan)
                );
            }

            if (_featureManager.IsEnabledAsync(FeatureFlags.PlanPremise).GetAwaiter().GetResult())
            {
                topNavMenuItemModels.Add(
                    MakeBreadcrumb(PlanNSchedConstant.URL_REGIONAL_PREMISE, PlanNSchedConstant.MENU_TEXT_REGIONAL_PREMISE,
                        PlanNSchedConstant.TITLE_REGIONAL_PREMISE, PlanNSchedConstant.MENU_ICON_REGIONAL_PREMISE, pageUri, selectedRole, businessCaseId, isHistoricalPlan)
                );
            }

            topNavMenuItemModels.Add(
                new TopNavMenuItemModel
                {
                    Text = PlanNSchedConstant.MENU_TEXT_DOWNLOAD_PLAN,
                    Title = PlanNSchedConstant.TITLE_DOWNLOAD_PLAN_RP,
                    Icon = PlanNSchedConstant.MENU_ICON_DOWNLOAD_PLAN,
                    Url = $"#/{businessCaseId}",
                    CurrentPage = false
                }
            );

            if (!IsAuthorizedPimsRegionalPremiseUser)
                topNavMenuItemModels.RemoveAll(x => x.Title == PlanNSchedConstant.TITLE_REGIONAL_PREMISE);
            return topNavMenuItemModels;
        }

        public List<TopNavMenuItemModel> GetRefineryPremiseTopNavigationSet(string selectedRole, int businessCaseId, bool isHistoricalPlan)
        {
            var pageUri = _navigationManager.ToBaseRelativePath(_navigationManager.Uri);
            var topNavMenuItemModels = new List<TopNavMenuItemModel>
            {
                MakeBreadcrumb(PlanNSchedConstant.URL_REFINERY_PLANING_SELL, PlanNSchedConstant.MENU_TEXT_REFINERY_PREMISE_SELL,
                    PlanNSchedConstant.TITLE_REFINERY_PREMISE_SELL, PlanNSchedConstant.MENU_ICON_REFINERY_PREMISE_SELL, pageUri, selectedRole, businessCaseId, isHistoricalPlan),
                MakeBreadcrumb(PlanNSchedConstant.URL_REFINERY_PLANNING_BUY, PlanNSchedConstant.MENU_TEXT_REFINERY_PREMISE_BUY,
                    PlanNSchedConstant.TITLE_REFINERY_PREMISE_BUY, PlanNSchedConstant.MENU_ICON_REFINERY_PREMISE_BUY, pageUri, selectedRole, businessCaseId, isHistoricalPlan)
            };

            if (_featureManager.IsEnabledAsync(FeatureFlags.PlanPremise).GetAwaiter().GetResult())
            {
                topNavMenuItemModels.Add(
                    MakeBreadcrumb(PlanNSchedConstant.URL_REFINERY_PREMISE, PlanNSchedConstant.MENU_TEXT_REFINERY_PREMISE,
                        PlanNSchedConstant.TITLE_REFINERY_PREMISE, PlanNSchedConstant.MENU_ICON_REFINERY_PREMISE, pageUri, selectedRole, businessCaseId, isHistoricalPlan)
                );
            }

            topNavMenuItemModels.Add(
                new TopNavMenuItemModel
                {
                    Text = PlanNSchedConstant.MENU_TEXT_DOWNLOAD_PLAN,
                    Title = PlanNSchedConstant.TITLE_DOWNLOAD_PLAN_RP,
                    Icon = PlanNSchedConstant.MENU_ICON_DOWNLOAD_PLAN,
                    Url = $"#/{businessCaseId}",
                    CurrentPage = false
                }
            );

            return topNavMenuItemModels;
        }

        public List<TopNavMenuItemModel> GetBackcastingTopNavigationSet(string selectedRole, int businessCaseId, bool isHistoricalPlan)
        {
            var pageUri = _navigationManager.ToBaseRelativePath(_navigationManager.Uri);

            var navItems = new List<TopNavMenuItemModel>
                {
                    MakeBreadcrumb(PlanNSchedConstant.URL_BACKCASTING_DEMAND_PRICE, PlanNSchedConstant.MENU_TEXT_DEMAND,
                        PlanNSchedConstant.TITLE_DEMAND_BACKCASTING, PlanNSchedConstant.MENU_ICON_DEMAND, pageUri, selectedRole, businessCaseId, isHistoricalPlan),

                    MakeBreadcrumb(PlanNSchedConstant.URL_BACKCASTING_SUPPLY_COST, PlanNSchedConstant.MENU_TEXT_SUPPLY,
                        PlanNSchedConstant.TITLE_SUPPLY_BACKCASTING, PlanNSchedConstant.MENU_ICON_SUPPLY, pageUri, selectedRole, businessCaseId, isHistoricalPlan),
                };

            navItems.Add(
                MakeBreadcrumb(PlanNSchedConstant.URL_BACKCASTING_OPTIMIZATION_AVAILS_COST, PlanNSchedConstant.MENU_TEXT_OPTIMIZATION_AVAILS,
                    PlanNSchedConstant.TITLE_OPTIMIZATION_AVAILS_BACKCASTING, PlanNSchedConstant.MENU_ICON_OPTIMIZATION_AVAILS, pageUri, selectedRole, businessCaseId, isHistoricalPlan)
            );

            if (_featureManager.IsEnabledAsync(FeatureFlags.EnableApsDataForBackcasting).GetAwaiter().GetResult())
            {
                navItems.Add(
                    MakeBreadcrumb(PlanNSchedConstant.URL_BACKCASTING_INVENTORY_COST, PlanNSchedConstant.MENU_TEXT_INVENTORY_BACKCASTING,
                        PlanNSchedConstant.TITLE_INVENTORY_BACKCASTING, PlanNSchedConstant.MENU_ICON_INVENTORY_BACKCASTING, pageUri, selectedRole, businessCaseId, isHistoricalPlan)
                );
                navItems.Add(
                   MakeBreadcrumb(PlanNSchedConstant.URL_BACKCASTING_TRANSFER_COST, PlanNSchedConstant.MENU_TEXT_TRANSFER,
                       PlanNSchedConstant.TITLE_TRANSFER_BACKCASTING, PlanNSchedConstant.MENU_ICON_TRANSFER_COST, pageUri, selectedRole, businessCaseId, isHistoricalPlan)
               );
            }

            navItems.Add(
                new TopNavMenuItemModel
                {
                    Text = PlanNSchedConstant.MENU_TEXT_DOWNLOAD_PLAN,
                    Title = PlanNSchedConstant.TITLE_DOWNLOAD_PLAN_RP,
                    Icon = PlanNSchedConstant.MENU_ICON_DOWNLOAD_PLAN,
                    Url = $"#/{businessCaseId}",
                    CurrentPage = false
                }
            );

            return navItems;
        }



        public List<TopNavMenuItemModel> GetDpTopNavigationSet(string selectedRole, int businessCaseId, bool isHistoricalPlan)
        {
            var pageUri = _navigationManager.ToBaseRelativePath(_navigationManager.Uri).TrimStart('/');


            var topNavMenuItemModels = new List<TopNavMenuItemModel>
            {
                MakeBreadcrumb(PlanNSchedConstant.URL_DEMAND_PRICE_DP, PlanNSchedConstant.MENU_TEXT_DEMAND,
                    PlanNSchedConstant.TITLE_DEMAND_DP, PlanNSchedConstant.MENU_ICON_DEMAND, pageUri, selectedRole, businessCaseId, isHistoricalPlan),
                MakeBreadcrumb(PlanNSchedConstant.URL_SUPPLY_COST_DP, PlanNSchedConstant.MENU_TEXT_SUPPLY,
                    PlanNSchedConstant.TITLE_SUPPLY_DP, PlanNSchedConstant.MENU_ICON_SUPPLY, pageUri, selectedRole, businessCaseId, isHistoricalPlan)
            };

            if (!IsMidterm)
            {
                topNavMenuItemModels.Add(
                    MakeBreadcrumb(PlanNSchedConstant.URL_OPENING_INVENTORY_DP, PlanNSchedConstant.MENU_TEXT_INVENTORY,
                        PlanNSchedConstant.TITLE_INVENTORY_DP, PlanNSchedConstant.MENU_ICON_INVENTORY, pageUri,	selectedRole, businessCaseId,
                        isHistoricalPlan)
                );
                topNavMenuItemModels.Add(
                    MakeBreadcrumb(PlanNSchedConstant.URL_INTRANSIT_INVENTORY_DP, PlanNSchedConstant.MENU_TEXT_INTRANSIT_INVENTORY,
                        PlanNSchedConstant.TITLE_INTRANSIT_INVENTORY_DP, PlanNSchedConstant.MENU_ICON_INTRANSIT_INVENTORY, pageUri,
                        selectedRole, businessCaseId, isHistoricalPlan)
                );
                topNavMenuItemModels.Add(
                    MakeBreadcrumb(PlanNSchedConstant.URL_TRANSPORTATION_COST_DP, PlanNSchedConstant.MENU_TEXT_TRANSP_COST,
                        PlanNSchedConstant.TITLE_TRANSP_COST_DP, PlanNSchedConstant.MENU_ICON_TRANSP_COST, pageUri, selectedRole, businessCaseId, isHistoricalPlan)
                );
            }

            /// Add download and disable item to breadcrumb
            topNavMenuItemModels.Add(
                new TopNavMenuItemModel
                {
                    Text = PlanNSchedConstant.MENU_TEXT_DOWNLOAD_PLAN,
                    Title = PlanNSchedConstant.TITLE_DOWNLOAD_PLAN_DP,
                    Icon = PlanNSchedConstant.MENU_ICON_DOWNLOAD_PLAN,
                    Url = $"#/{businessCaseId}",
                    CurrentPage = false
                }
            );

            return topNavMenuItemModels;
        }

        private static TopNavMenuItemModel MakeBreadcrumb(string pagePattern, string text, string title, string icon, string pageUri,
            string userRole, int businessCaseId, bool isHistoricalPlan = false)
        {
            var url = string.Format(pagePattern, userRole, businessCaseId, isHistoricalPlan);
            return new TopNavMenuItemModel
            {
                Text = text,
                Title = title,
                Icon = icon,
                CurrentPage = pageUri == url,
                Url = url,
                Disabled = false
            };
        }
    }
}