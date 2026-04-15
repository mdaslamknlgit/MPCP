using System.Collections.Concurrent;
using Microsoft.AspNetCore.Components;
using Microsoft.FeatureManagement;
using MPC.PlanSched.Model;
using MPC.PlanSched.Service;
using MPC.PlanSched.Shared.Common;
using MPC.PlanSched.Shared.Common.Extensions;
using MPC.PlanSched.Shared.Common.Service;
using MPC.PlanSched.Shared.Notification.Interface;
using MPC.PlanSched.UI.ViewModel;
using Newtonsoft.Json;
using Telerik.Blazor.Components;
using Telerik.DataSource.Extensions;

namespace MPC.PlanSched.UI.Pages.ProductDemandPrice
{
    public class ProductDemandPriceBaseComponent : PlanBaseComponent
    {
        [Parameter]
        public string SelectedRole { get; set; } = string.Empty;
        [Inject]
        public IOverrideValueCalculatorService? OverrideValueCalculatorService { get; set; } = default!;
        [Inject]
        public INotificationService NotificationService { get; set; } = default!;
        [Inject]
        public ILogger<ProductDemandPriceBaseComponent> Logger { get; set; } = default!;
        public string PlanDescription { get; set; } = string.Empty;
        public string PriceEffectiveDate { get; set; } = string.Empty;
        public string PlanType { get; set; } = string.Empty;
        public bool LoadGroupsOnDemand { get; set; } = true;
        public object[]? TotalMinDemandLineChartXAxisLabels;
        public TelerikGrid<ProductDemandAndPrice> GridDemandReference { get; set; } = default!;
        public ProductDemandPriceDPGrid DPDemandReference { get; set; } = default!;
        public ProductDemandPriceDPGrid DPNetworkDemandReference { get; set; } = default!;

        public List<ProductDemandAndPrice> ProductDemandAndPriceData { get; set; } = [];
        public List<ProductDemandAndPrice> ProductDemandAndPriceSourceData { get; set; } = [];
        public List<ProductDemandAndPrice> ProductDemandAndPriceRegionalData { get; set; } = [];
        public List<ProductDemandAndPrice> ProductDemandAndPriceRefineryData { get; set; } = [];
        public List<ProductDemandAndPrice> ProductDemandAndPriceNetworkData { get; set; } = [];
        public List<ProductDemandAndPrice> ProductSellAndPriceRefineryData { get; set; } = [];
        public List<ProductDemandAndPrice>? ProductDemandAndPriceDbState { get; set; } = [];
        public List<ProductDemandAndPrice>? ProductDemandAndPriceSourceDbState { get; set; } = [];
        public ProductDemandPriceRPGrid RPDemandReference { get; set; } = default!;
        public ProductDemandPriceRPGrid RPRegionalDemandReference { get; set; } = default!;
        public ProductDemandPriceRPGrid RPRefineryDemandReference { get; set; } = default!;
        public List<LineChartSeries> TotalMinDemandLineChartSeries { get; set; } = [];
        public List<LineChartSeries> TotalMinDemandLineChartSeriesVisible { get; set; } = [];
        public List<ValueTypes> OverrideTypes { get; set; } = [];
        public bool IsAggregatedTier { get; set; }
        public bool IsAggregatedTierVisible { get; set; }
        public bool IsHistoricalData { get; set; }
        public string? ApplicationState { get; set; }
        public bool IsTierVisible { get; set; } = true; // make it true in case of tier needs to show on south pims
        public bool IsReady { get; set; } = false;

