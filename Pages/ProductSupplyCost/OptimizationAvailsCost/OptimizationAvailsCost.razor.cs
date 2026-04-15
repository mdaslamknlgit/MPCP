using Microsoft.AspNetCore.Authorization;
using Microsoft.JSInterop;
using MPC.PlanSched.Model;
using MPC.PlanSched.Service;
using MPC.PlanSched.Shared.Common;
using MPC.PlanSched.Shared.Common.Extensions;
using MPC.PlanSched.UI.Pages.ProductSupplyCost.Components;
using MPC.PlanSched.UI.Shared;

namespace MPC.PlanSched.UI.Pages.ProductSupplyCost.OptimizationAvailsCost
{
    [Authorize(Policy = PlanNSchedConstant.ViewPimsBackcasting)]
    public partial class OptimizationAvailsCost : BackcastingProductSupplyCostBaseComponent
    {
        private ExcelDownloadDialog? _excelDialogRef;

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);
            if (firstRender)
            {
                LockLoading();
                StateHasChanged();
                Client = ClientFactory.CreateClient("WebAPI");
                Client.Timeout = TimeSpan.FromSeconds(600);
                OverrideTypes = await UtilityUI.GetOverrideTypes(Client);
                RegionModel = await UtilityUI.GetRegionByBusinessCaseIdAsync(BusinessCaseId, SessionService.GetCorrelationId(), Client);
                RegionModel.ApplicationState = Service.Model.State.Actual.Description();
                if (RegionModel != null)
                {
                    RegionTitle = RegionModel.BusinessCase.Name;
                    PlanUpdatedOn = "Last Saved: " + RegionModel.BusinessCase.UpdatedOn?.ToString("MM/dd/yy");
                    RegionName = RegionModel.RegionName;
                    PlanDescription = RegionModel.BusinessCase.Description;
                    ReadOnlyFlag = IsHistoricalPlan;
                    RegionModel.IsHierarchy = false;
                }

                await GetPeriodsAsync();

                CommonHelper.UpdateBaggage(SessionService.GetCorrelationId(),
                    RegionModel?.BusinessCase?.Id,
                    RequestedPeriods?.FirstOrDefault()?.PeriodID,
                    RegionModel?.DomainNamespace?.DestinationApplication.Name);

                await GetSupplyAndCostDataFromPublisherAsync(true);
                UnlockLoading();
                StateHasChanged();
            }
        }

        private Task OnPriceTypeChanged(PriceType newPriceType) =>
             InvokeAsync(() => SelectedPriceType = newPriceType);

        #region Excel Download
        public async Task ExcelDownloadAsync()
        {
            try
            {
                RegionModel.PriceType = SelectedPriceType.Description();
                Logger.LogMethodStart();
                LockLoading();
                _excelDialogRef.Close();
                RegionModel.PriceType = SelectedPriceType.Description();
                RegionModel.ApplicationState = Service.Model.State.Actual.Description();
                var data = await _excelCommon.GetExcelBase64ByRegion(RegionModel, ApplicationArea.regionalbackcasting);
                var fileName = RegionModel?.DomainNamespace?.DestinationApplication.Name + "_BackcastingPlanning.xlsx";
                await JsRuntime.InvokeVoidAsync("saveAsFile", data, fileName, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
                UnlockLoading();
                Logger.LogMethodEnd();
            }
            catch (Exception ex)
            {
                UnlockLoading();
                Logger.LogMethodError(ex, "Error occurred in Backcasting ExcelDownload method in PIMS.");
            }
        }
        #endregion Excel Download
    }
}