using System.Collections.Concurrent;
using Microsoft.AspNetCore.Components;
using MPC.PlanSched.Model;
using MPC.PlanSched.Service;
using MPC.PlanSched.Shared.Common;
using MPC.PlanSched.Shared.Common.Extensions;
using MPC.PlanSched.Shared.Common.Service;
using MPC.PlanSched.Shared.Notification.Interface;
using Newtonsoft.Json;
using Telerik.Blazor.Components;
using Telerik.DataSource.Extensions;
namespace MPC.PlanSched.UI.Pages.ProductSupplyCost.Components
{
    public class ProductSupplyCostBaseComponent : PlanBaseComponent
    {
        [Inject]
        public IOverrideValueCalculatorService? OverrideValueCalculatorService { get; set; } = default!;
        [Inject]
        public INotificationService NotificationService { get; set; } = default!;
        [Inject]
        public ILogger<ProductSupplyCostBaseComponent> Logger { get; set; } = default!;
        [Parameter]
        public string SelectedRole { get; set; } = string.Empty;
        public bool LoadGroupsOnSupply { get; set; } = true;
        public bool LoadGroupsOnCrudeAvailSupply { get; set; } = true;
        public string PlanDescription { get; set; } = string.Empty;
        public string PriceEffectiveDate { get; set; } = string.Empty;
        public string SupplyEffectiveDate { get; set; } = string.Empty;
        public string PlanType { get; set; } = string.Empty;
        public bool IsReady { get; set; } = false;
        public List<RequestedPeriodModel> CrudeAvailPeriods { get; set; } = [];
        public List<ValueTypes> OverrideTypes { get; set; } = new List<ValueTypes>();
        public TelerikGrid<ProductSupplyAndCost> GridSupplyReference { get; set; } = default!;
        public ProductSupplyCostDPGrid DPSupplyReference { get; set; } = default!;
        public ProductSupplyCostDPGrid DPNetworkSupplyReference { get; set; } = default!;
        public TelerikGrid<ProductSupplyAndCost> GridCrudeAvailSupplyReference { get; set; } = default!;
        public ProductSupplyCostRPGrid RPSupplyReference { get; set; } = default!;
        public ProductSupplyCostRPGrid RPRegionalSupplyReference { get; set; } = default!;
        public ProductSupplyCostRPGrid RPRefinerySupplyReference { get; set; } = default!;
        public List<ProductSupplyAndCost> ProductSupplyAndCostData { get; set; } = [];
        public List<ProductSupplyAndCost> ProductSupplyAndCostSourceData { get; set; } = [];
        public List<ProductSupplyAndCost> ProductSupplyAndCostRegionalData { get; set; } = [];
        public List<ProductSupplyAndCost> ProductSupplyAndCostRefineryData { get; set; } = [];
        public List<ProductSupplyAndCost> ProductSupplyAndCostNetworkData { get; set; } = [];
        public List<ProductSupplyAndCost> ProductBuyAndCostRefineryData { get; set; } = [];
        public List<ProductSupplyAndCost> CrudeSupplyAndCostData { get; set; } = [];
        public List<ProductSupplyAndCost>? ProductSupplyAndCostDataDbState = [];
        public List<ProductSupplyAndCost> CrudeSupplyAndCostDataOriginalOrder { get; set; } = [];
        public List<ProductSupplyAndCost>? CrudeSupplyAndCostDataDbState = [];

        public const string GetPeriods = "GetPeriods";
        public const string GetProductSupplyCost = "GetProductSupplyCosts";
        public const string GetCrudeSupplyCost = "GetCrudeSupplyCost";
        public const int SupplyPriceDecimalPlaces = 4;
        public const int SupplyQuantityDecimalPlaces = 6;
        public const decimal SupplyOverrideMinimumValue = 0.005m;

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
                    Periods = RequestedPeriods.Take(RequestedPeriods.Count - 1).ToList();
                    SelectedPeriodId = Periods[0].PeriodID;
                    PriceEffectiveDate = RequestedPeriods[0].PriceEffectiveDate?.ToString(Constant.DateFormat);
                    if (Periods[0].DomainNamespace.DestinationApplication.Name.Contains(PlanNSchedConstant.PIMS))
                        SupplyEffectiveDate = ConvertUTCDateToLocal(DateTime.Parse(RequestedPeriods[0].SupplyEffectiveDate?.ToString())).ToString(Constant.DateFormat);
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

