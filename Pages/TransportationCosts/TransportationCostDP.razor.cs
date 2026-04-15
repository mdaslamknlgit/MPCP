using System.Collections.Concurrent;
using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MPC.PlanSched.Model;
using MPC.PlanSched.Service;
using MPC.PlanSched.Shared.Common;
using MPC.PlanSched.Shared.Common.Extensions;
using MPC.PlanSched.Shared.Common.Service;
using MPC.PlanSched.Shared.Notification.Interface;
using Newtonsoft.Json;
using Telerik.Blazor.Components;
using Telerik.DataSource.Extensions;

namespace MPC.PlanSched.UI.Pages.TransportationCosts
{
    [Authorize(Policy = "ViewDPO")]
    public partial class TransportationCostDP
    {
        [Parameter]
        public string SelectedRole { get; set; } = string.Empty;
        [Inject]
        public IOverrideValueCalculatorService? OverrideValueCalculatorService { get; set; } = default!;
        [Inject]
        private INotificationService NotificationService { get; set; } = default!;
        [Inject]
        public ILogger<TransportationCostDP> Logger { get; set; } = default!;
        public string PlanDescription { get; set; } = string.Empty;
        public string PlanType { get; set; } = string.Empty;
        public bool LoadGroupsOnDemand { get; set; } = true;
        public List<ValueTypes> OverrideTypes { get; set; } = [];
        public TelerikGrid<TransportationCost> GridTransportationCostReference { get; set; } = new();
        public List<TransportationCost> TransportationCostData { get; set; } = [];
        public List<TransportationCost> TransportationCostSourceData { get; set; } = [];
        public List<TransportationCost> TransportationCostNetworkData { get; set; } = [];
        public List<TransportationCost> TransportationCostDataOriginalOrder { get; set; } = [];
        public List<TransportationCost> TransportationCostDbState { get; set; } = [];
        public bool IsReady { get; set; } = false;
        public TransportationCostDPGrid DPTransportationReference { get; set; } = default!;
        public TransportationCostDPGrid DPNetworkTransportationReference { get; set; } = default!;

