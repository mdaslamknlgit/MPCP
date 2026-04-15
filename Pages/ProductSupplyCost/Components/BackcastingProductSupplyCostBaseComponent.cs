using System.Collections.Concurrent;
using Microsoft.AspNetCore.Components;
using MPC.PlanSched.Model;
using MPC.PlanSched.Service;
using MPC.PlanSched.Shared.Common;
using MPC.PlanSched.Shared.Common.Extensions;
using MPC.PlanSched.Shared.Common.Service;
using MPC.PlanSched.Shared.Notification.Interface;
using MPC.PlanSched.UI.Shared;
using Newtonsoft.Json;
using Telerik.Blazor.Components;
using Telerik.DataSource.Extensions;

namespace MPC.PlanSched.UI.Pages.ProductSupplyCost.Components
{
    public class BackcastingProductSupplyCostBaseComponent : PlanBaseComponent
    {
        [Inject]
        public IOverrideValueCalculatorService? OverrideValueCalculatorService { get; set; } = default!;
        [Inject]
        public INotificationService NotificationService { get; set; } = default!;
        [Inject]
        public ILogger<BackcastingProductSupplyCostBaseComponent> Logger { get; set; } = default!;
        [Parameter]
        public string SelectedRole { get; set; } = string.Empty;
        public List<ValueTypes> OverrideTypes { get; set; } = new List<ValueTypes>();
        public bool LoadGroupsOnSupply { get; set; } = true;
        public string DataLoadingStatusMessage { get; set; } = string.Empty;
        public string PlanDescription { get; set; } = string.Empty;
        public string PriceEffectiveDate { get; set; } = string.Empty;
        public string SupplyEffectiveDate { get; set; } = string.Empty;
        public TelerikGrid<ProductSupplyAndCost> GridSupplyReference { get; set; } = default!;
        public RefreshDialog _refreshModal = default!;
        public NotificationDialog _notificationModal = default!;
        public bool LoadGroupsOnCrudeAvailSupply { get; set; } = true;
        public const string GetPeriods = "GetPeriods";
        public const string GetProductSupplyCost = "GetProductSupplyCosts";
        public const string GetCrudeSupplyCost = "GetCrudeSupplyCost";
        public const int SupplyPriceDecimalPlaces = 4;
        public const int SupplyQuantityDecimalPlaces = 6;
        public const decimal SupplyOverrideMinimumValue = 0.005m;

        public List<ProductSupplyAndCost> ProductSupplyAndCostData { get; set; } = [];
        public List<ProductSupplyAndCost> ProductSupplyAndCostDataOriginalOrder { get; set; } = [];
        public List<ProductSupplyAndCost>? ProductSupplyAndCostDataDbState { get; set; } = [];
        public List<ProductSupplyAndCost> CrudeSupplyAndCostData { get; set; } = [];
        public List<ProductSupplyAndCost> CrudeSupplyAndCostDataOriginalOrder { get; set; } = [];
        public List<ProductSupplyAndCost>? CrudeSupplyAndCostDataDbState { get; set; } = [];
        public bool IsReady { get; set; } = false;

        public async Task<List<RequestedPeriodModel>> GetPeriodsAsync()
        {
            try
            {
                Logger.LogMethodStart();
                LockLoading();
                StateHasChanged();

                if (RegionModel == null)
                {
                    Logger.LogMethodError(new Exception("RegionModel is null in " + GetPeriods + " method."));
                    return [];
                }

                UserName = await ActiveUser.GetNameAsync();

                RequestedPeriods = UtilityUI.GetPeriodData(RequestedPeriods, RegionModel, SessionService, Client, UserName);
                if (RequestedPeriods.Any())
                {
                    Periods = RequestedPeriods.ToList();
                    SelectedPeriodId = Periods[0].PeriodID;
                    PriceEffectiveDate = RequestedPeriods[0].PriceEffectiveDate?.ToString(PlanNSchedConstant.DateFormatMMDDYY);
                    if (Periods[0].DomainNamespace.DestinationApplication.Name.Contains(PlanNSchedConstant.PIMS))
                        SupplyEffectiveDate = RequestedPeriods[0].SupplyEffectiveDate?.ToString(PlanNSchedConstant.DateFormatMMDDYY);
                }

                Logger.LogMethodEnd();
                return Periods;
            }
            catch (JsonSerializationException exSz)
            {
                Logger.LogMethodError(exSz, Constant.SerializationException + " occurred in " + GetPeriods + " method.");
                return [];
            }
            catch (Exception ex)
            {
                Logger.LogMethodError(ex, "Error occurred in " + GetPeriods + " method.");
                return [];
            }
            finally
            {
                UnlockLoading();
                StateHasChanged();
            }
        }

