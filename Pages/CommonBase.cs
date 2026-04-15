using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MPC.PlanSched.Model;
using MPC.PlanSched.Service;
using MPC.PlanSched.Shared.Common;
using MPC.PlanSched.Shared.Common.Extensions;
using MPC.PlanSched.UI.Services;
using MPC.PlanSched.UI.Shared;
using MPC.PlanSched.UI.ViewModel;

namespace MPC.PlanSched.UI.Pages
{
    public class CommonBase : ComponentBase
    {
        [Inject]
        public IJSRuntime JsRuntime { get; set; } = default!;
        [Inject]
        public IPopupService PopupService { get; set; } = default!;
        public string StatusMessageContent { get; set; } = string.Empty;

        private int _numMethodsExecuting = 0;

        [Inject]
        public NavigationManager NavigationManager { get; set; } = default!;
        [Inject]
        public IHttpClientFactory ClientFactory { get; set; } = default!;
        [Inject]
        protected ISessionService SessionService { get; set; } = default!;
        [Inject]
        public IActiveUser ActiveUser { get; set; } = default!;
        [Inject]
        public IExcelCommon _excelCommon { get; set; } = default!;

        public RegionModel RegionModel { get; set; } = new();
        public RefineryModel RefineryModel { get; set; } = new();
        public HttpClient Client { get; set; } = default!;
        public bool StatusPopup { get; set; } = false;
        public RequestedPeriodModel RequestedPeriodModel { get; set; } = new();
        public List<RequestedPeriodModel> RequestedPeriods { get; set; } = [];
        public List<RequestedPeriodModel> Periods { get; set; } = [];
        public bool ReadOnlyFlag { get; set; } = false;
        public bool ReadOnlyMinOverrideFlag { get; set; } = false;
        public bool ReadOnlyMaxOverrideFlag { get; set; } = false;
        public string Message { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string EditBackcastingPIMSPermissionName(string region) => $"Edit{region}{ApplicationArea.regionalbackcasting.AppDescription()}{Constant.Backcasting}";
        public static string EditPimsPermissionName(string region) => $"Edit{region}PIMS";
        public static string ViewPimsPermissionName(string region) => $"View{region}PIMS";
        public static string EditDpoPermissionName(string region) => $"Edit{region}DPO";
        public static string ViewDpoPermissionName(string region) => $"View{region}DPO";
        public static string ViewPimsRegionalPremisePremissionName(string region) => $"View{region}PIMSRegionalPremise";
        public static string EditPimsRegionalPremisePremissionName(string region) => $"Edit{region}PIMSRegionalPremise";
        public PriceType SelectedPriceType { get; set; } = PriceType.SettleCost;
        public List<PriceTypeModel> PriceTypes { get; set; } = new List<PriceTypeModel>()
        {
            new PriceTypeModel() { Text = PriceType.SettleCost.Description(), Value = PriceType.SettleCost },
            new PriceTypeModel() { Text = PriceType.MidCost.Description(), Value = PriceType.MidCost }
        };

        #region NavigationMethods
        public void NavigateToPeriod(string selectedRole, int domainNamespaceId, ApplicationArea area)
        {
            if (area == ApplicationArea.regionalbackcasting)
                NavigationManager.NavigateTo(string.Format("{0}/regionalbackcasting/plans/new/{1}", selectedRole, domainNamespaceId), true);
            else if (area == ApplicationArea.refineryplanning)
                NavigationManager.NavigateTo(string.Format("{0}/refineryplanning/plans/new/{1}", selectedRole, domainNamespaceId), true);
            else
                NavigationManager.NavigateTo(string.Format("{0}/plans/new/{1}", selectedRole, domainNamespaceId), true);
        }
        public void NavigateToRegionRP(string selectedRole)
        {
            var planType = SessionService.GetPlanType();
            if (planType.Contains("shared", StringComparison.InvariantCultureIgnoreCase))
            {
                NavigationManager.NavigateTo(string.Format(PlanNSchedConstant.SharePlanRP, selectedRole), true);
            }
            else if (planType.Contains("historical"))
            {
                NavigationManager.NavigateTo(string.Format(PlanNSchedConstant.HistoricalRp, selectedRole), true);
            }
            else
            {
                NavigationManager.NavigateTo(string.Format(PlanNSchedConstant.RegionsRp, selectedRole), true);
            }
        }

        public void NavigateToRegionBackcasting(string selectedRole)
        {
            var planType = SessionService.GetPlanType();
            if (planType.Contains("historical", StringComparison.InvariantCultureIgnoreCase))
            {
                NavigationManager.NavigateTo(string.Format(PlanNSchedConstant.HistoricalBackcasting, selectedRole), true);
            }
            else
            {
                NavigationManager.NavigateTo(string.Format(PlanNSchedConstant.RegionsBackcasting, selectedRole), true);
            }
        }
        public void NavigateToRegionDP(string selectedRole)
        {
            var planType = SessionService.GetPlanType();
            if (planType.Contains("shared", StringComparison.InvariantCultureIgnoreCase))
            {
                NavigationManager.NavigateTo(string.Format(PlanNSchedConstant.SharePlanDP, selectedRole), true);
            }
            else if (planType.Contains("historical"))
            {
                NavigationManager.NavigateTo(string.Format(PlanNSchedConstant.HistoricalDp, selectedRole), true);
            }
            else
            {
                NavigationManager.NavigateTo(string.Format(PlanNSchedConstant.RegionsDp, selectedRole), true);
            }
        }

        public void NavigateToProductRP(string selectedRole, int businessCaseId, bool isHistoricalPlan = false) =>
            NavigationManager.NavigateTo(string.Format("productdemandpricerp/{0}/{1}/{2}", selectedRole, businessCaseId, isHistoricalPlan), true);

        public void NavigateToRefineryPremiseProduct(string selectedRole, int businessCaseId, bool isHistoricalPlan = false) =>
           NavigationManager.NavigateTo(string.Format("refineryplanningsell/{0}/{1}/{2}", selectedRole, businessCaseId, isHistoricalPlan), true);

        public void NavigateToRefineryPremise(string selectedRole)
        {
            var planType = SessionService.GetPlanType();
            if (planType.Contains("historical", StringComparison.InvariantCultureIgnoreCase))
            {
                NavigationManager.NavigateTo(string.Format(PlanNSchedConstant.HistoricalRefineryPlanning, selectedRole), true);
            }
            else
            {
                NavigationManager.NavigateTo(string.Format(PlanNSchedConstant.Refineryplanning, selectedRole), true);
            }
        }

        public void NavigateToBackcastingProduct(string selectedRole, int businessCaseId, bool isHistoricalPlan = false) =>
           NavigationManager.NavigateTo(string.Format("backcastingproductdemandprice/{0}/{1}/{2}", selectedRole, businessCaseId, isHistoricalPlan), true);

        public void NavigateToProductDP(string selectedRole, int businessCaseId, bool isHistoricalPlan = false) =>
            NavigationManager.NavigateTo(string.Format("productdemandpricedp/{0}/{1}/{2}", selectedRole, businessCaseId, isHistoricalPlan), true);

        #endregion NavigationMethods

        public void Close() => StatusPopup = false;

        /// <summary>
        /// Increments counter for number of page setup methods executing
        /// </summary>
        public void LockLoading()
        {
            Interlocked.Increment(ref _numMethodsExecuting);
        }

        /// <summary>
        /// Decrements counter for number of page setup methods executing
        /// </summary>
        public void UnlockLoading()
        {
            if (_numMethodsExecuting != 0)
            {
                Interlocked.Decrement(ref _numMethodsExecuting);
            }

        }

        public void ValidateTextLength(string value, Action<string> setValue)
        {
            var input = value ?? string.Empty;
            var maxLength = ConfigurationUI.PlanDescriptionMaxLength;
            if (input.Length > maxLength)
            {
                StatusPopup = true;
                StatusMessageContent = $"Plan Description cannot exceed {maxLength} characters.";
                StateHasChanged();
            }
            else
            {
                setValue(input);
            }
        }

        /// <summary>
        /// Checks if page is loading
        /// </summary>
        /// <returns>False when all page setup methods are finished execution</returns>
        public bool IsLoading()
        {
            return _numMethodsExecuting != 0;
        }
    }
}