        private const string _getPeriods = "GetPeriods";
        private const string _getTransportationCost = "GetTransportationCost";
        public const int TransportCostDecimalPlaces = 4;

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
                    PlanUpdatedOn = "Last Saved: " + RegionModel.BusinessCase.UpdatedOn?.ToString(PlanNSchedConstant.DateFormatMMDDYY);
                    RegionName = RegionModel.RegionName;
                    PlanDescription = RegionModel.BusinessCase.Description;
                    ReadOnlyFlag = IsHistoricalPlan;
                    PlanType = RegionModel.BusinessCase.PlanType ?? string.Empty;
                }
                await GetPeriodsAsync();
                CommonHelper.UpdateBaggage(SessionService.GetCorrelationId(),
                    RegionModel?.BusinessCase?.Id,
                    RequestedPeriods?.FirstOrDefault()?.PeriodID,
                    RegionModel?.DomainNamespace?.DestinationApplication.Name);
                await GetTransportationCostDataFromPublisherAsync();
                UnlockLoading();
                StateHasChanged();
            }
        }

        private async Task RebindGridAsync()
        {
            await DPTransportationReference!.RebindGridAsync();
            await DPNetworkTransportationReference!.RebindGridAsync();
        }

        public async Task<List<RequestedPeriodModel>> GetPeriodsAsync()
        {
            Logger.LogMethodStart();
            LockLoading();
            StateHasChanged();
            try
            {
                if (RegionModel == null)
                    return new();

                UserName = await ActiveUser.GetNameAsync();
                var requestedPeriods = await UtilityUI.GetTransportationCostPeriodDataAsync([], RegionModel, SessionService, Client, UserName);
                if (requestedPeriods.Any())
                {
                    Periods = requestedPeriods.Take(requestedPeriods.Count - 1).ToList();
                    SelectedPeriodId = Periods[0].PeriodID;
                }
                Logger.LogMethodEnd();
                return Periods;
            }
            catch (Mpc.Helios.Exceptions.SerializationException exSz)
            {
                Logger.LogMethodError(exSz, Constant.SerializationException + " occurred in " + _getPeriods + " method.");
                return Periods;
            }
            catch (Mpc.Helios.Exceptions.NullReferenceException exNull)
            {
                Logger.LogMethodError(exNull, Constant.NullReferenceException + " occurred in " + _getPeriods + " method.");
                return Periods;
            }
            catch (Exception ex)
            {
                Logger.LogMethodError(ex, "Error occurred in " + _getPeriods + " method.");
                return Periods;
            }
            finally
            {
                UnlockLoading();
                StateHasChanged();
            }
        }

        public async Task GetTransportationCostDataFromPublisherAsync()
        {
            Logger.LogMethodStart();
            try
            {
                LockLoading();
                DataLoadingStatusMessage = ConfigurationUI.MultiPeriodDataLoadingInProgressMessage;
                StateHasChanged();
                var functionResponses = new ConcurrentBag<AZFunctionResponse>();
                var isLatestPlan = await UtilityUI.IsLatestPlanAsync(BusinessCaseId, Client);
                var tasks = Periods.Select(async (requestedPeriodModel, index) =>
                {
                    requestedPeriodModel.CorrelationId = SessionService.GetCorrelationId();
                    requestedPeriodModel.ApplicationState = Service.Model.State.Forecast.Description();
                    requestedPeriodModel.PriceEntityType = PlanNSchedConstant.PriceType_TranspCost;
                    var requestUri = new Uri(ConfigurationUI.Get_AzTransportationCostFunction);

                    var isPriceExist = await UtilityUI.IsTrasportationCostDataLoadedForPeriodAsync(requestedPeriodModel, Client);

                    if (!isPriceExist && !ReadOnlyFlag && isLatestPlan)
                    {
                        (await UtilityUI.LoadTransportationCostFromPublisherByPeriodAsync(requestedPeriodModel, isPriceExist,
                            PlanNSchedConstant.Application_CIP, requestUri, Client, Logger)).ForEach(functionResponses.Add);
                    }

                    if (index == 0 && requestedPeriodModel.PeriodID != null)
                    {
                        await GetTransportationCostAsync(requestedPeriodModel.PeriodID);
                    }
                }).ToList();

                await Task.WhenAll(tasks);
                await NotificationService.SendNotificationsAsync(
                   GetPlanStatus(functionResponses.ToList()),
                    PlanNSchedConstant.TranspPlan,
                    PlanNSchedConstant.BUSINESSMAIL,
                   Periods[0].SetupServiceData(PlanNSchedConstant.TranspMode, Constant.PlanNSchedUIEvtS));
                DataLoadingStatusMessage = ConfigurationUI.MultiPeriodDataLoadingCompletedMessage;
                Logger.LogMethodEnd();
            }
            catch (Exception ex)
            {
                Logger.LogMethodError(ex, "Error occurred in GetTransportationCostDataFromPublisher method.");
            }
            finally
            {
                UnlockLoading();
                await InvokeAsync(StateHasChanged);
            }
        }

        public async Task GetTransportationCostAsync(int periodId)
        {
            try
            {
                ResetGroupByButtonSelection();
                Logger.LogMethodStart();
                LockLoading();
                SelectedPeriodId = periodId;
                TransportationCostData = await UtilityUI.GetTransportationCostDataAsync(periodId, TransportationCostData, Periods, Client);

                if (TransportationCostData == null)
                    return;

                TransportationCostSourceData = TransportationCostData.Where(item => item.DataSource == PlanNSchedConstant.TranspCostDomain_TRR).ToList();
                TransportationCostNetworkData = TransportationCostData.Where(item => item.DataSource == PlanNSchedConstant.PlannerManualExcel).ToList();

                TransportationCostDataOriginalOrder = new List<TransportationCost>(TransportationCostData);
                TransportationCostDbState = TransportationCostData.Select(item => new TransportationCost(item)).ToList();

                Logger.LogMethodEnd();
                IsReady = true;
            }
            catch (JsonSerializationException exSz)
            {
                Logger.LogMethodError(exSz, Constant.SerializationException + " occurred in " + _getTransportationCost + " method.");
            }
            catch (Mpc.Helios.Exceptions.NullReferenceException exNull)
            {
                Logger.LogMethodError(exNull, Constant.NullReferenceException + " occurred in " + _getTransportationCost + " method.");
            }
            catch (Exception ex)
            {
                Logger.LogMethodError(ex, "Error occurred in " + _getTransportationCost + " UI method.");
            }
            finally
            {
                UnlockLoading();
            }
        }

        public async Task SaveTransportCostForecastAsync(List<TransportationCost> transportationCosts)
        {
            Logger.LogMethodStart();
            LockLoading();
            StateHasChanged();
            try
            {
                var modifiedItems = transportationCosts.Where(item =>
                    TransportationCostDbState.Any(originalItem =>
                    originalItem.Id == item.Id && originalItem.OverrideCostCalculated != item.OverrideCostCalculated))
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
                    item.BusinessCaseId = RegionModel.BusinessCase.Id;
                    return item;
                }).ToList();

                var applicationState = RegionModel.ApplicationState == Service.Model.State.Actual.Description() ? Service.Model.State.Actual.Description() : Service.Model.State.Forecast.Description();
                var saveOverrideUrl = string.Format(ConfigurationUI.SaveTransportationCost, applicationState);
                var response = await Client.PostAsJsonAsync(saveOverrideUrl, modifiedItems);

                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    StatusMessageContent = UIConstants.SuccessfullySavedData;
                    TransportationCostDbState = TransportationCostData.Select(item => new TransportationCost(item)).ToList();
                    Logger.LogMethodEnd();
                }
                else
                {
                    StatusMessageContent = UIConstants.FailedToSaveData;
                }
            }
            catch (Exception ex)
            {
                StatusMessageContent = UIConstants.FailedToSaveData;
                Logger.LogMethodError(ex, "Error occurred in SaveTransportCost UI method.");
            }
            finally
            {
                StatusPopup = true;
                UnlockLoading();
                StateHasChanged();
            }
        }

        public async Task CancelTransportCostForecastAsync()
        {
            try
            {
                Logger.LogMethodStart();
                LockLoading();
                StateHasChanged();

                await GetTransportationCostAsync(SelectedPeriodId);

                UnlockLoading();
                StateHasChanged();
                Logger.LogMethodEnd();
            }
            catch (Exception ex)
            {
                UnlockLoading();
                StateHasChanged();
                Logger.LogMethodError(ex, "Error occurred in CancelTransportCostForecastAsync method.");
            }
        }

        public async Task GetTransportationCostOnPeriodSelectionChangedAsync(int periodId)
        {
            try
            {
                Logger.LogMethodStart();
                LockLoading();
                StateHasChanged();

                await GetTransportationCostAsync(periodId);

                UnlockLoading();
                StateHasChanged();
                Logger.LogMethodEnd();
            }
            catch (Exception ex)
            {
                UnlockLoading();
                StateHasChanged();
                Logger.LogMethodError(ex, "Error occurred in PeriodSelectionChangedAsync method.");
            }
        }

        public void SortTransportationCostBy(Func<TransportationCost, object> sort)
        {
            TransportationCostSourceData = TransportationCostSourceData.OrderBy(sort).ToList();
            TransportationCostNetworkData = TransportationCostNetworkData.OrderBy(sort).ToList();
            InvokeAsync(() => RebindGridAsync());
            GridTransportationCostReference?.Rebind();
            StateHasChanged();
        }

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
                Logger.LogMethodError(ex, "Error occurred in ExcelDownload method in DPO.");
            }
        }
    }
}