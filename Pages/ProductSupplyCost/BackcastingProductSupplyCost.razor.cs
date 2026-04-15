using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MPC.PlanSched.Model;
using MPC.PlanSched.Service;
using MPC.PlanSched.Shared.Common;
using MPC.PlanSched.Shared.Common.Extensions;
using MPC.PlanSched.UI.Pages.ProductSupplyCost.Components;
using MPC.PlanSched.UI.Shared;

namespace MPC.PlanSched.UI.Pages.ProductSupplyCost
{
    [Authorize(Policy = PlanNSchedConstant.ViewPimsBackcasting)]
    public partial class BackcastingProductSupplyCost : BackcastingProductSupplyCostBaseComponent
    {
        private ExcelDownloadDialog? _excelDialogRef = default!;
        private string _localTimeZoneName = PlanNSchedConstant.DefaultTimeZone;

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);
            if (firstRender)
            {
                LockLoading();
                StateHasChanged();
                Client = ClientFactory.CreateClient("WebAPI");
                Client.Timeout = TimeSpan.FromSeconds(600);
                _localTimeZoneName = SessionService.GetLocalTimezoneName();
                OverrideTypes = await UtilityUI.GetOverrideTypes(Client);
                RegionModel = await UtilityUI.GetRegionByBusinessCaseIdAsync(BusinessCaseId, SessionService.GetCorrelationId(), Client);
                if (RegionModel != null)
                {
                    RegionModel.ApplicationState = Service.Model.State.Actual.Description();
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

                await GetSupplyAndCostDataFromPublisherAsync(false);
                UnlockLoading();
                StateHasChanged();
            }
        }

        private Task OnPriceTypeChanged(PriceType newPriceType) =>
             InvokeAsync(() => SelectedPriceType = newPriceType);

        public void ValidateCommentsLength(ChangeEventArgs e, object context)
        {
            var dataRow = (ProductSupplyAndCost)context;
            var maxLength = ConfigurationUI.CommentsMaxLength;
            var input = e.Value?.ToString()?.Trim() ?? string.Empty;
            if (input.Length > maxLength)
            {
                StatusPopup = true;
                StatusMessageContent = $"Comments cannot exceed {maxLength} characters.";
                StateHasChanged();
            }
            else
            {
                dataRow.Comments = e.Value?.ToString();
            }
        }

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
                UnlockLoading();
                Logger.LogMethodEnd();
            }
            catch (Exception ex)
            {
                UnlockLoading();
                Logger.LogMethodError(ex, "Error occurred in ExcelDownload method in PIMS.");
            }
        }
        #endregion Excel Download
    }
}