        public async Task GetSupplyAndCostDataFromPublisherAsync()
        {
            Logger.LogMethodStart();

            try
            {
                DataLoadingStatusMessage = ConfigurationUI.MultiPeriodDataLoadingInProgressMessage;
                var functionResponses = new ConcurrentBag<AZFunctionResponse>();
                var isLatestPlan = await UtilityUI.IsLatestPlanAsync(BusinessCaseId, Client);
                var tasks = Periods.Select(async (requestedPeriodModel, index) =>
                {
                    requestedPeriodModel.CorrelationId = SessionService.GetCorrelationId();
                    requestedPeriodModel.QuantityEntityType = ConfigurationUI.SupplyTypeName;
                    requestedPeriodModel.PriceEntityType = ConfigurationUI.CostTypeName;
                    requestedPeriodModel.CorrelationId = SessionService.GetCorrelationId();
                    requestedPeriodModel.DomainNamespace.SourceApplication.Name = PlanNSchedConstant.Application_AspenCDM;
                    requestedPeriodModel.ApplicationState = Service.Model.State.Forecast.Description();
                    var isSupplyLoadedToDb = await UtilityUI.IsSupplyDataLoadedForPeriodAsync(requestedPeriodModel, Client);
                    var isCostLoadedToDb = await UtilityUI.IsPriceDataLoadedForPeriodAsync(requestedPeriodModel, Client);

                    if (!(isSupplyLoadedToDb && isCostLoadedToDb) && !ReadOnlyFlag && isLatestPlan)
                        (await UtilityUI.LoadSupplyAndCostDataFromPublisherByPeriodAsync(requestedPeriodModel, isSupplyLoadedToDb, isCostLoadedToDb, Client, Logger))
                                                   .ForEach(functionResponses.Add);

                    if (index == 0)
                        GetProductSupplyCosts(requestedPeriodModel.PeriodID);
                }).ToList();

                if (Periods[0].DomainNamespace.DestinationApplication.Name.Contains(PlanNSchedConstant.PIMS))
                {
                    await ProcessCrudeAvailAsync(tasks, functionResponses);
                }

                await Task.WhenAll(tasks);
                if (Periods[0].DomainNamespace.DestinationApplication.Name.Contains(PlanNSchedConstant.PIMS))
                {
                    GetCrudeSupplyCosts(Periods[0].PeriodID);
                }
                await NotificationService.SendNotificationsAsync(
                    GetPlanStatus(functionResponses.ToList()),
                    PlanNSchedConstant.SupplyPlan,
                    PlanNSchedConstant.BUSINESSMAIL,
                    Periods[0].SetupServiceData(PlanNSchedConstant.Supply, Constant.PlanNSchedUIEvtS));

                DataLoadingStatusMessage = ConfigurationUI.MultiPeriodDataLoadingCompletedMessage;
                Logger.LogMethodEnd();
            }
            catch (Exception ex)
            {
                Logger.LogMethodError(ex, "Error occurred in GetSupplyAndCostDataFromPublisher method.");
            }
            finally
            {
                UnlockLoading();
                await InvokeAsync(StateHasChanged);
            }
        }

