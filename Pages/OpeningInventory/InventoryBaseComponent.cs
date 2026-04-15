using System.Collections.Concurrent;
using Microsoft.AspNetCore.Components;
using MPC.PlanSched.Model;
using MPC.PlanSched.Service;
using MPC.PlanSched.Shared.Common;
using MPC.PlanSched.Shared.Common.Extensions;
using MPC.PlanSched.Shared.Common.Service;
using MPC.PlanSched.Shared.Notification.Interface;
using MPC.PlanSched.UI.ViewModel;
using Newtonsoft.Json;
using Telerik.Blazor.Components;
using Telerik.DataSource;
using Telerik.DataSource.Extensions;

namespace MPC.PlanSched.UI.Pages.OpeningInventory
{
    public class InventoryBaseComponent : PlanBaseComponent
    {
        [Parameter]
        public string SelectedRole { get; set; } = string.Empty;
        [Inject]
        public IOverrideValueCalculatorService? OverrideValueCalculatorService { get; set; } = default!;
        [Inject]
        public INotificationService NotificationService { get; set; } = default!;
        [Inject]
        public ILogger<InventoryBaseComponent> Logger { get; set; } = default!;
        public RequestedPeriodModel FirstRequestedPeriodModel { get; set; } = new RequestedPeriodModel();
        public List<Model.OpeningInventory> OpeningInventories { get; set; } = [];
        public List<Model.OpeningInventory> OpeningInventoriesAllData { get; set; } = [];
        public List<Model.OpeningInventory> OpeningInventoriesDbState { get; set; } = [];
        public TelerikGrid<Model.OpeningInventory> GridInventoryReference { get; set; } = new TelerikGrid<Model.OpeningInventory>();
        public List<Refinery> RefineriesDD { get; set; } = [];
        public List<CommodityType> CommodityTypes { get; set; } = default!;
        public List<Refinery> RefineriesFromDb { get; set; } = default!;
        public string PeriodStartDate { get; set; } = string.Empty;
        public string PlanDescription { get; set; } = string.Empty;
        public string PlanType { get; set; } = string.Empty;
        public List<ValueTypes> OverrideTypes { get; set; } = [];
        public ServiceData ServiceData { get; set; } = new ServiceData();
        public int SelectedRefineryValue { get; set; }
        public int SelectedCommodityValue { get; set; }
        public bool LoadGroupsOnDemand { get; set; } = true;
        public bool IsReady { get; set; } = false;

        public const string GetFirstPeriodMessage = "GetFirstPeriodAsync method.";
        public const string GetInventoryAsyncMessage = "GetInventoryAsync method.";
        public const string GetInventoryDataFromPublisherAsyncMessage = "GetInventoryDataFromPublisherAsync";
        public const string SaveOpeningInventoryException = "Error occurred while saving Inventory data from UI to database.";
        public const string InventoryUI = " occurred in Inventory's ";
        public const int InventoryPriceDecimalPlaces = 4;
        public const int InventoryQuantityDecimalPlaces = 6;
        public const double InventoryOverrideMinimumValue = 0.005;

        public async Task GetInventoryDataFromPublisherAsync()
        {
            Logger.LogMethodStart();
            LockLoading();
            try
            {
                DataLoadingStatusMessage = ConfigurationUI.DataLoadingInProgressMessage;
                StateHasChanged();
                var functionResponses = new ConcurrentBag<AZFunctionResponse>();

                if (FirstRequestedPeriodModel.DomainNamespace.DestinationApplication.Name.Contains(PlanNSchedConstant.DPO))
                    RefineriesFromDb = RefineriesFromDb.Where(x => x.Name.ToUpper() == UIConstants.Terminal).ToList();
                var isPIMS = FirstRequestedPeriodModel.DomainNamespace.DestinationApplication.Name.Contains(PlanNSchedConstant.PIMS);
                var isForecast = FirstRequestedPeriodModel.ApplicationState != Service.Model.State.Actual.Description();

                if (isPIMS && isForecast && !ConfigurationUI.IsRefineryInventoryEnabled)
                {
                    RefineriesFromDb = RefineriesFromDb.Where(x => string.Equals(x.Name, UIConstants.Terminal, StringComparison.OrdinalIgnoreCase)).ToList();
                }
                FirstRequestedPeriodModel.ApplicationState = Service.Model.State.Forecast.Description();
                var isLatestPlan = await UtilityUI.IsLatestPlanAsync(BusinessCaseId, Client);

                var tasks = RefineriesFromDb.Select(async (refinery) =>
                {
                    var isInventoryLoadedToDb = await UtilityUI.IsInventoryDataLoadedForPeriodByInvtSourceAsync(FirstRequestedPeriodModel, Client, refinery.Name);
                    if (!isInventoryLoadedToDb && !ReadOnlyFlag && isLatestPlan)
                    {
                        var response = await UtilityUI.RequestInventoriesDataAsync(refinery, Client, FirstRequestedPeriodModel, Logger);
                        functionResponses.Add(response);
                    }
                }).ToList();
                await Task.WhenAll(tasks);
                await GetInventoryAsync();
                await NotificationService.SendNotificationsAsync(
                    GetPlanStatus(functionResponses.ToList()),
                    PlanNSchedConstant.InventoryPlan,
                    PlanNSchedConstant.BUSINESSMAIL,
                    FirstRequestedPeriodModel.SetupServiceData(PlanNSchedConstant.Inventory, Constant.PlanNSchedUIEvtS));

                DataLoadingStatusMessage = ConfigurationUI.DataLoadingCompletedMessage;
            }
            catch (Exception ex)
            {
                Logger.LogMethodError(ex, "Exception occurred in " + GetInventoryDataFromPublisherAsyncMessage + " method.");
            }
            finally
            {
                UnlockLoading();
                await InvokeAsync(StateHasChanged);
            }
        }

