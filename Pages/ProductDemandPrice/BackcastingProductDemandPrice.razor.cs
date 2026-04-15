using Microsoft.AspNetCore.Authorization;
using Microsoft.JSInterop;
using MPC.PlanSched.Model;
using MPC.PlanSched.Service;
using MPC.PlanSched.Shared.Common;
using MPC.PlanSched.Shared.Common.Extensions;
using MPC.PlanSched.UI.Shared;

namespace MPC.PlanSched.UI.Pages.ProductDemandPrice
{
    [Authorize(Policy = PlanNSchedConstant.ViewPimsBackcasting)]
    public partial class BackcastingProductDemandPrice
    {
        public RefreshDialog _refreshModal = default!;
        public NotificationDialog _notificationModal = default!;
        private ExcelDownloadDialog? _excelDialogRef = default!;
        private string _localTimeZoneName = PlanNSchedConstant.DefaultTimeZone;

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);

            if (firstRender)
            {
                LockLoading();
                StateHasChanged();
                IsAggregatedTier = false;
                Client = ClientFactory.CreateClient(PlanNSchedConstant.WebAPI);
                Client.Timeout = TimeSpan.FromSeconds(PlanNSchedConstant.Timeout);
                _localTimeZoneName = SessionService.GetLocalTimezoneName();
                OverrideTypes = await UtilityUI.GetOverrideTypes(Client);
                RegionModel = await UtilityUI.GetRegionByBusinessCaseIdAsync(BusinessCaseId, SessionService.GetCorrelationId(), Client);
                if (RegionModel != null)
                {
                    RegionModel.ApplicationState = Service.Model.State.Actual.Description();
                    RegionModel.IsHierarchy = false;
                    RegionTitle = RegionModel.BusinessCase.Name;
                    PlanUpdatedOn = "Last Saved: " + (RegionModel.BusinessCase.UpdatedOn?.ToString("MM/dd/yy") ?? "");
                    RegionName = RegionModel.RegionName;
                    PlanDescription = RegionModel.BusinessCase.Description;
                    ReadOnlyFlag = IsHistoricalData = IsHistoricalPlan;
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

        protected new async Task ToggleDemandGridGroupByCollapseAsync()
        {
            LoadGroupsOnDemand = !LoadGroupsOnDemand;
            GridDemandReference.LoadGroupsOnDemand = LoadGroupsOnDemand;
            await GridDemandReference.SetStateAsync(GridDemandReference.GetState());
        }

        private Task OnPriceTypeChanged(PriceType newPriceType) =>
            InvokeAsync(() => SelectedPriceType = newPriceType);

        #region Excel Download
        public async Task ExcelDownloadAsync()
        {
            try
            {
                Logger.LogMethodStart();
                LockLoading();
                _excelDialogRef.Close();
                RegionModel.PriceType = SelectedPriceType.Description();
                RegionModel.ApplicationState = Service.Model.State.Actual.Description();
                var data = await _excelCommon.GetExcelBase64ByRegion(RegionModel, ApplicationArea.regionalbackcasting);
                var fileName = RegionModel?.DomainNamespace?.DestinationApplication.Name + "_BackcastingPlanning.xlsx";
                await JsRuntime.InvokeVoidAsync("saveAsFile", data, fileName, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            }
            catch (Exception ex)
            {
                Logger.LogMethodError(ex, "Error occurred in ExcelDownload method for PIMS.");
            }
            finally
            {
                UnlockLoading();
                Logger.LogMethodEnd();
            }
        }
        #endregion Excel Download
    }
}