        private async Task ProcessCrudeAvailAsync(List<Task> tasks, ConcurrentBag<AZFunctionResponse> functionResponses)
        {
            CrudeAvailPeriods = JsonConvert.DeserializeObject<List<RequestedPeriodModel>>(JsonConvert.SerializeObject(Periods));
            CrudeAvailPeriods.ForEach(c =>
            {
                c.DomainNamespace.SourceApplication.Name = UIConstants.CrudeAvailExcel;
                c.QuantityEntityType = ConfigurationUI.CrudeSupplyTypeName;
            });

            var crudeSupplyTasks = CrudeAvailPeriods.Select(async (requestedPeriodModel, index) =>
            {
                requestedPeriodModel.CorrelationId = SessionService.GetCorrelationId();
                requestedPeriodModel.QuantityEntityType = ConfigurationUI.CrudeSupplyTypeName;
                requestedPeriodModel.PriceEntityType = ConfigurationUI.CostTypeName;
                requestedPeriodModel.CorrelationId = SessionService.GetCorrelationId();
                requestedPeriodModel.ApplicationState = Service.Model.State.Forecast.Description();
                var isCrudeSupplyLoadedToDb = await UtilityUI.IsSupplyDataLoadedForPeriodAsync(requestedPeriodModel, Client);
                if (!isCrudeSupplyLoadedToDb && !ReadOnlyFlag)
                {
                    (await UtilityUI.LoadSupplyAndCostDataFromPublisherByPeriodAsync(requestedPeriodModel, isCrudeSupplyLoadedToDb, true, Client, Logger))
                        .ForEach(functionResponses.Add);
                }
            }).ToList();

            tasks.AddRange(crudeSupplyTasks);
        }

        public void GetProductSupplyCosts(int periodId)
        {
            try
            {
                Logger.LogMethodStart();
                ResetGroupByButtonSelection();
                LockLoading();

                SelectedPeriodId = periodId;
                ProductSupplyAndCostData = UtilityUI.GetSupplyAndCostData(SelectedPeriodId, ProductSupplyAndCostData, Periods, Client);

                if (ProductSupplyAndCostData == null)
                    return;
                InvokeAsync(StateHasChanged);

                ProductSupplyAndCostSourceData = ProductSupplyAndCostData.Where(p => p.DataSource == PlanNSchedConstant.KittyHawk).ToList();
                ProductSupplyAndCostRegionalData = ProductSupplyAndCostData.Where(p => p.DataSource == PlanNSchedConstant.PlannerManualExcel && p.Scope == UIConstants.Regional).ToList();
                ProductSupplyAndCostRefineryData = ProductSupplyAndCostData.Where(p => p.DataSource == PlanNSchedConstant.PlannerManualExcel && p.Scope == UIConstants.Refinery).ToList();
                ProductSupplyAndCostNetworkData = ProductSupplyAndCostData.Where(p => p.DataSource == PlanNSchedConstant.PlannerManualExcel).ToList();
                ProductBuyAndCostRefineryData = ProductSupplyAndCostData.Where(p => p.DataSource == PlanNSchedConstant.PlannerManualExcel).ToList();

                ProductSupplyAndCostDataDbState = ProductSupplyAndCostSourceData.Select(item => new ProductSupplyAndCost(item)).ToList();

                IsReady = true;
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
                UnlockLoading();
            }
        }

        public void GetCrudeSupplyCosts(int periodId)
        {
            try
            {
                Logger.LogMethodStart();
                ResetGroupByButtonSelection();
                LockLoading();

                SelectedPeriodId = periodId;
                CrudeSupplyAndCostData = UtilityUI.GetSupplyAndCostData(SelectedPeriodId, CrudeSupplyAndCostData, CrudeAvailPeriods, Client)?
                                                    .Where(item => item.SystemMinSupply != Constant.DefaultZeroId || item.SystemMaxSupply != Constant.DefaultZeroId)
                                                    .OrderBy(x => string.IsNullOrEmpty(x.LocationName) ? 1 : 0)
                                                    .ThenBy(x => x.LocationName)
                                                    .ToList();

                if (CrudeSupplyAndCostData == null)
                    return;

                CrudeSupplyAndCostDataOriginalOrder = new List<ProductSupplyAndCost>(CrudeSupplyAndCostData);
                CrudeSupplyAndCostDataDbState = CrudeSupplyAndCostData.Select(item => new ProductSupplyAndCost(item)).ToList();
                GridCrudeAvailSupplyReference.Rebind();
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
                UnlockLoading();
            }
        }

