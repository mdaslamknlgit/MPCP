using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MPC.PlanSched.Model;
using MPC.PlanSched.Service;
using MPC.PlanSched.Shared.Common;
using MPC.PlanSched.Shared.Common.Extensions;
using MPC.PlanSched.UI.Shared;
using Telerik.Blazor.Components;
using Telerik.DataSource.Extensions;

namespace MPC.PlanSched.UI.Pages.OpeningInventory
{
    public partial class BackcastingRefineryInventory
    {
        public RefreshDialog _refreshModal = default!;
        public NotificationDialog _notificationModal = default!;
        private ExcelDownloadDialog? _excelDialogRef = default!;
        private string _localTimeZoneName = PlanNSchedConstant.DefaultTimeZone;
        public bool LoadGroupsOnInventory { get; set; } = true;
        private bool _isReady = false;

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);
            if (firstRender)
            {
                LockLoading();
                StateHasChanged();
                Client = ClientFactory.CreateClient(PlanNSchedConstant.WebAPI);
                Client.Timeout = TimeSpan.FromSeconds(PlanNSchedConstant.Timeout);
                OverrideTypes = await UtilityUI.GetOverrideTypes(Client);
                RegionModel = await UtilityUI.GetRegionByBusinessCaseIdAsync(BusinessCaseId, SessionService.GetCorrelationId(), Client);
                RegionModel.ApplicationState = Service.Model.State.Actual.Description();
                if (RegionModel != null)
                {
                    RegionName = RegionModel.RegionName;
                    RegionTitle = RegionModel.BusinessCase.Name;
                    PlanDescription = RegionModel.BusinessCase.Description;
                    ReadOnlyFlag = IsHistoricalPlan;
                    RefineriesFromDb = RegionModel.Refineries;
                    PlanUpdatedOn = "Last Saved: " + RegionModel.BusinessCase.UpdatedOn?.ToString(PlanNSchedConstant.DateFormatMMDDYY);
                    RegionModel.IsHierarchy = false;
                }
                FirstRequestedPeriodModel = await GetFirstPeriodAsync();
                FirstRequestedPeriodModel.CorrelationId = SessionService.GetCorrelationId();
                ServiceData = FirstRequestedPeriodModel.SetupServiceData(PlanNSchedConstant.Inventory, Constant.PlanNSchedUIEvtS);
                CommonHelper.UpdateBaggage(ServiceData);
                await GetBackcastingInventoryDataFromPublisherAsync();
                _isReady = true;
                GridInventoryReference?.Rebind();
                UnlockLoading();
                StateHasChanged();
            }
        }

        private Task OnPriceTypeChanged(PriceType newPriceType) =>
            InvokeAsync(() => SelectedPriceType = newPriceType);

        public async Task OnReadHandlerAsync(GridReadEventArgs args)
        {
            if (!_isReady)
            {
                args.Data = Enumerable.Empty<Model.OpeningInventory>();
                args.Total = 0;
                return;
            }
            if (LocationOptions.Count == 0 || ProductOptions.Count == 0)
            {
                LocationOptions = GetLocationsFromService();
                ProductOptions = GetProductsFromService();
            }

            IEnumerable<Model.OpeningInventory> data = OpeningInventories;

            if (args.Request.Filters.Any())
                ApplyFilters(args.Request.Filters, ref data);


            var result = await data.ToDataSourceResultAsync(args.Request);

            args.Data = result.Data;
            args.Total = result.Total;
            args.AggregateResults = result.AggregateResults;
        }


        public void ValidateCommentsLength(ChangeEventArgs e, object context)
        {
            var dataRow = (Model.OpeningInventory)context;
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

        protected async Task ToggleBackcastingInventoryGridGroupByCollapseAsync()
        {
            LoadGroupsOnInventory = !LoadGroupsOnInventory;
            GridInventoryReference.LoadGroupsOnDemand = LoadGroupsOnInventory;
            await GridInventoryReference.SetStateAsync(GridInventoryReference.GetState());
        }

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

    }
}