        private List<Task> ProcessOptimizationAvailsAsync(ConcurrentBag<AZFunctionResponse> functionResponses) =>
            Periods.Select(async (requestedPeriodModel, index) =>
            {
                requestedPeriodModel.DomainNamespace.SourceApplication.Name = UIConstants.CrudeAvailExcel;

                requestedPeriodModel.CorrelationId = SessionService.GetCorrelationId();
                requestedPeriodModel.QuantityEntityType = ConfigurationUI.CrudeSupplyTypeName;
                requestedPeriodModel.CorrelationId = SessionService.GetCorrelationId();
                requestedPeriodModel.ApplicationState = Service.Model.State.Actual.Description();

                requestedPeriodModel.PriceEntityType = PlanNSchedConstant.PriceType_MidCrudeCost;
                var isMidCostLoadedToDb = await UtilityUI.IsPriceDataLoadedForPeriodAsync(requestedPeriodModel, Client);
                requestedPeriodModel.PriceEntityType = PlanNSchedConstant.PriceType_Cost;
                var isSettleCostLoadedToDb = await UtilityUI.IsPriceDataLoadedForPeriodAsync(requestedPeriodModel, Client);
                var isCostLoadedToDb = isSettleCostLoadedToDb || isMidCostLoadedToDb;
                var isCrudeSupplyLoadedToDb = await UtilityUI.IsSupplyDataLoadedForPeriodAsync(requestedPeriodModel, Client);

                if (!(isCrudeSupplyLoadedToDb && isCostLoadedToDb) && !ReadOnlyFlag)
                {
                    (await UtilityUI.LoadSupplyAndCostDataFromPublisherByPeriodAsync(requestedPeriodModel, isCrudeSupplyLoadedToDb, isCostLoadedToDb, Client, Logger))
                        .ForEach(functionResponses.Add);
                }

                if (index == 0)
                    GetCrudeSupplyCosts(requestedPeriodModel.PeriodID);
            }).ToList();

        private List<Task> ProcessProductSupplyAsync(ConcurrentBag<AZFunctionResponse> functionResponses) =>
             Periods.Select(async (requestedPeriodModel, index) =>
             {
                 requestedPeriodModel.CorrelationId = SessionService.GetCorrelationId();
                 requestedPeriodModel.QuantityEntityType = ConfigurationUI.SupplyTypeName;
                 requestedPeriodModel.PriceEntityType = ConfigurationUI.CostTypeName;
                 requestedPeriodModel.CorrelationId = SessionService.GetCorrelationId();
                 requestedPeriodModel.DomainNamespace.SourceApplication.Name = PlanNSchedConstant.Application_AspenCDM;
                 requestedPeriodModel.ApplicationState = Service.Model.State.Actual.Description();

                 var isProductSupplyLoadedToDb = false;
                 if (requestedPeriodModel.ApplicationState == Service.Model.State.Actual.Description())
                 {
                     var refineries = requestedPeriodModel.RegionRefineryName.Split(",").Select(r => r.Trim()).ToList();
                     var refineryNames = refineries.Select(r => r == PlanNSchedConstant.Terminal ? PlanNSchedConstant.KittyHawk : r).ToList();

                     var tasks = refineryNames.Select(async refineryName =>
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
                             RegionRefineryName = refineryName
                         };
                         return new { refineryName, isExists = await UtilityUI.IsSupplyDataLoadedForPeriodAsync(periodModelCopy, Client) };
                     }).ToList();

                     var results = await Task.WhenAll(tasks);
                     isProductSupplyLoadedToDb = results.Any(x => x.isExists);

                     if (!isProductSupplyLoadedToDb)
                         requestedPeriodModel.RegionRefineryName = string.Join(", ", refineryNames);
                 }
                 else
                 {
                     isProductSupplyLoadedToDb = await UtilityUI.IsSupplyDataLoadedForPeriodAsync(requestedPeriodModel, Client);
                 }
                 var isCostLoadedToDb = await UtilityUI.IsPriceDataLoadedForPeriodAsync(requestedPeriodModel, Client);
                 if (!(isProductSupplyLoadedToDb && isCostLoadedToDb) && !ReadOnlyFlag)
                 {
                     (await UtilityUI.LoadSupplyAndCostDataFromPublisherByPeriodAsync(requestedPeriodModel, isProductSupplyLoadedToDb, isCostLoadedToDb, Client, Logger))
                         .ForEach(functionResponses.Add);
                 }