        public async Task GetProductSupplyCostsOnPeriodSelectionChangedAsync(int PeriodId)
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
                Logger.LogMethodError(ex, "Error occurred in GetProductSupplyOnPeriodSelectionChangedAsync method.");
            }
        }

        public async Task GetCrudeSupplyCostsOnPeriodSelectionChangedAsync(int PeriodId)
        {
            try
            {
                Logger.LogMethodStart();
                LockLoading();
                StateHasChanged();

                Task.Run(() =>
                {
                    GetCrudeSupplyCosts(PeriodId);
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
                Logger.LogMethodError(ex, "Error occurred in GetCrudeSupplyOnPeriodSelectionChangedAsync method.");
            }
        }

        public async Task SaveProductSupplyCostAsync(List<ProductSupplyAndCost> supplyAndCostData)
        {
            try
            {
                Logger.LogMethodStart();
                LockLoading();

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
                });

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
                    StatusMessageContent = UIConstants.FailedToSaveData;

                Logger.LogMethodEnd();
                StateHasChanged();
            }
            catch (Exception ex)
            {
                StatusMessageContent = UIConstants.FailedToSaveData;
                Logger.LogMethodError(ex, "Error occurred in SaveProductSupplyCost UI method.");
            }
            finally
            {
                UnlockLoading();
                StatusPopup = true;
            }
        }

        private List<ProductSupplyAndCost> GetValidModifiedItems(List<ProductSupplyAndCost> supplyAndCostData) =>
            supplyAndCostData.Where(item =>
                ProductSupplyAndCostDataDbState.Any(originalItem =>
                    originalItem.Id == item.Id &&
                    (originalItem.MinSupplyOverrideCalculated != item.MinSupplyOverrideCalculated ||
                    originalItem.MaxSupplyOverrideCalculated != item.MaxSupplyOverrideCalculated ||
                    originalItem.CostOverrideCalculated != item.CostOverrideCalculated)
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

        public void SortProductSupplyCostsBy(Func<ProductSupplyAndCost, object> sort)
        {
            ProductSupplyAndCostData = ProductSupplyAndCostData.OrderBy(sort).ToList();
            ProductSupplyAndCostSourceData = ProductSupplyAndCostSourceData.OrderBy(sort).ToList();
            ProductSupplyAndCostRegionalData = ProductSupplyAndCostData.OrderBy(sort).ToList();
            ProductSupplyAndCostRefineryData = ProductSupplyAndCostData.OrderBy(sort).ToList();
            ProductSupplyAndCostNetworkData = ProductSupplyAndCostData.OrderBy(sort).ToList();
            ProductBuyAndCostRefineryData = ProductSupplyAndCostData.OrderBy(sort).ToList();
            CrudeSupplyAndCostData = CrudeSupplyAndCostData.OrderBy(sort).ToList();
            InvokeAsync(RebindGridAsync);
            GridSupplyReference?.Rebind();
            GridCrudeAvailSupplyReference?.Rebind();
        }
        protected async Task ToggleSupplyGridGroupByCollapseAsync()
        {
            LoadGroupsOnSupply = !LoadGroupsOnSupply;
            GridSupplyReference.LoadGroupsOnDemand = LoadGroupsOnSupply;
            await GridSupplyReference.SetStateAsync(GridSupplyReference.GetState());
        }

        protected async Task ToggleCrudeAvailSupplyGridGroupByCollapseAsync()
        {
            LoadGroupsOnCrudeAvailSupply = !LoadGroupsOnCrudeAvailSupply;
            GridCrudeAvailSupplyReference.LoadGroupsOnDemand = LoadGroupsOnCrudeAvailSupply;
            await GridCrudeAvailSupplyReference.SetStateAsync(GridCrudeAvailSupplyReference.GetState());
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

        private async Task RebindGridAsync()
        {
            if (DPSupplyReference != null)
                await DPSupplyReference!.RebindGridAsync();

            if (DPNetworkSupplyReference != null)
                await DPNetworkSupplyReference!.RebindGridAsync();

            if (RPSupplyReference != null)
                await RPSupplyReference!.RebindGridAsync();

            if (RPRegionalSupplyReference != null)
                await RPRegionalSupplyReference.RebindGridAsync();

            if (RPRefinerySupplyReference != null)
                await RPRefinerySupplyReference!.RebindGridAsync();
        }

        public static decimal? GetValue(decimal? systemValue, decimal? calculatedValue, decimal? overrideValue, int precision) =>
            CommonHelper.IsValueDifferent(systemValue, calculatedValue, precision) ? overrideValue : null;

        #region region Override Methods

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
        #endregion region Override Methods        
    }
}