        public async Task<RequestedPeriodModel> GetFirstPeriodAsync()
        {
            Logger.LogMethodStart();
            LockLoading();
            StateHasChanged();
            try
            {
                if (RegionModel == null)
                    return new();

                UserName = await ActiveUser.GetNameAsync();
                var periodModels = await UtilityUI.GetInventoryPeriodsAsync([], RegionModel, SessionService, Client, UserName);
                var finalRequestedPeriod = UtilityUI.GetInventoryFirstRequestedPeriod(periodModels, RegionModel, new(), UserName);
                if (periodModels.Count > 0)
                    PeriodStartDate = periodModels[0].DateTimeRange.FromDateTime.Value.ToShortDateString();
                Logger.LogMethodEnd();
                return finalRequestedPeriod;
            }
            catch (JsonSerializationException exSz)
            {
                Logger.LogMethodError(exSz, Constant.SerializationException + InventoryUI + GetFirstPeriodMessage);
                return new();
            }
            catch (Exception ex)
            {
                Logger.LogMethodError(ex, "Exception" + InventoryUI + GetFirstPeriodMessage);
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

        public async Task<List<Model.OpeningInventory>> GetInventoryAsync()
        {
            Logger.LogMethodStart();
            LockLoading();
            await InvokeAsync(StateHasChanged);
            CommodityTypes = [];
            RefineriesDD = [];
            try
            {
                var inventories = await UtilityUI.GetInventoryDataAsync(FirstRequestedPeriodModel, [], Client) ?? [];

                OpeningInventories = inventories.ToList();
                OpeningInventoriesAllData = inventories.ToList();

                if (OpeningInventories.Count > 0)
                {
                    CommodityTypes = UtilityUI.GetInventoryCommodityData(inventories, [])?.ToList() ?? [];
                    RefineriesDD = UtilityUI.GetInventoryRefineryData(inventories, [])?.ToList() ?? [];
                }

                OpeningInventoriesDbState = OpeningInventoriesAllData
                    .Select(item => new Model.OpeningInventory(item))
                    .ToList();
                IsReady = true;
                GridInventoryReference?.Rebind();
                Logger.LogMethodEnd();
                return OpeningInventories;
            }
            catch (JsonSerializationException exSz)
            {
                Logger.LogMethodError(exSz, Constant.SerializationException + InventoryUI + GetInventoryAsyncMessage);
                return [];
            }
            catch (Exception ex)
            {
                Logger.LogMethodError(ex, "Exception " + InventoryUI + GetInventoryAsyncMessage);
                return [];
            }
            finally
            {
                UnlockLoading();
                await InvokeAsync(StateHasChanged);
            }
        }

        public async Task CancelInventoryAsync()
        {
            try
            {
                Logger.LogMethodStart();
                LockLoading();
                StateHasChanged();

                await Task.Run(() => GetInventoryAsync());

                UnlockLoading();
                StateHasChanged();
                Logger.LogMethodEnd();
            }
            catch (Exception ex)
            {
                UnlockLoading();
                StateHasChanged();
                Logger.LogMethodError(ex, "Error occurred in CancelInventoryAsync method.");
            }
        }
        public async Task SaveOpeningInventoryAsync(List<Model.OpeningInventory> openingInventories)
        {
            try
            {
                Logger.LogMethodStart();
                LockLoading();
                StateHasChanged();
                var modifiedItems = openingInventories.Where(item =>
                    OpeningInventoriesDbState.Any(originalItem =>
                        originalItem.Id == item.Id &&
                        (originalItem.OverrideOpeningInventoryMinCalculated != item.OverrideOpeningInventoryMinCalculated ||
                        originalItem.OverrideOpeningInventoryMaxCalculated != item.OverrideOpeningInventoryMaxCalculated ||
                        originalItem.OverrideOpeningInventoryCalculated != item.OverrideOpeningInventoryCalculated || originalItem.TCost != item.TCost
                        || originalItem.Safety != item.Safety || originalItem.Target != item.Target)
                    ))
                    .Where(x => !x.IsInvalid)
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
                var saveInventoryUrl = string.Format(ConfigurationUI.SaveInventory, applicationState);
                var response = await Client.PostAsJsonAsync(saveInventoryUrl, modifiedItems);

                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    Logger.LogMethodInfo("Successfully save Inventory data UI in SaveOpeningInventory");
                    StatusMessageContent = UIConstants.SuccessfullySavedData;

                    OpeningInventoriesDbState = OpeningInventoriesAllData.Select(item => new Model.OpeningInventory(item)).ToList();
                }
                else
                {
                    Logger.LogMethodError(new Exception(SaveOpeningInventoryException));
                    StatusMessageContent = UIConstants.FailedToSaveData;
                }
                Logger.LogMethodEnd();
            }
            catch (Exception ex)
            {
                StatusMessageContent = UIConstants.FailedToSaveData;
                Logger.LogMethodError(ex, SaveOpeningInventoryException);
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

        protected void RefineryAndCommodityOnChanged()
        {
            OpeningInventories = OpeningInventoriesAllData;
            if (SelectedRefineryValue <= 0 && SelectedCommodityValue <= 0)
            {
                GridInventoryReference?.Rebind();
                return;
            }

            var refinery = RefineriesDD.Find(r => r.Id == SelectedRefineryValue);
            var commodityType = CommodityTypes.Find(r => r.Id == SelectedCommodityValue);
            if (SelectedRefineryValue > 0 && SelectedCommodityValue == 0)
                OpeningInventories = OpeningInventories.Where(p => p.Location == refinery.Name).ToList();
            if (SelectedRefineryValue > 0 && SelectedCommodityValue > 0)
                OpeningInventories = OpeningInventories.Where(p => p.Location == refinery.Name && p.Type.Contains(commodityType.Name)).ToList();
            if (SelectedRefineryValue == 0 && SelectedCommodityValue > 0)
                OpeningInventories = OpeningInventories.Where(p => p.Type.Contains(commodityType.Name)).ToList();
            GridInventoryReference?.Rebind();
        }

        protected async Task ToggleInventoryGridGroupByCollapseAsync()
        {
            LoadGroupsOnDemand = !LoadGroupsOnDemand;
            GridInventoryReference.LoadGroupsOnDemand = LoadGroupsOnDemand;
            await GridInventoryReference.SetStateAsync(GridInventoryReference.GetState());
        }

        public async Task OnReadHandlerAsync(GridReadEventArgs args)
        {
            if (!IsReady)
            {
                args.Data = Enumerable.Empty<Model.OpeningInventory>();
                args.Total = 0;
                return;
            }

            var result = await BuildInventoryResultAsync(args.Request, OpeningInventories, x => x.Location, x => x.CommodityName);

            args.Data = result.Data;
            args.Total = result.Total;
            args.AggregateResults = result.AggregateResults;
        }

        public async Task<DataSourceResult> BuildInventoryResultAsync<T>(DataSourceRequest request, IList<T> source,
           Func<T, string> locationSelector, Func<T, string> productSelector)
        {
            if (LocationOptions.Count == 0 || ProductOptions.Count == 0)
            {
                LocationOptions = GetInventoryLocationsFromService(source, locationSelector);
                ProductOptions = GetInventoryProductsFromService(source, productSelector);
            }

            IEnumerable<T> data = source;

            if (request.Filters.Any())
            {
                var selectors = new Dictionary<string, Func<T, string>>
                {
                    { "Location", locationSelector },
                    { "CommodityName", productSelector }
                };
                ApplyFilters(request.Filters, ref data, selectors);
            }

            return await data.ToDataSourceResultAsync(request);
        }

        public List<LocationFilterOption> GetInventoryLocationsFromService<T>(IList<T> source, Func<T, string> locationSelector)
        {
            if (source == null || source.Count == 0)
                return [];

            return source
                .Select(locationSelector)
                .Distinct()
                .OrderBy(x => x)
                .Select(t => new LocationFilterOption { Location = t })
                .ToList();
        }

        public List<ProductFilterOption> GetInventoryProductsFromService<T>(IList<T> source, Func<T, string> productSelector)
        {
            if (source == null || source.Count == 0)
                return [];

            return source
                .Select(productSelector)
                .Distinct()
                .OrderBy(x => x)
                .Select(t => new ProductFilterOption { CommodityName = t })
                .ToList();
        }

        #region Override
        public void OnInventoryTypeChanged(ChangeEventArgs e, object context)
        {
            var dataRow = (Model.OpeningInventory)context;
            OverrideInventoryValues(e, dataRow, dataRow.SetInventoriesOverrideOnTypeChange);
        }

        public void OverrideInventoryValues(ChangeEventArgs e, Model.OpeningInventory dataRow, Action<OverrideCalculationResult,
            OverrideCalculationResult, OverrideCalculationResult> setOverrideValues)
        {
            var calculatedResult = OverrideValueCalculatorService.CalculateUsing(e.Value.ToString()).CalculateInventoryOverride(dataRow.OverrideOpeningInventoryLatest, dataRow.SystemOpeningInventory);
            var calculatedMinResult = OverrideValueCalculatorService.CalculateUsing(e.Value.ToString()).CalculateInventoryOverride(dataRow.OverrideOpeningInventoryMinLatest, dataRow.SystemOpeningInventoryMin);
            var calculatedMaxResult = OverrideValueCalculatorService.CalculateUsing(e.Value.ToString()).CalculateInventoryOverride(dataRow.OverrideOpeningInventoryMaxLatest, dataRow.SystemOpeningInventoryMax);
            setOverrideValues(calculatedResult, calculatedMinResult, calculatedMaxResult);
            GridInventoryReference?.Rebind();
        }

        public void OnInventoryOverride(ChangeEventArgs e, object context)
        {
            var dataRow = (Model.OpeningInventory)context;
            OverrideValue(e, dataRow, dataRow.OverrideInventoryValueTypeNameLatest, dataRow.SystemOpeningInventory, dataRow.SetInventoryOverride, dataRow.ClearInventoryOverride);
        }

        public void OnMinInventoryOverride(ChangeEventArgs e, object context)
        {
            var dataRow = (Model.OpeningInventory)context;
            OverrideValue(e, dataRow, dataRow.OverrideInventoryValueTypeNameLatest, dataRow.SystemOpeningInventoryMin, dataRow.SetMinInventoryOverride, dataRow.ClearMinInventoryOverride);
        }

        public void OnMaxInventoryOverride(ChangeEventArgs e, object context)
        {
            var dataRow = (Model.OpeningInventory)context;
            OverrideValue(e, dataRow, dataRow.OverrideInventoryValueTypeNameLatest, dataRow.SystemOpeningInventoryMax, dataRow.SetMaxInventoryOverride, dataRow.ClearMaxInventoryOverride);
        }

        public void OverrideValue(ChangeEventArgs e, Model.OpeningInventory dataRow, string? overrideType, decimal? systemBoundedValue,
            Action<OverrideCalculationResult> setOverrideValues, Action setOverrideFlag)
        {
            if (!decimal.TryParse(e.Value?.ToString(), out var value))
            {
                setOverrideFlag();
                GridInventoryReference?.Rebind();
                return;
            }

            value = Math.Round(value, InventoryQuantityDecimalPlaces, MidpointRounding.AwayFromZero);
            var calculatedResult = OverrideValueCalculatorService.CalculateUsing(overrideType).CalculateInventoryOverride(value, systemBoundedValue);
            setOverrideValues(calculatedResult);
            GridInventoryReference?.Rebind();
        }

        public void OnOverrideValue(ChangeEventArgs e, object context, string columnName)
        {
            var dataRow = (Model.OpeningInventory)context;
            var overrideValue = decimal.TryParse(e.Value?.ToString(), out var value) ? value : (decimal?)null;

            switch (columnName)
            {
                case UIConstants.Target:
                    dataRow.Target = overrideValue;
                    break;
                case UIConstants.TCost:
                    dataRow.TCost = overrideValue;
                    break;
                case UIConstants.Safety:
                    dataRow.Safety = overrideValue;
                    break;
            }
            dataRow.SetOverrideFlag();
            GridInventoryReference?.Rebind();
        }

        #endregion Override
    }
}
