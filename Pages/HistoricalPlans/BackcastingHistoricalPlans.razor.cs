using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MPC.PlanSched.Model;
using MPC.PlanSched.Service;
using MPC.PlanSched.Shared.Common;
using MPC.PlanSched.Shared.Common.Extensions;
using MPC.PlanSched.UI.Shared;
using Telerik.Blazor.Components;
using BusinessCase = MPC.PlanSched.Shared.Service.Schema.BusinessCase;

namespace MPC.PlanSched.UI.Pages.HistoricalPlans
{
    [Authorize(Policy = PlanNSchedConstant.ViewPimsBackcasting)]
    public partial class BackcastingHistoricalPlans
    {
        [Parameter]
        public string SelectedRole { get; set; }
        private string CreatedOn { get; set; }
        private string UpdatedOn { get; set; }
        private string LocalTimeZoneName { get; set; } = string.Empty;
        private ExcelDownloadDialog? _excelDialogRef;
        private BusinessCase? _selectedBusinessCase;
        private List<RegionModel> _regionsList = new List<RegionModel>();
        public TelerikGrid<BusinessCase> HistoricalGridRPRef { get; set; }
        public IEnumerable<BusinessCase>? BusinessCases { get; set; } = null;
        [Inject]
        public ILogger<BackcastingHistoricalPlans> Logger { get; set; } = default!;

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                LockLoading();
                StateHasChanged();
                CommonHelper.UpdateBaggage(SessionService.GetCorrelationId());
                LocalTimeZoneName = SessionService.GetLocalTimezoneName();
                if (LocalTimeZoneName == string.Empty)
                {
                    LocalTimeZoneName = PlanNSchedConstant.DefaultTimeZone;
                }
                CreatedOn = "Plan Create Date (" + LocalTimeZoneName + ")";
                UpdatedOn = "Last Updated On (" + LocalTimeZoneName + ")";
                SessionService.SetPlanType("HistoricalPlansRP");
                Client = ClientFactory.CreateClient("WebAPI");
                Client.Timeout = TimeSpan.FromSeconds(600);
                await GetRegionsAsync();
                UnlockLoading();
                StateHasChanged();
            }
            await base.OnAfterRenderAsync(firstRender);
        }

        private async Task GetRegionsAsync()
        {
            Logger.LogMethodInfo("Start of GetRegions");
            LockLoading();
            try
            {
                _regionsList = await UtilityUI.GetRegionListAsync(ActiveUser, ConfigurationUI.domainNamespaceTypeRP, Client, LocalTimeZoneName, SessionService.GetCorrelationId(), true, true);
                BusinessCases = _regionsList?.SelectMany(x => x.ActiveBusinessCases ?? Enumerable.Empty<BusinessCase>());
                ConvertUTCDateToLocal();
                UnlockLoading();
                Logger.LogMethodInfo("End of GetRegionsAsync");
            }
            catch (Exception ex)
            {
                UnlockLoading();
                Logger.LogMethodError(ex, "Error occurred in GetRegions method.");
            }
        }

        private void ViewPlan(BusinessCase businessCase)
        {
            LockLoading();
            var regionObj = GetRegion(businessCase);
            CommonHelper.UpdateBaggageWOPeriod(SessionService.GetCorrelationId(),
               regionObj.BusinessCase?.Id,
               regionObj?.DomainNamespace?.DestinationApplication.Name);
            regionObj.CorrelationId = SessionService?.GetCorrelationId();
            var businessCaseId = regionObj.BusinessCase.Id;
            NavigateToBackcastingProduct(SelectedRole, businessCaseId, true);
        }

        private Task OnPriceTypeChanged(PriceType newPriceType) =>
            InvokeAsync(() => SelectedPriceType = newPriceType);

        private async Task ExcelDownloadAsync(BusinessCase businessCase)
        {
            try
            {
                businessCase.PriceType = SelectedPriceType.Description();
                Logger?.LogMethodStart();
                LockLoading();
                _excelDialogRef.Close();
                var regionObj = GetRegion(businessCase);
                regionObj.PriceType = SelectedPriceType.Description();
                var base64String = await _excelCommon.GetExcelBase64ByRegion(regionObj, ApplicationArea.regionalbackcasting);
                var fileName = regionObj?.DomainNamespace?.DestinationApplication.Name + PlanNSchedConstant.BackcastingPlanningFile;
                await JsRuntime.InvokeVoidAsync(PlanNSchedConstant.ExcelDownloadJavascriptFunction, base64String, fileName, PlanNSchedConstant.ExcelDownloadContentType);
                UnlockLoading();
                Logger?.LogMethodEnd();
            }
            catch (Exception ex)
            {
                UnlockLoading();
                Logger?.LogMethodError(ex, "Error occurred in ExcelDownload method for PIMS.");
            }
        }

        private RegionModel GetRegion(BusinessCase businessCase)
        {
            var regionModel = _regionsList.Find(x => x.RegionName == businessCase.Region);
            regionModel.BusinessCase = businessCase;
            regionModel.IsHistoricalPlan = true;
            return regionModel;
        }

        private void ConvertUTCDateToLocal()
        {
            var cstZone = TimeZoneInfo.FindSystemTimeZoneById(LocalTimeZoneName);
            DateTime UTC_CreatedOn, UTC_UpdatedOn;
            foreach (var _businessCaseObj in BusinessCases)
            {
                if (_businessCaseObj.CreatedOn != null && _businessCaseObj.UpdatedOn != null)
                {
                    UTC_CreatedOn = DateTime.Parse(Convert.ToString(_businessCaseObj.CreatedOn));
                    UTC_UpdatedOn = DateTime.Parse(Convert.ToString(_businessCaseObj.UpdatedOn));
                    if (LocalTimeZoneName == PlanNSchedConstant.DefaultTimeZone)
                    {
                        _businessCaseObj.CreatedOn = UTC_CreatedOn.ToLocalTime();
                        _businessCaseObj.UpdatedOn = UTC_UpdatedOn.ToLocalTime();
                    }
                    else
                    {
                        _businessCaseObj.CreatedOn = TimeZoneInfo.ConvertTimeFromUtc(UTC_CreatedOn, cstZone);
                        _businessCaseObj.UpdatedOn = TimeZoneInfo.ConvertTimeFromUtc(UTC_UpdatedOn, cstZone);
                    }
                }
            }
        }
    }
}
