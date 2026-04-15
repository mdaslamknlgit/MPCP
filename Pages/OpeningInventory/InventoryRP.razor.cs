using Microsoft.AspNetCore.Authorization;
using Microsoft.JSInterop;
using MPC.PlanSched.Model;
using MPC.PlanSched.Service;
using MPC.PlanSched.Shared.Common;
using MPC.PlanSched.Shared.Common.Extensions;

namespace MPC.PlanSched.UI.Pages.OpeningInventory
{
    [Authorize(Policy = PlanNSchedConstant.ViewPims)]
    public partial class InventoryRP
    {
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
                if (RegionModel != null)
                {
                    RegionName = RegionModel.RegionName;
                    RegionTitle = RegionModel.BusinessCase.Name;
                    PlanDescription = RegionModel.BusinessCase.Description;
                    ReadOnlyFlag = IsHistoricalPlan;
                    RefineriesFromDb = RegionModel.Refineries;
                    PlanUpdatedOn = "Last Saved: " + RegionModel.BusinessCase.UpdatedOn?.ToString("MM/dd/yy");
                    PlanType = RegionModel.BusinessCase.PlanType ?? string.Empty;
                }
                FirstRequestedPeriodModel = await GetFirstPeriodAsync();
                FirstRequestedPeriodModel.CorrelationId = SessionService.GetCorrelationId();
                ServiceData = FirstRequestedPeriodModel.SetupServiceData(PlanNSchedConstant.Inventory, Constant.PlanNSchedUIEvtS);
                CommonHelper.UpdateBaggage(ServiceData);
                await GetInventoryDataFromPublisherAsync();
                UnlockLoading();
                StateHasChanged();
            }
        }

        public async Task ExcelDownloadAsync()
        {
            try
            {
                logger?.LogMethodStart();
                LockLoading();
                var data = await _excelCommon.GetExcelBase64ByRegion(RegionModel, ApplicationArea.regionalplanning);
                var fileName = RegionModel?.DomainNamespace?.DestinationApplication.Name + "_Planning.xlsx";
                await JsRuntime.InvokeVoidAsync("saveAsFile", data, fileName, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
                UnlockLoading();
                logger?.LogMethodEnd();
            }
            catch (Exception ex)
            {
                UnlockLoading();
                logger?.LogMethodError(ex, "Exception occurred in ExcelDownload method for PIMS.");
            }
        }
    }
}
