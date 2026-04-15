using System.Collections.Concurrent;
using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MPC.PlanSched.Model;
using MPC.PlanSched.Service;
using MPC.PlanSched.Shared.Common;
using MPC.PlanSched.Shared.Common.Extensions;
using MPC.PlanSched.Shared.Notification.Interface;
using Newtonsoft.Json;
using Telerik.DataSource.Extensions;

namespace MPC.PlanSched.UI.Pages.InTransitInventory
{
    [Authorize(Policy = PlanNSchedConstant.ViewDpo)]
    public partial class InTransitInventoryDP
    {
        [Parameter]
        public string SelectedRole { get; set; } = string.Empty;
        [Inject]
        public INotificationService NotificationService { get; set; } = default!;
        [Inject]
        public ILogger<InTransitInventoryDP> Logger { get; set; } = default!;
        public string PlanDescription { get; set; } = string.Empty;
        public string PlanType { get; set; } = string.Empty;
        public string PeriodStartDate { get; set; } = string.Empty;
        public ServiceData ServiceData { get; set; } = new ServiceData();
        public RequestedPeriodModel FirstRequestedPeriodModel { get; set; } = new RequestedPeriodModel();
        public List<Model.InTransitInventory> InTransitInventories { get; set; } = [];
        public List<Model.InTransitInventory> InTransitInventoriesNetworkData { get; set; } = [];
        public List<Model.InTransitInventory>? InTransitInventoryDbState { get; set; } = [];
        public List<ValueTypes> OverrideTypes { get; set; } = [];
        public InTransitInventoryDPGrid DPInTransitInventoryReference { get; set; } = default!;
        public InTransitInventoryDPGrid DPNetworkInTransitInventoryReference { get; set; } = default!;
        public bool IsReady { get; set; } = false;

        public const string IntransitInventoryUI = " occurred in Intransit Inventory's ";
        public const string GetFirstPeriodMessage = "GetFirstPeriodAsync method.";
        private const string _saveInTransitInventoryException = "Error occurred while saving InTransitInventory data from UI to database.";
        public const string GetIntransitInventoryDataFromPublisherAsyncMessage = "GetIntransitInventoryDataFromPublisherAsync";
        public const string GetIntransitInventoryAsyncMessage = "GetIntransitInventoryAsync method.";
        public const string SaveIntransitInventoryException = "Error occurred while saving Intransit Inventory data from UI to database.";

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
                if (RegionModel != null)
                {
                    RegionName = RegionModel.RegionName;
                    RegionTitle = RegionModel.BusinessCase.Name;
                    PlanDescription = RegionModel.BusinessCase.Description;
                    ReadOnlyFlag = IsHistoricalPlan;
                    PlanUpdatedOn = "Last Saved: " + RegionModel.BusinessCase.UpdatedOn?.ToString(PlanNSchedConstant.DateFormatMMDDYY);
                    PlanType = RegionModel.BusinessCase.PlanType ?? string.Empty;
                }
                FirstRequestedPeriodModel = await GetFirstPeriodAsync();
                FirstRequestedPeriodModel.CorrelationId = SessionService.GetCorrelationId();
                ServiceData = FirstRequestedPeriodModel.SetupServiceData(PlanNSchedConstant.Inventory, Constant.PlanNSchedUIEvtS);
                CommonHelper.UpdateBaggage(ServiceData);

