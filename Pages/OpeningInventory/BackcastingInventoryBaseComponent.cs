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

namespace MPC.PlanSched.UI.Pages.OpeningInventory
{
    public class BackcastingInventoryInventoryBaseComponent : PlanBaseComponent
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
        public List<string> DataSources { get; set; } = [];
        public List<CommodityType> CommodityTypes { get; set; } = default!;
        public List<Refinery> RefineriesFromDb { get; set; } = default!;
        public string PeriodStartDate { get; set; } = string.Empty;
        public string PeriodEndDate { get; set; } = string.Empty;
        public string PlanDescription { get; set; } = string.Empty;
        public List<ValueTypes> OverrideTypes { get; set; } = [];
        public List<LocationFilterOption> LocationOptions { get; set; } = [];
        public List<ProductFilterOption> ProductOptions { get; set; } = [];
        public ServiceData ServiceData { get; set; } = new ServiceData();
        public int SelectedCommodityValue { get; set; }
        public string SelectedDataSource { get; set; } = string.Empty;
        public bool LoadGroupsOnDemand { get; set; } = true;

        public const string GetFirstPeriodMessage = "GetFirstPeriodAsync method.";
        public const string GetInventoryAsyncMessage = "GetInventoryAsync method.";
        public const string GetInventoryDataFromPublisherAsyncMessage = "GetInventoryDataFromPublisherAsync";
        public const string SaveOpeningInventoryException = "Error occurred while saving Inventory data from UI to database.";
        public const string InventoryUI = " occurred in Inventory's ";
        public const int InventoryPriceDecimalPlaces = 4;
        public const int InventoryQuantityDecimalPlaces = 6;
        public const double InventoryOverrideMinimumValue = 0.005;