                 if (index == 0)
                     GetProductSupplyCosts(requestedPeriodModel.PeriodID);
             }).ToList();

        public async Task GetSupplyAndCostDataFromPublisherAsync(bool isOptimizationAvails)
        {
            Logger.LogMethodStart();
            try
            {
                LockLoading();
                DataLoadingStatusMessage = ConfigurationUI.MultiPeriodDataLoadingInProgressMessage;
                StateHasChanged();
                var functionResponses = new ConcurrentBag<AZFunctionResponse>();

                var tasks = isOptimizationAvails ? ProcessOptimizationAvailsAsync(functionResponses)
                    : ProcessProductSupplyAsync(functionResponses);
                await Task.WhenAll(tasks);
                DataLoadingStatusMessage = ConfigurationUI.MultiPeriodDataLoadingCompletedMessage;
                await NotificationService.SendNotificationsAsync(
                    GetPlanStatus(functionResponses.ToList()),
                    PlanNSchedConstant.SupplyPlan,
                    PlanNSchedConstant.BUSINESSMAIL,
                    Periods[0].SetupServiceData(PlanNSchedConstant.Supply, Constant.PlanNSchedUIEvtS));

                Logger.LogMethodEnd();
            }
            catch (Exception ex)
            {
                Logger.LogMethodError(ex, "Error occurred in GetSupplyAndCostDataFromPublisher method.");
            }
            finally
            {
                IsReady = true;
                GridSupplyReference?.Rebind();
                UnlockLoading();
                await InvokeAsync(StateHasChanged);
            }
        }

        public void GetCrudeSupplyCosts(int periodId)
        {
            Logger.LogMethodStart();
            ResetGroupByButtonSelection();
            LockLoading();
            try
            {
                SelectedPeriodId = periodId;

                CrudeSupplyAndCostData = UtilityUI.GetSupplyAndCostData(SelectedPeriodId, CrudeSupplyAndCostData, Periods, Client)?
                        .Where(item => item.SystemMinSupply != Constant.DefaultZeroId || item.SystemMaxSupply != Constant.DefaultZeroId)
                        .OrderBy(x => string.IsNullOrEmpty(x.LocationName) ? 1 : 0)
                        .ThenBy(x => x.LocationName)
                        .ToList();
                if (CrudeSupplyAndCostData == null)
                    return;

                CrudeSupplyAndCostDataOriginalOrder = new List<ProductSupplyAndCost>(CrudeSupplyAndCostData);
                CrudeSupplyAndCostDataDbState = CrudeSupplyAndCostData
                    .Select(item => new ProductSupplyAndCost(item))
                    .ToList();

                Logger.LogMethodEnd();
            }

            catch (JsonSerializationException exSz)
            {
                Logger.LogMethodError(exSz, Constant.SerializationException + " occurred in " + GetCrudeSupplyCost + " method.");
            }
            catch (Exception ex)
            {
                Logger.LogMethodError(ex, "Error occurred in " + GetCrudeSupplyCost + " UI method.");
            }
            finally
            {
                IsReady = true;
                GridSupplyReference?.Rebind();
                UnlockLoading();
            }
        }

        public void GetProductSupplyCosts(int periodId)
        {
            Logger.LogMethodStart();
            ResetGroupByButtonSelection();
            LockLoading();

            try
            {
                SelectedPeriodId = periodId;
                ProductSupplyAndCostData = UtilityUI.GetSupplyAndCostData(SelectedPeriodId, ProductSupplyAndCostData, Periods, Client)?
                   .Where(item => CommonHelper.VolumeDecimalPrecision(item.SystemMaxSupply ?? Constant.DefaultZeroId) != Constant.DefaultZeroId)
                   .ToList();


                if (ProductSupplyAndCostData == null)
                    return;

                ProductSupplyAndCostDataOriginalOrder = new List<ProductSupplyAndCost>(ProductSupplyAndCostData);
                ProductSupplyAndCostDataDbState = ProductSupplyAndCostData.Select(item => new ProductSupplyAndCost(item)).ToList();

                Logger.LogMethodEnd();
            }
            catch (JsonSerializationException exSz)
            {
                Logger.LogMethodError(exSz, Constant.SerializationException + " occurred in " + GetProductSupplyCost + " method.");
            }
            catch (Exception ex)
            {
                Logger.LogMethodError(ex, "Error occurred in " + GetProductSupplyCost + " UI method.");
            }
            finally
            {
                IsReady = true;
                GridSupplyReference?.Rebind();
                UnlockLoading();
            }
        }

        protected async Task ToggleSupplyGridGroupByCollapseAsync()
        {
            LoadGroupsOnSupply = !LoadGroupsOnSupply;
            GridSupplyReference.LoadGroupsOnDemand = LoadGroupsOnSupply;
            await GridSupplyReference.SetStateAsync(GridSupplyReference.GetState());
        }

        public void SortCrudeSupplyCostsBy(Func<ProductSupplyAndCost, object> sort)
        {
            CrudeSupplyAndCostData = CrudeSupplyAndCostData.OrderBy(sort).ToList();
            GridSupplyReference?.Rebind();
        }

        public void SortProductSupplyCostsBy(Func<ProductSupplyAndCost, object> sort)
        {
            ProductSupplyAndCostData = ProductSupplyAndCostData.OrderBy(sort).ToList();
            GridSupplyReference?.Rebind();
        }

        public async Task SaveProductSupplyCostAsync(List<ProductSupplyAndCost> supplyAndCostData)
        {
            Logger.LogMethodStart();
            LockLoading();

            try
            {
                var userName = await ActiveUser.GetNameAsync();

                var modifiedItems = GetValidModifiedItems(supplyAndCostData);
                if (modifiedItems.Count == 0)
                {
                    StatusMessageContent = "No overwrite record was found.";
                    return;
                }
                modifiedItems.ForEach(item =>
                {
                    item.CreatedBy = userName;
                    item.UpdatedBy = userName;
                    item.CorrelationId = SessionService.GetCorrelationId();
                    item.State = Service.Model.State.Actual.Description();
                });

                if (RegionModel.ApplicationState == Service.Model.State.Actual.Description())
                {
                    var validationMessage = ValidateOverrideCommentsBeforeSave(modifiedItems);
                    if (validationMessage != null)
                    {
                        UnlockLoading();
                        StatusPopup = true;
                        StatusMessageContent = validationMessage;
                        StateHasChanged();
                        return;
                    }
                }

                var applicationState = RegionModel.ApplicationState == Service.Model.State.Actual.Description() ? Service.Model.State.Actual.Description() : Service.Model.State.Forecast.Description();
                var saveOverrideUrl = string.Format(ConfigurationUI.SaveSupplyAndCost, applicationState);

                var response = await Client.PostAsJsonAsync(saveOverrideUrl, modifiedItems);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    StatusMessageContent = "Data saved successfully.";
                    supplyAndCostData.ForEach(supplyAndCostData =>
                    {
                        supplyAndCostData.IsSupplyOverridden = false;
                        supplyAndCostData.IsCostOverridden = false;
                    });
                    ProductSupplyAndCostDataDbState = supplyAndCostData.Select(item => new ProductSupplyAndCost(item)).ToList();
                }
                else
                    StatusMessageContent = "Fail to save the data.";

                Logger.LogMethodEnd();
                StateHasChanged();
            }
            catch (Exception ex)
            {
                StatusMessageContent = "Fail to save the data.";
                Logger.LogMethodError(ex, "Error occurred in SaveProductSupplyCost UI method.");
            }
            finally
            {
                UnlockLoading();
                StatusPopup = true;
            }
        }

        private List<ProductSupplyAndCost> GetValidModifiedItems(List<ProductSupplyAndCost> supplyAndCostData) =>
            supplyAndCostData
            .Where(item => ProductSupplyAndCostDataDbState.Any(originalItem =>
                originalItem.Id == item.Id &&
                (originalItem.MinSupplyOverrideCalculated != item.MinSupplyOverrideCalculated ||
                originalItem.MaxSupplyOverrideCalculated != item.MaxSupplyOverrideCalculated ||
                originalItem.CostOverrideCalculated != item.CostOverrideCalculated ||
                originalItem.Comments != item.Comments)
            ))
            .Where(x => !x.IsInvalid && !x.IsCostInvalid)
            .ToList();

        public async Task CancelProductSupplyCost()
        {
            try
            {
                Logger.LogMethodStart();
                LockLoading();
                StateHasChanged();
                await Task.Run(() => GetProductSupplyCosts(SelectedPeriodId));

                UnlockLoading();
                StateHasChanged();
                Logger.LogMethodEnd();
            }
            catch (Exception ex)
            {
                UnlockLoading();
                StateHasChanged();
                Logger.LogMethodError(ex, "Error occurred in CancelProductSupplyCost method.");
            }
        }

        public async Task OnReadHandlerAsync(GridReadEventArgs args, bool isReady, List<ProductSupplyAndCost> data)
        {
            if (!isReady)
            {
                args.Data = Enumerable.Empty<ProductSupplyAndCost>();
                args.Total = 0;
                return;
            }

            var result = await BuildGridResultAsync(args.Request, data, x => x.LocationName, x => x.ProductName);

            args.Data = result.Data;
            args.Total = result.Total;
            args.AggregateResults = result.AggregateResults;
        }

        #region Override Methods

        public void OverrideCostValue(ChangeEventArgs e, object context, string overrideType)
        {
            var dataRow = (ProductSupplyAndCost)context;
            OverrideValue(e, dataRow, overrideType, dataRow.SystemCost, dataRow.SetCostOverride, dataRow.ClearCostOverride);
        }

        public void OverrideMinSupplyValue(ChangeEventArgs e, object context, string overrideType)
        {
            var dataRow = (ProductSupplyAndCost)context;
            OverrideValue(e, dataRow, overrideType, dataRow.SystemMinSupply, dataRow.SetMinSupplyOverride, dataRow.ClearMinSupplyOverride);
        }

        public void OverrideMaxSupplyValue(ChangeEventArgs e, object context, string overrideType)
        {
            var dataRow = (ProductSupplyAndCost)context;
            OverrideValue(e, dataRow, overrideType, dataRow.SystemMaxSupply, dataRow.SetMaxSupplyOverride, dataRow.ClearMaxSupplyOverride);
        }

        public void OverrideValue(ChangeEventArgs e, ProductSupplyAndCost dataRow, string overrideType, decimal? systemBoundedValue, Action<OverrideCalculationResult> setOverrideValues, Action clearOverride)
        {
            if (!decimal.TryParse(e.Value?.ToString(), out var value))
            {
                clearOverride();
                GridSupplyReference?.Rebind();
                return;
            }

            value = Math.Round(value, SupplyPriceDecimalPlaces, MidpointRounding.AwayFromZero);
            var calculatedResult = OverrideValueCalculatorService.CalculateUsing(overrideType).CalculateOverride(value, systemBoundedValue);
            setOverrideValues(calculatedResult);
            GridSupplyReference?.Rebind();
        }
        #endregion Override Methods

        public static decimal? GetValue(decimal? systemValue, decimal? calculatedValue, decimal? overrideValue, int precision) =>
        CommonHelper.IsValueDifferent(systemValue, calculatedValue, precision) ? overrideValue : null;

        public async Task BackcastingProductSupplyCostsOnPeriodSelectionChangedAsync(int PeriodId)
        {
            try
            {
                Logger.LogMethodStart();
                LockLoading();
                StateHasChanged();
                Task.Run(() =>
                {
                    GetProductSupplyCosts(PeriodId);
                    InvokeAsync(() =>
                    {
                        UnlockLoading();
                        StateHasChanged();
                    });
                });
            }
            catch (Exception ex)
            {
                UnlockLoading();
                StateHasChanged();
                Logger.LogMethodError(ex, "Error occurred in BackcastingProductSupplyCostsOnPeriodSelectionChangedAsync method.");
            }
        }

        public string ValidateOverrideCommentsBeforeSave(List<ProductSupplyAndCost> modifiedItems)
        {
            var requireComment = false;
            var commentTooLong = false;
            var invalidComment = false;

            foreach (var item in modifiedItems)
            {
                var overrideChanged =
                    item.SystemMinSupply != item.MinSupplyOverrideCalculated ||
                    item.SystemMaxSupply != item.MaxSupplyOverrideCalculated ||
                    item.SystemCost != item.CostOverrideCalculated;

                var comment = item.Comments;

                if (overrideChanged && string.IsNullOrWhiteSpace(comment))
                {
                    requireComment = true;
                    break;
                }
                if (!string.IsNullOrEmpty(comment) && comment.All(char.IsWhiteSpace))
                {
                    requireComment = true;
                    break;
                }
                if (comment?.Trim().Length > ConfigurationUI.CommentsMaxLength)
                {
                    commentTooLong = true;
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
    }
}