        public const string GetPeriods = "GetPeriodsAsync";
        public const string GetProductDemandPrices = "GetProductDemandPricesAsync";
        public const string GetDemandAndPriceDataFromPublisher = "GetDemandAndPriceDataFromPublisherAsync";
        public const string SaveProductDemandPrice = "SaveProductDemandPriceAsync";
        public const string GetProductDemandPricesOnPeriodSelectionChanged = "GetProductDemandPricesOnPeriodSelectionChangedAsync";
        public const int DemandPriceDecimalPlaces = 4;
        public const int DemandQuantityDecimalPlaces = 6;
        public const decimal DemandOverrideMinimumValue = 0.005m;
        [Inject]
        public IFeatureManager FeatureManager { get; set; } = default!;


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
                    Logger.LogMethodError(new Exception("RegionModel is null in " + GetPeriods + " method."));
                    return Periods;
                }
            }
            catch (JsonSerializationException exSz)
            {
                UnlockLoading();
                StateHasChanged();
                Logger.LogMethodError(exSz, Constant.SerializationException + " occurred in " + GetPeriods + " UI method.");
                return Periods;
            }
            catch (Exception ex)
            {
                UnlockLoading();
                StateHasChanged();
                Logger.LogMethodError(ex, "Error occurred in " + GetPeriods + " method.");
                return Periods;
            }
        }

        public async Task GetDemandAndPriceDataFromPublisherAsync()
        {
            Logger.LogMethodStart();
            try
            {
                LockLoading();
                DataLoadingStatusMessage = ConfigurationUI.MultiPeriodDataLoadingInProgressMessage;
                StateHasChanged();
                var functionResponses = new ConcurrentBag<AZFunctionResponse>();
                var isLatestPlan = true;
                if (RegionModel.ApplicationState != Service.Model.State.Actual.Description())
                    isLatestPlan = await UtilityUI.IsLatestPlanAsync(BusinessCaseId, Client);

                var tasks = Periods.Select(async (requestedPeriodModel, index) =>
                {
                    ConfigurePeriodModel(requestedPeriodModel);
                    var isDemandExist = await CheckDemandExistAsync(requestedPeriodModel);
                    var isPriceExist = await UtilityUI.IsPriceDataLoadedForPeriodAsync(requestedPeriodModel, Client);

                    await LoadDataIfNeededAsync(requestedPeriodModel, isDemandExist, isPriceExist, isLatestPlan, functionResponses);

                    if (index == 0)
                    {
                        if (requestedPeriodModel.PeriodName != PlanNSchedConstant.LastPeriodOCostName)
                            await GetProductDemandPricesAsync(requestedPeriodModel.PeriodID);
                    }
                }).ToList();
                await Task.WhenAll(tasks);
                await NotificationService.SendNotificationsAsync(
                   GetPlanStatus(functionResponses.ToList()),
                   PlanNSchedConstant.DemandPlan,
                   PlanNSchedConstant.BUSINESSMAIL,
                   Periods[0].SetupServiceData(PlanNSchedConstant.Demand, Constant.PlanNSchedUIEvtS));

                DataLoadingStatusMessage = ConfigurationUI.MultiPeriodDataLoadingCompletedMessage;
                Logger.LogMethodEnd();
            }
            catch (Exception ex)
            {
                Logger.LogMethodError(ex, "Error occurred in " + GetDemandAndPriceDataFromPublisher + " method.");
            }
            finally
            {
                UnlockLoading();
                await InvokeAsync(StateHasChanged);
            }
        }

        private void ConfigurePeriodModel(RequestedPeriodModel requestedPeriodModel)
        {
            requestedPeriodModel.CorrelationId = SessionService.GetCorrelationId();
            requestedPeriodModel.QuantityEntityType = ConfigurationUI.DemandTypeName;
            requestedPeriodModel.PriceEntityType = ConfigurationUI.PriceTypeName;
            requestedPeriodModel.ApplicationState = ApplicationState = RegionModel.ApplicationState == Service.Model.State.Actual.Description()
                ? Service.Model.State.Actual.Description()
                : Service.Model.State.Forecast.Description();
        }

        private async Task<bool> CheckDemandExistAsync(RequestedPeriodModel requestedPeriodModel)
        {
            var isDemandExist = false;
            if (ApplicationState == Service.Model.State.Actual.Description())
            {
                if (FeatureManager.IsEnabledAsync(FeatureFlags.EnableApsDataForBackcasting).Result)
                {
                    var refineries = requestedPeriodModel.RegionRefineryName
                        .Split(",")
                        .Select(r => r.Trim() == PlanNSchedConstant.Terminal ? PlanNSchedConstant.KittyHawk : r.Trim())
                        .ToList();
                    var tasks = refineries.Select(async refinery =>
                    {
                        var periodModelCopy = new RequestedPeriodModel
                        {
                            PeriodID = requestedPeriodModel.PeriodID,
                            PeriodName = requestedPeriodModel.PeriodName,
                            RegionName = requestedPeriodModel.RegionName,
                            RegionMarket = requestedPeriodModel.RegionMarket,
                            RegionTerminal = requestedPeriodModel.RegionTerminal,
                            RegionType = requestedPeriodModel.RegionType,
                            PeriodDescription = requestedPeriodModel.PeriodDescription,
                            PeriodDisplayName = requestedPeriodModel.PeriodDisplayName,
                            PriceEffectiveDate = requestedPeriodModel.PriceEffectiveDate,
                            ApplicationState = requestedPeriodModel.ApplicationState,
                            QuantityEntityType = requestedPeriodModel.QuantityEntityType,
                            PriceEntityType = requestedPeriodModel.PriceEntityType,
                            DomainNamespace = requestedPeriodModel.DomainNamespace,
                            BusinessCase = requestedPeriodModel.BusinessCase,
                            IsHierarchy = requestedPeriodModel.IsHierarchy,
                            SenderContext = requestedPeriodModel.SenderContext,
                            DateTimeRange = requestedPeriodModel.DateTimeRange,
                            HierarchyDpoLocation = requestedPeriodModel.HierarchyDpoLocation,
                            HierarchyPimsLocation = requestedPeriodModel.HierarchyPimsLocation,
                            IsManualData = requestedPeriodModel.IsManualData,
                            RegionRefineryName = refinery
                        };
                        var isExists = await UtilityUI.IsDemandDataLoadedForPeriodAsync(periodModelCopy, Client);
                        return new { refineryName = refinery, isExists };
                    }).ToList();

                    var results = await Task.WhenAll(tasks);
                    isDemandExist = results.Any(x => x.isExists);

                    if (!isDemandExist)
                        requestedPeriodModel.RegionRefineryName = string.Join(", ", refineries);
                }
                else
                {
                    requestedPeriodModel.RegionRefineryName = PlanNSchedConstant.KittyHawk;
                    isDemandExist = await UtilityUI.IsDemandDataLoadedForPeriodAsync(requestedPeriodModel, Client);
                }
            }
            else
            {
                isDemandExist = await UtilityUI.IsDemandDataLoadedForPeriodAsync(requestedPeriodModel, Client);
            }
            return isDemandExist;
        }

        private async Task LoadDataIfNeededAsync(RequestedPeriodModel requestedPeriodModel, bool isDemandExist, bool isPriceExist, bool isLatestPlan, ConcurrentBag<AZFunctionResponse> functionResponses)
        {
            if (!(isDemandExist && isPriceExist) && !IsHistoricalData && isLatestPlan)
            {
                if (!isPriceExist ||
                    (isPriceExist && !isDemandExist &&
                    requestedPeriodModel.PeriodName != PlanNSchedConstant.LastPeriodOCostName))
                {
                    (await UtilityUI.LoadDemandAndPriceDataFromPublisherByPeriodAsync(requestedPeriodModel, isDemandExist, isPriceExist, Client, Logger)).ForEach(functionResponses.Add);
                }
            }
        }

        public async Task GetProductDemandPricesAsync(int periodId)
        {
            ResetGroupByButtonSelection();
            Logger.LogMethodStart();
            LockLoading();
            await InvokeAsync(() => { StateHasChanged(); });

            try
            {
                SelectedPeriodId = periodId;
                ProductDemandAndPriceData = (await UtilityUI.GetProductDemandAndPriceDataAsync(IsAggregatedTier, SelectedPeriodId, ProductDemandAndPriceData, Periods, Client))?
                       .Where(item => CommonHelper.VolumeDecimalPrecision(item.SystemMaxDemand ?? Constant.DefaultZeroId) != Constant.DefaultZeroId)
                       .ToList();

                await InvokeAsync(() => { StateHasChanged(); });

                if (ProductDemandAndPriceData == null)
                {
                    ProductDemandAndPriceData = [];
                    IsReady = true;
                    GridDemandReference?.Rebind();
                    return;
                }

                if (ApplicationState == Service.Model.State.Forecast.Description())
                {
                    ProductDemandAndPriceSourceData = ProductDemandAndPriceData.Where(p => p.DataSource == PlanNSchedConstant.KittyHawk).ToList();
                    ProductDemandAndPriceSourceDbState = ProductDemandAndPriceSourceData.Select(item => new ProductDemandAndPrice(item)).ToList();
                    ProductDemandAndPriceRegionalData = ProductDemandAndPriceData.Where(p => p.DataSource == PlanNSchedConstant.PlannerManualExcel && p.Scope == UIConstants.Regional).ToList();
                    ProductDemandAndPriceRefineryData = ProductDemandAndPriceData.Where(p => p.DataSource == PlanNSchedConstant.PlannerManualExcel && p.Scope == UIConstants.Refinery).ToList();
                    ProductDemandAndPriceNetworkData = ProductDemandAndPriceData.Where(p => p.DataSource == PlanNSchedConstant.PlannerManualExcel).ToList();
                    ProductSellAndPriceRefineryData = ProductDemandAndPriceData.Where(p => p.DataSource == PlanNSchedConstant.PlannerManualExcel).ToList();
                }
                else
                {
                    ProductDemandAndPriceDbState = ProductDemandAndPriceData.Select(item => new ProductDemandAndPrice(item)).ToList();
                    GridDemandReference?.Rebind();
                }
                Logger.LogMethodEnd();
                IsReady = true;
                UnlockLoading();
                StateHasChanged();
            }
            catch (JsonSerializationException exSz)
            {
                UnlockLoading();
                StateHasChanged();
                Logger.LogMethodError(exSz, Constant.SerializationException + " occurred in " + GetProductDemandPrices + " method.");
            }
            catch (Exception ex)
            {
                UnlockLoading();
                StateHasChanged();
                Logger.LogMethodError(ex, "Error occurred in " + GetProductDemandPrices + " method.");
            }
        }

        public async Task<string> SaveProductDemandPriceAsync(List<ProductDemandAndPrice> demandAndPriceData)
        {
            try
            {
                Logger.LogMethodStart();
                LockLoading();
                StateHasChanged();

                var dbStateItems = ApplicationState == Service.Model.State.Actual.Description()
                    ? ProductDemandAndPriceDbState : ProductDemandAndPriceSourceDbState;


                var modifiedItems = demandAndPriceData.Where(item =>
                dbStateItems.Any(originalItem =>
                    originalItem.Id == item.Id &&
                    (originalItem.MinDemandOverrideCalculated != item.MinDemandOverrideCalculated ||
                    originalItem.MaxDemandOverrideCalculated != item.MaxDemandOverrideCalculated ||
                    originalItem.PriceOverrideCalculated != item.PriceOverrideCalculated ||
                    originalItem.Comments != item.Comments)
                ))
                .Where(x => !x.IsInvalid && !x.IsPriceInvalid)
                .ToList();

                if (!modifiedItems.Any())
                {
                    UnlockLoading();
                    StatusPopup = true;
                    StatusMessageContent = "No overwrite record was found.";
                    StateHasChanged();
                    return Message;
                }

                if (ApplicationState == Service.Model.State.Actual.Description())
                {
                    var validationMessage = ValidateOverrideCommentsBeforeSave(modifiedItems);
                    if (validationMessage != null)
                    {
                        UnlockLoading();
                        StatusPopup = true;
                        StatusMessageContent = validationMessage;
                        StateHasChanged();
                        return Message;
                    }
                }

                // Update the CreatedBy, UpdatedBy, and CorrelationId fields for the modified items
                var username = await ActiveUser.GetNameAsync();
                modifiedItems = modifiedItems.Select(item =>
                {
                    item.CreatedBy = username;
                    item.UpdatedBy = username;
                    item.CorrelationId = SessionService.GetCorrelationId();
                    return item;
                }).ToList();

                var applicationState = RegionModel.ApplicationState == Service.Model.State.Actual.Description() ? Service.Model.State.Actual.Description() : Service.Model.State.Forecast.Description();
                var saveOverrideUrl = string.Format(ConfigurationUI.SaveDemandAndPrice, applicationState);

                var response = await Client.PostAsJsonAsync(saveOverrideUrl, modifiedItems);

                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    UnlockLoading();
                    StatusPopup = true;
                    StatusMessageContent = "Data saved successfully.";
                    ProductDemandAndPriceData.ForEach(demandAndPriceData =>
                    {
                        demandAndPriceData.IsDemandOverridden = false;
                        demandAndPriceData.IsPriceOverridden = false;
                    });
                    ProductDemandAndPriceDbState = ProductDemandAndPriceData.Select(item => new ProductDemandAndPrice(item)).ToList();
                    ProductDemandAndPriceSourceData.ForEach(demandAndPriceData =>
                    {
                        demandAndPriceData.IsDemandOverridden = false;
                        demandAndPriceData.IsPriceOverridden = false;
                    });
                    ProductDemandAndPriceSourceDbState = ProductDemandAndPriceSourceData.Select(item => new ProductDemandAndPrice(item)).ToList();
                }
                else
                {
                    UnlockLoading();
                    StatusPopup = true;
                    StatusMessageContent = UIConstants.FailedToSaveData;
                }
                Logger.LogMethodEnd();
                StateHasChanged();
                return Message;
            }
            catch (Exception ex)
            {
                UnlockLoading();
                StateHasChanged();
                StatusPopup = true;
                StatusMessageContent = UIConstants.FailedToSaveData;
                Message = "Failure";
                Logger.LogMethodError(ex, "Error occurred in " + SaveProductDemandPrice + " UI method.");
                return Message;
            }
        }

        public async Task CancelProductDemandPriceAsync()
        {
            try
            {
                Logger.LogMethodStart();
                LockLoading();
                StateHasChanged();

                await GetProductDemandPricesAsync(SelectedPeriodId);

                UnlockLoading();
                StateHasChanged();
                Logger.LogMethodEnd();
            }
            catch (Exception ex)
            {
                UnlockLoading();
                StateHasChanged();
                Logger.LogMethodError(ex, "Error occurred in CancelProductDemandPriceAsync method.");
            }
        }

        public async Task GetProductDemandPricesOnPeriodSelectionChangedAsync(int periodId)
        {
            try
            {
                Logger.LogMethodStart();
                LockLoading();
                StateHasChanged();

                await GetProductDemandPricesAsync(periodId);

                UnlockLoading();
                StateHasChanged();
                Logger.LogMethodEnd();
            }
            catch (Exception ex)
            {
                UnlockLoading();
                StateHasChanged();
                Logger.LogMethodError(ex, "Error occurred in " + GetProductDemandPricesOnPeriodSelectionChanged + " method.");
            }
        }

        protected async Task ToggleDemandGridGroupByCollapseAsync()
        {
            LoadGroupsOnDemand = !LoadGroupsOnDemand;
            GridDemandReference.LoadGroupsOnDemand = LoadGroupsOnDemand;
            await GridDemandReference.SetStateAsync(GridDemandReference.GetState());
        }

        public static decimal? GetValue(decimal? systemValue, decimal? calculatedValue, decimal? overrideValue, int precision) =>
            CommonHelper.IsValueDifferent(systemValue, calculatedValue, precision) ? overrideValue : null;
        public void SortDemandPricesBy(Func<ProductDemandAndPrice, object> sort)
        {
            if (ApplicationState == Service.Model.State.Forecast.Description())
            {
                ProductDemandAndPriceSourceData = ProductDemandAndPriceSourceData.OrderBy(sort).ToList();
                ProductDemandAndPriceRegionalData = ProductDemandAndPriceRegionalData.OrderBy(sort).ToList();
                ProductDemandAndPriceNetworkData = ProductDemandAndPriceNetworkData.OrderBy(sort).ToList();
                ProductSellAndPriceRefineryData = ProductSellAndPriceRefineryData.OrderBy(sort).ToList();
                ProductDemandAndPriceRefineryData = ProductDemandAndPriceRefineryData.OrderBy(sort).ToList();

                InvokeAsync(RebindGridAsync);
            }
            else
            {
                ProductDemandAndPriceData = ProductDemandAndPriceData.OrderBy(sort).ToList();
                GridDemandReference?.Rebind();
            }
        }

        public void HandleTierAggregationChangeAsync(bool isAggregatedTier)
        {
            LockLoading();
            IsAggregatedTier = isAggregatedTier;
            ReadOnlyFlag = IsHistoricalData ? true : !IsAggregatedTier;
            GetDemandAndPriceDataFromPublisherAsync();
            UnlockLoading();
            StateHasChanged();
        }

        public async Task OnReadHandlerAsync(GridReadEventArgs args, bool isReady, List<ProductDemandAndPrice> data)
        {
            if (!isReady)
            {
                args.Data = Enumerable.Empty<ProductDemandAndPrice>();
                args.Total = 0;
                return;
            }

            var result = await BuildGridResultAsync(args.Request, data, x => x.LocationName, x => x.ProductName);

            args.Data = result.Data;
            args.Total = result.Total;
            args.AggregateResults = result.AggregateResults;
        }

        private async Task RebindGridAsync()
        {
            if (DPDemandReference != null)
                await DPDemandReference!.RebindGridAsync();

            if (DPNetworkDemandReference != null)
                await DPNetworkDemandReference!.RebindGridAsync();

            if (RPDemandReference != null)
                await RPDemandReference!.RebindGridAsync();

            if (RPRegionalDemandReference != null)
                await RPRegionalDemandReference!.RebindGridAsync();

            if (RPRefineryDemandReference != null)
                await RPRefineryDemandReference!.RebindGridAsync();
        }

        #region override calculation and setting the values

        public void OverridePriceValue(ChangeEventArgs e, object context, string overrideType)
        {
            var dataRow = (ProductDemandAndPrice)context;
            OverrideValue(e, dataRow, overrideType, dataRow.SystemPrice, dataRow.SetPriceOverride, dataRow.ClearPriceOverride);
        }

        public void OverrideMinDemandValue(ChangeEventArgs e, object context, string overrideType)
        {
            var dataRow = (ProductDemandAndPrice)context;
            OverrideQuantityValue(e, dataRow, overrideType, dataRow.SystemMinDemand, dataRow.SetMinDemandOverride, dataRow.ClearMinDemandOverride);
        }

        public void OverrideMaxDemandValue(ChangeEventArgs e, object context, string overrideType)
        {
            var dataRow = (ProductDemandAndPrice)context;
            OverrideQuantityValue(e, dataRow, overrideType, dataRow.SystemMaxDemand, dataRow.SetMaxDemandOverride, dataRow.ClearMaxDemandOverride);
        }

        public void OverrideQuantityValue(ChangeEventArgs e, ProductDemandAndPrice dataRow, string overrideType, decimal? systemBoundedValue, Action<OverrideCalculationResult> setOverrideValues, Action clearOverride)
        {
            if (!decimal.TryParse(e.Value?.ToString(), out var value))
            {
                clearOverride();
                GridDemandReference?.Rebind();
                return;
            }

            value = Math.Round(value, DemandQuantityDecimalPlaces, MidpointRounding.AwayFromZero);
            var calculatedResult = OverrideValueCalculatorService.CalculateUsing(overrideType).CalculateOverride(value, systemBoundedValue);
            setOverrideValues(calculatedResult);
            GridDemandReference?.Rebind();
        }

        public void OverrideValue(ChangeEventArgs e, ProductDemandAndPrice dataRow, string overrideType, decimal? systemBoundedValue, Action<OverrideCalculationResult> setOverrideValues, Action clearOverride)
        {
            if (!decimal.TryParse(e.Value?.ToString(), out var value))
            {
                clearOverride();
                GridDemandReference?.Rebind();
                return;
            }

            value = Math.Round(value, DemandPriceDecimalPlaces, MidpointRounding.AwayFromZero);
            var calculatedResult = OverrideValueCalculatorService.CalculateUsing(overrideType).CalculateOverride(value, systemBoundedValue);
            setOverrideValues(calculatedResult);
            GridDemandReference?.Rebind();
        }
        #endregion override calculation and setting the values

        #region Validate Comments
        public static string ValidateOverrideCommentsBeforeSave(List<ProductDemandAndPrice> modifiedItems)
        {
            var requireComment = false;
            var commentTooLong = false;
            var invalidComment = false;

            foreach (var item in modifiedItems)
            {
                var overrideChanged =
                    item.SystemMinDemand != item.MinDemandOverrideCalculated ||
                    item.SystemMaxDemand != item.MaxDemandOverrideCalculated ||
                    item.SystemPrice != item.PriceOverrideCalculated;

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
        public void ValidateCommentsLength(ChangeEventArgs e, object context)
        {
            var dataRow = (ProductDemandAndPrice)context;
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

        #endregion Validate Comments

    }
}