        public async Task GetBackcastingInventoryDataFromPublisherAsync()
        {
            Logger.LogMethodStart();
            LockLoading();
            try
            {
                DataLoadingStatusMessage = ConfigurationUI.DataLoadingInProgressMessage;
                StateHasChanged();
                var functionResponses = new ConcurrentBag<AZFunctionResponse>();

                FirstRequestedPeriodModel.ApplicationState = Service.Model.State.Actual.Description();

                var refineryChecks = await Task.WhenAll(RefineriesFromDb.Select(async refinery =>
                {
                    var isLoaded = await UtilityUI.IsInventoryDataLoadedForPeriodByInvtSourceAsync(FirstRequestedPeriodModel, Client, refinery.Name);
                    return (Refinery: refinery.Name, IsLoaded: isLoaded);
                }));

                var isRefineryExists = refineryChecks
                    .Where(x => !x.Refinery.Contains(UIConstants.Terminal, StringComparison.OrdinalIgnoreCase))
                    .Any(x => x.IsLoaded);

                var terminalExists = refineryChecks
                    .Where(x => x.Refinery.Contains(UIConstants.Terminal, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var isTerminalExists = terminalExists.Count == 0 || terminalExists.Any(x => x.IsLoaded);

                var inventoryRequestTasks = new List<Task<AZFunctionResponse>>();

                if (!isRefineryExists && !ReadOnlyFlag)
                {
                    var refinery = new Refinery
                    {
                        Name = string.Join(Constant.CommaSeparator.ToString(), RefineriesFromDb.Select(r => r.Name).Where(x => !x.Equals(UIConstants.Terminal, StringComparison.OrdinalIgnoreCase))),
                        RegionName = FirstRequestedPeriodModel.RegionName,
                        DomainNamespace = FirstRequestedPeriodModel.DomainNamespace
                    };

                    inventoryRequestTasks.Add(UtilityUI.RequestInventoriesDataAsync(refinery, Client, FirstRequestedPeriodModel, Logger));
                }

                if (!isTerminalExists && !ReadOnlyFlag)
                {
                    var terminalInventory = new Refinery
                    {
                        Name = RefineriesFromDb.FirstOrDefault(x => x.Name.Equals(UIConstants.Terminal, StringComparison.OrdinalIgnoreCase))?.Name,
                        RegionName = FirstRequestedPeriodModel.RegionName,
                        DomainNamespace = FirstRequestedPeriodModel.DomainNamespace
                    };

                    inventoryRequestTasks.Add(UtilityUI.RequestInventoriesDataAsync(terminalInventory, Client, FirstRequestedPeriodModel, Logger));
                }

                var responses = await Task.WhenAll(inventoryRequestTasks);
                responses.ToList().ForEach(functionResponses.Add);

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
        public async Task<List<Model.OpeningInventory>> GetInventoryAsync()
        {
            Logger.LogMethodStart();
            LockLoading();
            StateHasChanged();
            OpeningInventories = [];
            CommodityTypes = [];
            DataSources = [];
            try
            {
                var inventories = await UtilityUI.GetInventoryDataAsync(FirstRequestedPeriodModel, [], Client);
                var validInventories = inventories?
                    .Where(item => IsValidForInventoryDisplay(
                        item.SystemOpeningInventoryMin,
                        item.SystemOpeningInventoryMax,
                        item.SystemOpeningInventory))
                    .ToList();

                OpeningInventories.AddRange(validInventories);
                if (OpeningInventories.Any())
                {
                    CommodityTypes.AddRange(UtilityUI.GetInventoryCommodityData(inventories, []));
                    DataSources.AddRange(inventories.Select(x => x.DataSource).Distinct().ToList());
                    OpeningInventoriesAllData = OpeningInventories;

                    OpeningInventoriesDbState = OpeningInventoriesAllData.Select(item => new Model.OpeningInventory(item)).ToList();
                }
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
                await InvokeAsync(() =>
                {
                    StateHasChanged();
                });
            }
        }

        public static bool IsValidForInventoryDisplay(decimal? min, decimal? max, decimal? openingValue)
        {
            var isMinZero = min.HasValue && CommonHelper.VolumeDecimalPrecision(min.Value) == Constant.DefaultZeroId;
            var isMaxZero = max.HasValue && CommonHelper.VolumeDecimalPrecision(max.Value) == Constant.DefaultZeroId;
            var isOpeningZero = openingValue.HasValue && CommonHelper.VolumeDecimalPrecision(openingValue.Value) == Constant.DefaultZeroId;

            return !(isMinZero && isMaxZero && isOpeningZero);
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
                {
                    PeriodStartDate = periodModels[0].DateTimeRange.FromDateTime.Value.ToShortDateString();
                    PeriodEndDate = periodModels.LastOrDefault().DateTimeRange.ToDateTime.Value.ToShortDateString();
                }
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

        protected async Task ToggleInventoryGridGroupByCollapseAsync()
        {
            LoadGroupsOnDemand = !LoadGroupsOnDemand;
            GridInventoryReference.LoadGroupsOnDemand = LoadGroupsOnDemand;
            await GridInventoryReference.SetStateAsync(GridInventoryReference.GetState());
        }

        public async Task CancelInventoryBackCastingAsync()
        {
            try
            {
                Logger.LogMethodStart();
                LockLoading();
                StateHasChanged();

                await GetInventoryAsync();

                UnlockLoading();
                StateHasChanged();
                Logger.LogMethodEnd();
            }
            catch (Exception ex)
            {
                UnlockLoading();
                StateHasChanged();
                Logger.LogMethodError(ex, "Error occurred in CancelInventoryBackCastingAsync method.");
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
                        || originalItem.Safety != item.Safety || originalItem.Target != item.Target
                        || originalItem.Comments != item.Comments)
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

                var validationMessage = ValidateOverrideCommentsBeforeSave(modifiedItems);
                if (validationMessage != null)
                {
                    UnlockLoading();
                    StatusPopup = true;
                    StatusMessageContent = validationMessage;
                    StateHasChanged();
                    return;
                }

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
            if (string.IsNullOrEmpty(SelectedDataSource) && SelectedCommodityValue <= 0)
            {
                GridInventoryReference?.Rebind();
                return;
            }

            if (!string.IsNullOrEmpty(SelectedDataSource))
            {
                OpeningInventories = OpeningInventories
                    .Where(p => p.DataSource == SelectedDataSource)
                    .ToList();
            }

            if (SelectedCommodityValue > 0)
            {
                var commodityType = CommodityTypes.Find(c => c.Id == SelectedCommodityValue);
                if (commodityType != null)
                {
                    OpeningInventories = OpeningInventories
                        .Where(p => p.Type.Contains(commodityType.Name))
                        .ToList();
                }
            }

            GridInventoryReference?.Rebind();
        }

        public List<LocationFilterOption> GetLocationsFromService()
        {
            if (OpeningInventories == null || OpeningInventories.Count == 0) return [];

            var data = OpeningInventories.OrderBy(z => z.Location).Select(z => z.Location).
                Distinct().Select(t => new LocationFilterOption { Location = t }).ToList();
            return data;
        }

        public List<ProductFilterOption> GetProductsFromService()
        {
            if (OpeningInventories == null || OpeningInventories.Count == 0) return [];

            var data = OpeningInventories.OrderBy(z => z.CommodityName).Select(z => z.CommodityName).
                Distinct().Select(t => new ProductFilterOption { CommodityName = t }).ToList();
            return data;
        }

        public static void ApplyFilters(IEnumerable<IFilterDescriptor> filters, ref IEnumerable<Model.OpeningInventory> data)
        {
            foreach (var filter in filters)
            {
                if (filter is CompositeFilterDescriptor composite)
                    ApplyCompositeFilterDescriptor(composite, ref data);

                if (filter is FilterDescriptor simple)
                    SimpleFilterDescriptor(simple, ref data);
            }
        }

        public static void ApplyCompositeFilterDescriptor(CompositeFilterDescriptor composite, ref IEnumerable<Model.OpeningInventory> data)
        {
            var locationValues = composite.FilterDescriptors
                        .OfType<FilterDescriptor>()
                        .Where(f => f.Member == nameof(Model.OpeningInventory.Location))
                        .Select(f => f.Value?.ToString())
                        .ToList();

            var productValues = composite.FilterDescriptors
                .OfType<FilterDescriptor>()
                .Where(f => f.Member == nameof(Model.OpeningInventory.CommodityName))
                .Select(f => f.Value?.ToString())
                .ToList();

            if (locationValues.Count > 0)
            {
                data = data.Where(x => x.Location != null &&
                                       locationValues.Any(v => x.Location.Equals(v, StringComparison.Ordinal)));
            }
            if (productValues.Count > 0)
            {
                data = data.Where(x => x.CommodityName != null &&
                                       productValues.Any(v => x.CommodityName.Equals(v, StringComparison.Ordinal)));
            }

            var nestedComposites = composite.FilterDescriptors
                .OfType<CompositeFilterDescriptor>()
                .ToList();

            foreach (var nested in nestedComposites)
            {
                ApplyFilters(nested.FilterDescriptors, ref data);
            }
        }

        public static void SimpleFilterDescriptor(FilterDescriptor simple, ref IEnumerable<Model.OpeningInventory> data)
        {
            var value = simple.Value?.ToString();
            if (string.IsNullOrEmpty(value)) return;

            if (simple.Member == nameof(Model.OpeningInventory.Location))
            {
                data = data.Where(x => x.Location != null &&
                                       x.Location.Equals(value, StringComparison.Ordinal));
            }

            if (simple.Member == nameof(Model.OpeningInventory.CommodityName))
            {
                data = data.Where(x => x.CommodityName != null &&
                                       x.CommodityName.Equals(value, StringComparison.Ordinal));
            }
        }
        #region Override

        public void OnInventoryOverride(ChangeEventArgs e, object context, string overrideType)
        {
            var dataRow = (Model.OpeningInventory)context;
            OverrideValue(e, dataRow, overrideType, dataRow.SystemOpeningInventory, dataRow.SetBackcastingInventoryOverride, dataRow.ClearInventoryOverride);
        }

        public void OnMinInventoryOverride(ChangeEventArgs e, object context, string overrideType)
        {
            var dataRow = (Model.OpeningInventory)context;
            OverrideValue(e, dataRow, overrideType, dataRow.SystemOpeningInventoryMin, dataRow.SetBackcastingMinInventoryOverride, dataRow.ClearMinInventoryOverride);
        }

        public void OnMaxInventoryOverride(ChangeEventArgs e, object context, string overrideType)
        {
            var dataRow = (Model.OpeningInventory)context;
            OverrideValue(e, dataRow, overrideType, dataRow.SystemOpeningInventoryMax, dataRow.SetBackcastingMaxInventoryOverride, dataRow.ClearMaxInventoryOverride);
        }

        public static decimal? GetValue(decimal? systemValue, decimal? calculatedValue, decimal? overrideValue, int precision) =>
           CommonHelper.IsValueDifferent(systemValue, calculatedValue, precision) ? overrideValue : null;

        public void OverrideValue(ChangeEventArgs e, Model.OpeningInventory dataRow, string overrideType, decimal? systemBoundedValue,
            Action<OverrideCalculationResult> setOverrideValues, Action setOverrideFlag)
        {
            if (!decimal.TryParse(e.Value?.ToString(), out var value))
            {
                setOverrideFlag();
                GridInventoryReference?.Rebind();
                return;
            }

            value = Math.Round(value, InventoryQuantityDecimalPlaces, MidpointRounding.AwayFromZero);
            var calculatedResult = OverrideValueCalculatorService.CalculateUsing(overrideType).CalculateOverride(value, systemBoundedValue);
            setOverrideValues(calculatedResult);
            GridInventoryReference?.Rebind();
        }

        public string ValidateOverrideCommentsBeforeSave(List<Model.OpeningInventory> modifiedItems)
        {
            var requireComment = false;
            var commentTooLong = false;
            var invalidComment = false;

            foreach (var item in modifiedItems)
            {
                var overrideChanged =
                    item.SystemOpeningInventoryMin != item.OverrideOpeningInventoryMinCalculated ||
                    item.SystemOpeningInventoryMax != item.OverrideOpeningInventoryMaxCalculated ||
                    item.SystemOpeningInventory != item.OverrideOpeningInventoryCalculated;

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
        #endregion Override
    }
}