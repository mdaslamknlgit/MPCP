using Microsoft.AspNetCore.Authorization;
using Microsoft.JSInterop;
using MPC.PlanSched.Model;
using MPC.PlanSched.Service;
using MPC.PlanSched.Shared.Common;
using MPC.PlanSched.Shared.Common.Extensions;

namespace MPC.PlanSched.UI.Pages.ProductDemandPrice
{
    [Authorize(Policy = PlanNSchedConstant.ViewDpo)]
    public partial class ProductDemandPriceDP
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
                    RegionTitle = RegionModel.BusinessCase.Name;
                    PlanUpdatedOn = "Last Saved: " + (RegionModel.BusinessCase.UpdatedOn?.ToString("MM/dd/yy") ?? "");
                    RegionName = RegionModel.RegionName;
                    PlanDescription = RegionModel.BusinessCase.Description;

                    if (!ConfigurationUI.IsMidtermEnabled)
                        PlanType = RegionModel.BusinessCase.PlanType ?? string.Empty;
                    IsHistoricalData = IsHistoricalPlan;
                    if (RegionName.Contains(Constant.SouthRegion))
                    {
                        IsAggregatedTierVisible = false;
                        ReadOnlyFlag = IsHistoricalData;
                    }
                    else
                    {
                        ReadOnlyFlag = true;
                        IsAggregatedTierVisible = true;
                        IsAggregatedTier = false;
                    }
                }

                await GetPeriodsAsync();

                CommonHelper.UpdateBaggage(SessionService.GetCorrelationId(),
                    RegionModel?.BusinessCase?.Id,
                    RequestedPeriods?.FirstOrDefault()?.PeriodID,
                    RegionModel?.DomainNamespace?.DestinationApplication.Name);

                await GetDemandAndPriceDataFromPublisherAsync();
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
                var data = await _excelCommon.GetExcelBase64ByRegion(RegionModel, ApplicationArea.distributionplanning);
                var fileName = RegionModel?.DomainNamespace?.DestinationApplication.Name + "_Planning.xlsx";
                await JsRuntime.InvokeVoidAsync("saveAsFile", data, fileName, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            }
            catch (Exception ex)
            {
                logger?.LogMethodError(ex, "Error occurred in ExcelDownload method for DPO.");
            }
            finally
            {
                UnlockLoading();
                logger?.LogMethodEnd();
            }
        }
    }
}