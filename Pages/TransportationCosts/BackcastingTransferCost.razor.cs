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
using MPC.PlanSched.UI.Shared;
using MPC.PlanSched.UI.ViewModel;
using Newtonsoft.Json;
using Telerik.Blazor.Components;
using Telerik.DataSource;
using Telerik.DataSource.Extensions;

namespace MPC.PlanSched.UI.Pages.TransportationCosts
{
    [Authorize(Policy = PlanNSchedConstant.ViewPimsBackcasting)]
    public partial class BackcastingTransferCost
    {
        [Parameter]
        public string SelectedRole { get; set; } = string.Empty;
        [Inject]
        public IOverrideValueCalculatorService? OverrideValueCalculatorService { get; set; } = default!;
        [Inject]
        private INotificationService NotificationService { get; set; } = default!;
        [Inject]
        public ILogger<BackcastingTransferCost> Logger { get; set; } = default!;
        public string PlanDescription { get; set; } = string.Empty;
        public string PriceEffectiveDate { get; set; } = string.Empty;
        public bool LoadGroupsOnDemand { get; set; } = true;
        public List<ValueTypes> OverrideTypes { get; set; } = [];
        public List<LocationFilterOption> ToLocationOptions { get; set; } = [];
        public List<LocationFilterOption> FromLocationOptions { get; set; } = [];
        public List<ProductFilterOption> ProductOptions { get; set; } = [];

        public TelerikGrid<TransferAndCost> GridTransportationCostReference { get; set; } = new();
        public List<TransferAndCost> TransferCostSourceData { get; set; } = [];
        public List<TransferAndCost> TransferCostDataOriginalOrder { get; set; } = [];
        public List<TransferAndCost> TransferCostDbState { get; set; } = [];
        public List<TransferAndCost> TransferCostData { get; set; } = [];

        private const string _getPeriods = "GetPeriods";
        private const string _getTransportationCost = "GetTransportationCost";
        public const int TransportCostDecimalPlaces = 4;
        public const int TransportVolumeDecimalPlaces = 3;
        public static readonly decimal TransportOverrideMinimumValue = 0.005m;
        private ExcelDownloadDialog? _excelDialogRef = default!;
        private string _localTimeZoneName = PlanNSchedConstant.DefaultTimeZone;
        public RefreshDialog _refreshModal = default!;
        public NotificationDialog _notificationModal = default!;
        private bool _isReady = false;

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
                RegionModel.ApplicationState = Service.Model.State.Actual.Description();
                if (RegionModel != null)
                {
                    RegionTitle = RegionModel.BusinessCase.Name;
                    PlanUpdatedOn = "Last Saved: " + RegionModel.BusinessCase.UpdatedOn?.ToString(PlanNSchedConstant.DateFormatMMDDYY);
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
                await GetTransferCostDataFromPublisherAsync();
                UnlockLoading();
                StateHasChanged();
            }
        }

        public async Task OnReadHandlerAsync(GridReadEventArgs args)
        {
            if (!_isReady)
            {
                args.Data = Enumerable.Empty<TransferAndCost>();
                args.Total = 0;
                return;
            }

            var result = await BuildTransferGridResultAsync(args.Request, TransferCostData, x => x.ToLocationName, x => x.FromLocationName, x => x.ProductName);

            args.Data = result.Data;
            args.Total = result.Total;
            args.AggregateResults = result.AggregateResults;
        }

        public async Task<List<RequestedPeriodModel>> GetPeriodsAsync()
        {
            try
            {
                Logger.LogMethodStart();
                LockLoading();
                StateHasChanged();

                RequestedPeriods = new List<RequestedPeriodModel>();
                Periods = new List<RequestedPeriodModel>();
                if (RegionModel != null)
                {
                    UserName = await ActiveUser.GetNameAsync();
                    RequestedPeriods = UtilityUI.GetPeriodData(RequestedPeriods, RegionModel, SessionService, Client, UserName);
                    if (RequestedPeriods.Any())
                    {
                        Periods = RequestedPeriods.Where(x => x.PeriodName != PlanNSchedConstant.LastPeriodOCostName).ToList();
                        SelectedPeriodId = Periods[0].PeriodID;
                        PriceEffectiveDate = RequestedPeriods[0].PriceEffectiveDate?.ToString(Constant.DateFormat);
                    }
                    UnlockLoading();
                    StateHasChanged();
                    Logger.LogMethodEnd();
                    return Periods;
                }
                else
                {
                    UnlockLoading();
                    StateHasChanged();
                    Logger.LogMethodError(new Exception("RegionModel is null in " + _getPeriods + " method."));
                    return Periods;
                }
            }
            catch (JsonSerializationException exSz)
            {
                UnlockLoading();
                StateHasChanged();
                Logger.LogMethodError(exSz, Constant.SerializationException + " occurred in " + _getPeriods + " UI method.");
                return Periods;
            }
            catch (Exception ex)
            {
                UnlockLoading();
                StateHasChanged();
                Logger.LogMethodError(ex, "Error occurred in " + _getPeriods + " method.");
                return Periods;
            }
        }