                await GetInTransitInventoryDataFromPublisherAsync();
                UnlockLoading();
                StateHasChanged();
            }
        }

        #region FunctionalEvents
        public async Task<RequestedPeriodModel> GetFirstPeriodAsync()
        {

            try
            {
                Logger.LogMethodStart();
                LockLoading();
                StateHasChanged();
                if (RegionModel == null)
                    return new();

                UserName = await ActiveUser.GetNameAsync();
                var periodModels = await UtilityUI.GetInventoryPeriodsAsync([], RegionModel, SessionService, Client, UserName);

                var firstRequestedPeriod = UtilityUI.GetInventoryFirstRequestedPeriod(periodModels, RegionModel, new(),
                    UserName);
                if (periodModels.Count > 0)
                {
                    PeriodStartDate = periodModels[0].DateTimeRange.FromDateTime.Value.ToShortDateString();
                    SelectedPeriodId = periodModels[0].PeriodID;
                }
                Logger.LogMethodEnd();
                return firstRequestedPeriod;
            }
            catch (JsonSerializationException exSz)
            {
                Logger.LogMethodError(exSz, Constant.SerializationException + IntransitInventoryUI + GetFirstPeriodMessage);
                return new();
            }
            catch (Exception ex)
            {
                Logger.LogMethodError(ex, "Exception" + IntransitInventoryUI + GetFirstPeriodMessage);
                return new();
            }
            finally
            {
                UnlockLoading();
                await InvokeAsync(() =>
                {
                    StateHasChanged();
                });
            }
        }

        public async Task GetInTransitInventoryDataFromPublisherAsync()
        {
            Logger.LogMethodStart();
            LockLoading();
            try
            {
                DataLoadingStatusMessage = ConfigurationUI.DataLoadingInProgressMessage;
                StateHasChanged();
                var functionResponses = new ConcurrentBag<AZFunctionResponse>();
                FirstRequestedPeriodModel.ApplicationState = Service.Model.State.Forecast.Description();

                var isInventoryLoadedToDb = await UtilityUI.IsInventoryDataLoadedForPeriodByInvtSourceAsync(
    FirstRequestedPeriodModel, Client, PlanNSchedConstant.InventoryType_InTransitInventory);
                var isLatestPlan = await UtilityUI.IsLatestPlanAsync(BusinessCaseId, Client);

                if (!isInventoryLoadedToDb && !ReadOnlyFlag && isLatestPlan)
                {
                    var response = await UtilityUI.RequestInTransitInventoriesAsync(
                        FirstRequestedPeriodModel, Client, PlanNSchedConstant.InventoryType_InTransitInventory, Logger);

                    functionResponses.Add(response);
                }

                await GetInTransitInventoryAsync(FirstRequestedPeriodModel.PeriodID);
                await NotificationService.SendNotificationsAsync(
                    GetPlanStatus(functionResponses.ToList()),
                    PlanNSchedConstant.InventoryPlan,
                    PlanNSchedConstant.BUSINESSMAIL,
                    FirstRequestedPeriodModel.SetupServiceData(PlanNSchedConstant.Inventory, Constant.PlanNSchedUIEvtS));

                DataLoadingStatusMessage = ConfigurationUI.DataLoadingCompletedMessage;
            }
            catch (Exception ex)
            {
                Logger.LogMethodError(ex, "Exception occurred in " + GetIntransitInventoryDataFromPublisherAsyncMessage + " method.");
            }
            finally
            {
                UnlockLoading();
                await InvokeAsync(StateHasChanged);
            }
        }

        public async Task<List<Model.InTransitInventory>> GetInTransitInventoryAsync(int periodId)
        {
            Logger.LogMethodStart();
            LockLoading();
            StateHasChanged();
            InTransitInventoryDbState = [];
            try
            {
                var inTransitInventories = new List<Model.InTransitInventory>();
                inTransitInventories = UtilityUI.GetInTransitInventoriesAsync(FirstRequestedPeriodModel, inTransitInventories, Client);
                if (inTransitInventories != null)
                {
                    UnlockLoading();
                    StateHasChanged();
                    InTransitInventories = inTransitInventories.Where(p => p.DataSource == PlanNSchedConstant.KittyHawk).ToList();
                    InTransitInventoryDbState = InTransitInventories.Select(item => new Model.InTransitInventory(item)).ToList();
                    InTransitInventoriesNetworkData = inTransitInventories.Where(p => p.DataSource == PlanNSchedConstant.PlannerManualExcel).ToList();
                    Logger.LogMethodEnd();
                }
                IsReady = true;
                return InTransitInventories;
            }
            catch (JsonSerializationException exSz)
            {
                Logger.LogMethodError(exSz, Constant.SerializationException + IntransitInventoryUI + GetIntransitInventoryAsyncMessage);
                return [];
            }
            catch (Exception ex)
            {
                Logger.LogMethodError(ex, "Exception " + IntransitInventoryUI + GetIntransitInventoryAsyncMessage);
                return [];
            }
            finally
            {
                UnlockLoading();
                await InvokeAsync(() =>
                {
                    StateHasChanged();
                });
            }
        }

        public async Task CancelInTransitInventoryForecastAsync()
        {
            try
            {
                Logger.LogMethodStart();
                LockLoading();
                StateHasChanged();

                await GetInTransitInventoryAsync(SelectedPeriodId);

                UnlockLoading();
                StateHasChanged();
                Logger.LogMethodEnd();
            }
            catch (Exception ex)
            {
                UnlockLoading();
                StateHasChanged();
                Logger.LogMethodError(ex, "Error occurred in CancelInTransitInventoryForecastAsync method.");
            }
        }

        public async Task SaveInTransitInventoryForecastAsync(List<Model.InTransitInventory> inTransitList)
        {
            Logger.LogMethodStart();
            LockLoading();
            StateHasChanged();
            try
            {
                var modifiedItems = inTransitList.Where(item =>
                     InTransitInventoryDbState.Any(originalItem =>
                     item.Id == originalItem.Id &&
                     (item.OverrideQtyCalculated != originalItem.OverrideQtyCalculated ||
                     item.OverrideLeadDay != originalItem.OverrideLeadDay)
                     ))
                     .Where(item => !item.IsInvalid)
                     .ToList();

                if (!modifiedItems.Any())
                {
                    StatusMessageContent = UIConstants.NoOverwriteRecord;
                    return;
                }

                var username = await ActiveUser.GetNameAsync();
                modifiedItems = modifiedItems.Select(item =>
                {
                    item.CreatedBy = username;
                    item.UpdatedBy = username;
                    item.CorrelationId = SessionService.GetCorrelationId();
                    item.BusinessCaseId = RegionModel?.BusinessCase?.Id;
                    return item;
                }).ToList();

                var applicationState = RegionModel.ApplicationState == Service.Model.State.Actual.Description() ? Service.Model.State.Actual.Description() : Service.Model.State.Forecast.Description();
                var saveOverrideUrl = string.Format(ConfigurationUI.SaveInTransitInventory, applicationState);
                var response = await Client.PostAsJsonAsync(saveOverrideUrl, modifiedItems);

                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    UnlockLoading();
                    StatusPopup = true;
                    Logger.LogMethodInfo("Successfully saved InTransitInventory data from UI to database.");
                    StatusMessageContent = "Data saved successfully.";

                    InTransitInventoryDbState = InTransitInventories.Select(item => new Model.InTransitInventory(item)).ToList();
                }
                else
                {
                    UnlockLoading();
                    StatusPopup = true;
                    Logger.LogMethodError(new Exception(_saveInTransitInventoryException));
                    StatusMessageContent = "Fail to save the data.";
                }
                Logger.LogMethodEnd();
            }
            catch (Exception ex)
            {
                StatusMessageContent = UIConstants.FailedToSaveData;
                Logger.LogMethodError(ex, SaveIntransitInventoryException);
            }
            finally
            {
                StatusPopup = true;
                UnlockLoading();
                await InvokeAsync(() =>
                {
                    StateHasChanged();
                });
            }
        }

        public void SortInTransitInventoryBy(Func<Model.InTransitInventory, object> sort)
        {
            InTransitInventories = InTransitInventories.OrderBy(sort).ToList();
            InvokeAsync(RebindGridAsync);
            RebindGridAsync().GetAwaiter().GetResult();
        }

        private async Task RebindGridAsync()
        {
            await DPInTransitInventoryReference!.RebindGridAsync();
            await DPNetworkInTransitInventoryReference!.RebindGridAsync();
        }
        #endregion FunctionalEvents

        #region Excel Download
        public async Task ExcelDownloadAsync()
        {
            try
            {
                Logger.LogMethodStart();
                LockLoading();
                var base64String = await _excelCommon.GetExcelBase64ByRegion(RegionModel, ApplicationArea.distributionplanning);
                var fileName = RegionModel?.DomainNamespace?.DestinationApplication.Name + "_Planning.xlsx";
                await JsRuntime.InvokeVoidAsync("saveAsFile", base64String, fileName, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
                UnlockLoading();
                Logger.LogMethodEnd();
            }
            catch (Exception ex)
            {
                UnlockLoading();
                Logger.LogMethodError(ex, "Error occurred in ExcelDownload method for DPO.");
            }
        }
        #endregion Excel Download
    }
}