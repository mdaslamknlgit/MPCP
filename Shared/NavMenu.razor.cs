using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MPC.PlanSched.Model;
using MPC.PlanSched.Service;
using MPC.PlanSched.Shared.Common;
using MPC.PlanSched.Shared.Common.Extensions;
using MPC.PlanSched.UI.Services;
using MPC.PlanSched.UI.ViewModel;

namespace MPC.PlanSched.UI.Shared
{
    public partial class NavMenu
    {
        [Inject]
        public IHttpClientFactory HttpClientFactory { get; set; }
        [Inject]
        private INavigationMenuService NavigationMenuService { get; set; } = default!;
        [CascadingParameter(Name = "SelectedRole")]
        public string SelectedRole { get; set; } = default!;
        private ExcelDownloadDialog _downloadDialog;
        [Inject]
        public IJSRuntime JsRuntime { get; set; }
        public List<TopNavMenuItemModel> TopNavMenuItems { get; set; } = default!;
        public int businessCaseId;
        public bool IsBackcasting { get; set; } = false;
        public bool IsRefineryPremise { get; set; } = false;

        protected override async Task OnInitializedAsync()
        {
            Client = HttpClientFactory.CreateClient("WebAPI");
            Client.Timeout = TimeSpan.FromSeconds(600);
            TopNavMenuItems = NavigationMenuService.GetTopNavigationSet(SelectedRole);

            await base.OnInitializedAsync();
        }

        /// <summary>
        /// On click of bread crumb items will hit this function to navigate to corresponding page or function
        /// </summary>
        /// <param name = "args"></param>
        private async Task OnMenuItemClick(TopNavMenuItemModel clickedItem)
        {
            IsBackcasting = NavigationManager.Uri.Contains(Constant.Backcasting, StringComparison.InvariantCultureIgnoreCase);
            IsRefineryPremise = NavigationManager.Uri.Contains(Constant.RefineryPremise, StringComparison.InvariantCultureIgnoreCase)
                                || NavigationManager.Uri.Contains(Constant.RefineryPlanning, StringComparison.InvariantCultureIgnoreCase);
            TopNavMenuItems.ForEach(menuItem => menuItem.CurrentPage = false);
            clickedItem.CurrentPage = true;
            if (clickedItem.Url.Contains("#/") && IsBackcasting)
            {
                var splitUri = clickedItem.Url.TrimEnd('/').Split('/');
                if (splitUri.Length > 0 && int.TryParse(splitUri.Last(), out businessCaseId))
                {
                    _downloadDialog.Open();
                    return;
                }
            }
            else if (clickedItem.Url.Contains("#/"))
            {
                var splitUri = clickedItem.Url.Split('/');
                if (int.TryParse(splitUri.Last(), out businessCaseId))
                {
                    await DownloadExcelAsync(businessCaseId);
                }
            }
            else
            {
                NavigationManager.NavigateTo(clickedItem.Url);
            }
            StateHasChanged();
        }

        #region NavigationMethods

        private async void NavigateToExcel()
        {
            await JsRuntime.InvokeVoidAsync("myJavaScriptFunction");
        }

        private Task OnPriceTypeChanged(PriceType newPriceType) =>
            InvokeAsync(() => SelectedPriceType = newPriceType);


        private async Task DownloadExcelAsync(int businessCaseId)
        {
            try
            {
                LockLoading();
                _downloadDialog.Close();
                if (IsRefineryPremise)
                {
                    var refineryModel = await UtilityUI.GetRefineryModelByBusinessCaseIdAsync(businessCaseId, SessionService.GetCorrelationId(), Client);
                    var refineryPlanExcelResponse = await Client.PostAsJsonAsync(ConfigurationUI.GetExcelPlanByRefinery, refineryModel);
                    if (!refineryPlanExcelResponse.IsSuccessStatusCode) return;

                    var stream = await refineryPlanExcelResponse.Content.ReadAsStreamAsync();
                    using var memoryStream = new MemoryStream();
                    await stream.CopyToAsync(memoryStream);
                    var base64String = Convert.ToBase64String(memoryStream.ToArray());
                    var fileName = refineryModel.DomainNamespace.DestinationApplication.Name + "_Planning.xlsx";
                    await JsRuntime.InvokeVoidAsync("saveAsFile", base64String, fileName, PlanNSchedConstant.ExcelDownloadContentType);
                    UnlockLoading();
                    return;
                }
                else
                {
                    RegionModel.PriceType = SelectedPriceType.Description();
                    var regionModel = await UtilityUI.GetRegionByBusinessCaseIdAsync(businessCaseId, SessionService.GetCorrelationId(), Client);
                    regionModel.PriceType = SelectedPriceType.Description();
                    if (regionModel.BusinessCase.Name.Contains(PlanNSchedConstant.ActualsData, StringComparison.InvariantCultureIgnoreCase))
                        regionModel.ApplicationState = Service.Model.State.Actual.Description();

                    var Area = regionModel?.DomainNamespace?.DestinationApplication.Name.Contains(PlanNSchedConstant.DPO) ?? false ? ApplicationArea.distributionplanning : ApplicationArea.regionalplanning;
                    var base64String = await _excelCommon.GetExcelBase64ByRegion(regionModel, Area);
                    var fileName = "";
                    if (regionModel.ApplicationState == Service.Model.State.Actual.Description())
                        fileName = regionModel?.DomainNamespace?.DestinationApplication.Name + PlanNSchedConstant.BackcastingPlanningFile;
                    else
                        fileName = regionModel?.DomainNamespace?.DestinationApplication.Name + PlanNSchedConstant.PlanningFile;

                    await JsRuntime.InvokeVoidAsync("saveAsFile", base64String, fileName, PlanNSchedConstant.ExcelDownloadContentType);
                }

                UnlockLoading();
            }
            catch (Exception)
            {
                UnlockLoading();
            }
        }
        #endregion NavigationMethods
    }
}