        public async Task GetTransferCostDataFromPublisherAsync()
        {
            Logger.LogMethodStart();
            try
            {
                DataLoadingStatusMessage = ConfigurationUI.MultiPeriodDataLoadingInProgressMessage;
                StateHasChanged();
                var functionResponses = new ConcurrentBag<AZFunctionResponse>();
                var tasks = Periods.Select(async (requestedPeriodModel, index) =>
                {
                    var refineries = requestedPeriodModel.RegionRefineryName.Split(',').Where(s => !s.Equals(UIConstants.Terminal, StringComparison.OrdinalIgnoreCase)).ToList();

                    requestedPeriodModel.CorrelationId = SessionService.GetCorrelationId();
                    requestedPeriodModel.ApplicationState = Service.Model.State.Actual.Description();
                    requestedPeriodModel.PriceEntityType = PlanNSchedConstant.PriceType_TranspCost;
                    var requestUri = new Uri(ConfigurationUI.Get_AzTransportationCostFunction);

                    var refineryChecks = await Task.WhenAll(refineries.Select(async refinery =>
                    {
                        requestedPeriodModel.RegionRefineryName = refinery.ToString();
                        var isLoaded = await UtilityUI.IsTransferDataLoadedForPeriodAsync(requestedPeriodModel, Client);
                        return (Refinery: refinery, IsLoaded: isLoaded);
                    }));

                    var isTransferExist = refineryChecks.Any(x => x.IsLoaded);
                    var isPriceExist = await UtilityUI.IsTrasportationCostDataLoadedForPeriodAsync(requestedPeriodModel, Client);

                    requestedPeriodModel.RegionRefineryName = string.Join(",", refineries);
                    if (!(isTransferExist && isPriceExist) && !ReadOnlyFlag)
                    {
                        (await UtilityUI.LoadTransportationCostFromPublisherByPeriodAsync(requestedPeriodModel, isPriceExist,
                            PlanNSchedConstant.Application_CIP, requestUri, Client, Logger, isTransferExist)).ForEach(functionResponses.Add);
                    }

                    if (index == 0 && requestedPeriodModel.PeriodID != null)
                    {
                        await GetTransferCostAsync(requestedPeriodModel.PeriodID);
                    }
                }).ToList();

                await Task.WhenAll(tasks);
                await NotificationService.SendNotificationsAsync(
                    GetPlanStatus(functionResponses.ToList()),
                    PlanNSchedConstant.TransferCostPlan,
                    PlanNSchedConstant.BUSINESSMAIL,
                    Periods[0].SetupServiceData(Constant.Transfer, Constant.PlanNSchedUIEvtS));
                DataLoadingStatusMessage = ConfigurationUI.MultiPeriodDataLoadingCompletedMessage;
                Logger.LogMethodEnd();
            }
            catch (Exception ex)
            {
                Logger.LogMethodError(ex, "Error occurred in GetTransferCostDataFromPublisher method.");
            }
            finally
            {
                UnlockLoading();
                await InvokeAsync(StateHasChanged);
            }
        }

        public async Task GetTransferCostAsync(int periodId)
        {
            try
            {
                ResetGroupByButtonSelection();
                Logger.LogMethodStart();
                LockLoading();
                SelectedPeriodId = periodId;

                var transferCostDataTask = UtilityUI.GetTransferCostDataAsync(periodId, TransferCostData, Periods, Client);
                var transferCostDataResult = await transferCostDataTask;

                TransferCostData = transferCostDataResult?
                   .Where(item => CommonHelper.VolumeDecimalPrecision(item.SystemMax ?? Constant.DefaultZeroId) != Constant.DefaultZeroId)
                   .ToList();

                if (TransferCostData == null)
                    return;

                TransferCostSourceData = TransferCostData.Where(item => item.DataSource == PlanNSchedConstant.TranspCostDomain_TRR).ToList();
                TransferCostDataOriginalOrder = new List<TransferAndCost>(TransferCostData);
                TransferCostDbState = TransferCostData.Select(item => new TransferAndCost(item)).ToList();

                Logger.LogMethodEnd();
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
                _isReady = true;
                GridTransportationCostReference?.Rebind();
                UnlockLoading();
            }
        }

        public async Task SaveTransportationCostOverridesAsync(List<TransferAndCost> transportationCosts)
        {
            Logger.LogMethodStart();
            LockLoading();
            StateHasChanged();
            try
            {
                var modifiedItems = transportationCosts.Where(item =>
                    TransferCostDbState.Any(originalItem =>
                    originalItem.Id == item.Id &&
                    (originalItem.OverrideMinCalculated != item.OverrideMinCalculated ||
                    originalItem.OverrideMaxCalculated != item.OverrideMaxCalculated ||
                    originalItem.OverrideCostCalculated != item.OverrideCostCalculated ||
                    originalItem.Comments != item.Comments)
                ))
                .Where(item => !item.IsInvalid && !item.IsTransferInvalid)
                .ToList();

                if (!modifiedItems.Any())
                {
                    StatusMessageContent = UIConstants.NoOverwriteRecord;
                    return;
                }

                var validationMessage = ValidateOverrideCommentsBeforeSave(modifiedItems);
                if (validationMessage != null)
                {
                    UnlockLoading();
                    StatusPopup = true;
                    StatusMessageContent = validationMessage;
                    StateHasChanged();
                    return;
                }

                var username = await ActiveUser.GetNameAsync();
                modifiedItems = modifiedItems.Select(item =>
                {
                    item.CreatedBy = username;
                    item.UpdatedBy = username;
                    item.CorrelationId = SessionService.GetCorrelationId();
                    item.BusinessCaseId = RegionModel.BusinessCase.Id;
                    item.Comments = item.Comments?.Trim();
                    return item;
                }).ToList();

                var applicationState = Service.Model.State.Actual.Description();
                var saveOverrideUrl = string.Format(ConfigurationUI.SaveTransferAndCost, applicationState);
                var response = await Client.PostAsJsonAsync(saveOverrideUrl, modifiedItems);

                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    StatusMessageContent = UIConstants.SuccessfullySavedData;
                    TransferCostDbState = TransferCostData.Select(item => new TransferAndCost(item)).ToList();
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

        public async Task CancelTransportCostAsync()
        {
            try
            {
                Logger.LogMethodStart();
                LockLoading();
                StateHasChanged();
                await GetTransferCostAsync(SelectedPeriodId);

                UnlockLoading();
                StateHasChanged();
                Logger.LogMethodEnd();
            }
            catch (Exception ex)
            {
                UnlockLoading();
                StateHasChanged();
                Logger.LogMethodError(ex, "Error occurred in CancelTransportCostAsync method.");
            }
        }

        public async Task GetTransferCostOnPeriodSelectionChangedAsync(int periodId)
        {
            try
            {
                Logger.LogMethodStart();
                LockLoading();
                StateHasChanged();

                await GetTransferCostAsync(periodId);

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

        public void SortTransportationCostBy(Func<TransferAndCost, object> sort)
        {
            TransferCostData = TransferCostData.OrderBy(sort).ToList();
            GridTransportationCostReference?.Rebind();
            StateHasChanged();
        }

        private Task OnPriceTypeChanged(PriceType newPriceType) =>
             InvokeAsync(() => SelectedPriceType = newPriceType);

        protected async Task ToggleTransferCostGridGroupByCollapseAsync()
        {
            LoadGroupsOnDemand = !LoadGroupsOnDemand;
            GridTransportationCostReference.LoadGroupsOnDemand = LoadGroupsOnDemand;
            await GridTransportationCostReference.SetStateAsync(GridTransportationCostReference.GetState());
        }

        public void ValidateCommentsLength(ChangeEventArgs e, object context)
        {
            var dataRow = (TransferAndCost)context;
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

        public string ValidateOverrideCommentsBeforeSave(List<TransferAndCost> modifiedItems)
        {
            var requireComment = false;
            var commentTooLong = false;
            var invalidComment = false;

            foreach (var item in modifiedItems)
            {
                var overrideChanged =
                    item.SystemMin != item.OverrideMinCalculated ||
                    item.SystemMax != item.OverrideMaxCalculated ||
                    item.SystemCost != item.OverrideCostCalculated;

                var comment = item.Comments;

                if (overrideChanged && string.IsNullOrWhiteSpace(comment))
                {
                    requireComment = true;
                    break;
                }
                if (comment?.Trim().Length > ConfigurationUI.CommentsMaxLength)
                {
                    commentTooLong = true;
                    break;
                }
                if (!string.IsNullOrEmpty(comment) && comment.All(char.IsWhiteSpace))
                {
                    invalidComment = true;
                    break;
                }
            }

            if (requireComment)
                return "Please enter valid comments for overrides before saving.";

            if (commentTooLong)
                return $"Comments cannot exceed {ConfigurationUI.CommentsMaxLength} characters.";

            if (invalidComment)
                return "Please enter valid comments before saving.";

            return null;
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

        public async Task<DataSourceResult> BuildTransferGridResultAsync<T>(DataSourceRequest request, IList<T> source,
            Func<T, string> toLocationSelector, Func<T, string> fromLocationSelector, Func<T, string> productSelector)
        {
            if (ToLocationOptions.Count == 0 || FromLocationOptions.Count == 0 || ProductOptions.Count == 0)
            {
                ToLocationOptions = GetToLocationsFromService(source, toLocationSelector);
                FromLocationOptions = GetFromLocationsFromService(source, fromLocationSelector);
                ProductOptions = GetProductsFromService(source, productSelector);
            }

            IEnumerable<T> data = source;

            if (request.Filters.Any())
            {
                var selectors = new Dictionary<string, Func<T, string>>
                {
                    { "ToLocationName", toLocationSelector },
                    { "FromLocationName", fromLocationSelector },
                    { "ProductName", productSelector }
                };
                ApplyFilters(request.Filters, ref data, selectors);
            }

            return await data.ToDataSourceResultAsync(request);
        }

        public List<LocationFilterOption> GetToLocationsFromService<T>(IList<T> source, Func<T, string> toLocationSelector)
        {
            if (source == null || source.Count == 0)
                return [];

            return source
                .Select(toLocationSelector)
                .Distinct()
                .OrderBy(x => x)
                .Select(t => new LocationFilterOption { ToLocationName = t })
                .ToList();
        }

        public List<LocationFilterOption> GetFromLocationsFromService<T>(IList<T> source, Func<T, string> fromLocationSelector)
        {
            if (source == null || source.Count == 0)
                return [];

            return source
                .Select(fromLocationSelector)
                .Distinct()
                .OrderBy(x => x)
                .Select(t => new LocationFilterOption { FromLocationName = t })
                .ToList();
        }

        #region override calculation and setting the values
        public void OverrideCostValue(ChangeEventArgs e, object context, string overrideType)
        {
            var dataRow = (TransferAndCost)context;
            OverrideValue(e, dataRow, overrideType, dataRow.SystemCost, dataRow.SetCostOverride, dataRow.ClearCostOverride);
        }
        public void OverrideMinValue(ChangeEventArgs e, object context, string overrideType)
        {
            var dataRow = (TransferAndCost)context;
            OverrideValue(e, dataRow, overrideType, dataRow.SystemMin, dataRow.SetMinOverride, dataRow.ClearMinOverride);
        }
        public void OverrideMaxValue(ChangeEventArgs e, object context, string overrideType)
        {
            var dataRow = (TransferAndCost)context;
            OverrideValue(e, dataRow, overrideType, dataRow.SystemMin, dataRow.SetMaxOverride, dataRow.ClearMaxOverride);
        }

        public void OverrideValue(ChangeEventArgs e, TransportationCost dataRow, string overrideType, decimal? systemBoundedValue, Action<OverrideCalculationResult> setOverrideValues, Action clearOverride)
        {
            if (!decimal.TryParse(e.Value?.ToString(), out var value))
            {
                clearOverride();
                GridTransportationCostReference?.Rebind();
                return;
            }

            var calculatedResult = OverrideValueCalculatorService.CalculateUsing(overrideType).CalculateOverride(value, systemBoundedValue);
            setOverrideValues(calculatedResult);
            GridTransportationCostReference?.Rebind();
        }

        public static decimal? GetValue(decimal? systemValue, decimal? calculatedValue, decimal? OverrideValue) =>
            (systemValue != calculatedValue) ? OverrideValue : null;
        #endregion override calculation and setting the values
    }
}