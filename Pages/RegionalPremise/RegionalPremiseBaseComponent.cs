using System.Collections.Concurrent;
using Microsoft.AspNetCore.Components;
using MPC.PlanSched.Model;
using MPC.PlanSched.Service;
using MPC.PlanSched.Shared.Common;
using MPC.PlanSched.Shared.Common.Extensions;
using MPC.PlanSched.Shared.Common.Service;
using MPC.PlanSched.Shared.Notification.Interface;
using MPC.PlanSched.Shared.Service.Schema;
using MPC.PlanSched.UI.ViewModel;
using Newtonsoft.Json;
using Telerik.Blazor.Components;
using Telerik.DataSource;
using Telerik.DataSource.Extensions;

namespace MPC.PlanSched.UI.Pages.RegionalPremise
{
    public class RegionalPremiseBaseComponent : PlanBaseComponent
    {
        [Inject]
        public IOverrideValueCalculatorService? OverrideValueCalculatorService { get; set; } = default!;
        [Inject]
        public INotificationService NotificationService { get; set; } = default!;
        [Inject]
        public ILogger<RegionalPremiseBaseComponent> Logger { get; set; } = default!;
        [Parameter]
        public string SelectedConstraint { get; set; } = string.Empty;
        [Parameter]
        public List<RequestedPeriodModel> RequestedPeriods { get; set; }
        [Parameter]
        public int SelectedPeriodId { get; set; } = 0;
        public bool IsReady { get; set; } = false;
        public List<RegionalCapsBoundsModel> RegionalCapsBoundsData { get; set; } = [];
        public List<RegionalCapsBoundsModel>? RegionalCapsBoundsOriginalOrder { get; set; } = [];
        public List<RegionalCapsBoundsModel>? RegionalCapsBoundsDbState { get; set; } = [];
        public List<RegionalTransferModel> RegionalTransferData { get; set; } = [];
        public List<RegionalTransferModel>? RegionalTransferOriginalOrder { get; set; } = [];
        public List<RegionalTransferModel>? RegionalTransferDbState { get; set; } = [];
        public List<ValueTypes> OverrideTypes { get; set; } = [];
        public string PriceEffectiveDate { get; set; } = string.Empty;
        public TelerikGrid<RegionalCapsBoundsModel> GridRegionalCapsBoundsReference { get; set; } = default!;
        public bool LoadGroupsOnRegionalCapsBounds { get; set; } = true;
        public TelerikGrid<RegionalTransferModel> GridRegionalTransferReference { get; set; } = default!;
        public List<RefineryModel> Refineries { get; set; } = [];
        public bool LoadGroupsOnRegionalTransfer { get; set; } = true;
        public string PlanDescription { get; set; } = string.Empty;
        public string PlanType { get; set; } = string.Empty;
        public bool IsHistoricalData { get; set; }

        public const string GetPeriods = "GetPeriodsAsync";
        public const string GetRegionalPremiseConstraint = "GetRegionalPremiseConstraint";
        public const string GetRegionalPremiseDataFromPublisher = "GetRegionalPremiseDataFromPublisher";
        public const string SaveRegionalCapsBoundsModel = "SaveRegionalCapsBoundsModel";
        public const string SaveRegionalTransferModel = "SaveRegionalTransferModel";
        public const int ConstraintValueDecimalPlaces = 3;
        public const int ConstraintCostDecimalPlaces = 4;


        public async Task GetRegionalPremiseDataFromPublisherAsync(bool isFirstLoad)
        {
            Logger.LogMethodStart();
            try
            {
                if (isFirstLoad)
                {
                    DataLoadingStatusMessage = ConfigurationUI.MultiPeriodDataLoadingInProgressMessage;
                    var functionResponses = new ConcurrentBag<AZFunctionResponse>();
                    var isLatestPlan = await UtilityUI.IsLatestPlanAsync(BusinessCaseId, Client);
                    var tasks = Periods.Select(async (requestedPeriodModel, index) =>
                    {
                        requestedPeriodModel.CorrelationId = SessionService.GetCorrelationId();
                        requestedPeriodModel.ApplicationState = Service.Model.State.Forecast.Description();

                        var isConstraintExistResult = await UtilityUI.IsRegionalPremiseDataLoadedForPeriodAsync(requestedPeriodModel, SelectedConstraint, Client);

                        if (isConstraintExistResult.Values.Any(e => e.Equals(false)) && !IsHistoricalData && isLatestPlan)
                        {
                            (await UtilityUI.LoadRegionalPremiseConstraintDataFromPublisherByPeriodAsync(requestedPeriodModel, isConstraintExistResult, Client, Logger)).ForEach(functionResponses.Add);
                        }

                        var refineryCodes = new List<string>();
                        switch (requestedPeriodModel.DomainNamespace.DestinationApplication.Name)
                        {
                            case Constant.SouthPIMS:
                                var southRefineries = Refineries.Where(r => r.RegionName == Constant.SouthRegion).Select(r => r.Name).ToList();
                                refineryCodes.AddRange(southRefineries);
                                break;
                            case Constant.NorthPIMS:
                                var northRefineries = Refineries.Where(r => r.RegionName == Constant.NorthRegion).Select(r => r.Name).ToList();
                                refineryCodes.AddRange(northRefineries);
                                break;
                            case Constant.WestPIMS:
                                var westRefineries = Refineries.Where(r => r.RegionName == Constant.WestRegion).Select(r => r.Name).ToList();
                                refineryCodes.AddRange(westRefineries);
                                break;
                        }

                        foreach (var refinery in refineryCodes)
                        {
                            var isRefineryConstrantExistsResult = await UtilityUI.IsRefineryPremiseDataLoadedForPeriodAsync(requestedPeriodModel, SelectedConstraint, refinery, Client);

                            if (isRefineryConstrantExistsResult.Values.Where(e => e.Equals(false)).Any() && !IsHistoricalData)
                                (await UtilityUI.LoadRefineryPremiseConstraintDataFromPublisherByPeriodAsync(requestedPeriodModel, isRefineryConstrantExistsResult, refinery, Client, Logger)).ForEach(functionResponses.Add);
                        }

                        if (index == 0)
                        {
                            await GetRegionalPremiseConstraintAsync(requestedPeriodModel, SelectedConstraint);
                        }
                    }).ToList();
                    await Task.WhenAll(tasks);
                    await NotificationService.SendNotificationsAsync(
                       GetPlanStatus(functionResponses.ToList()),
                       PlanNSchedConstant.RegionalCapsBounds,
                       PlanNSchedConstant.BUSINESSMAIL,
                       Periods[0].SetupServiceData(PlanNSchedConstant.RegionalPremise, Constant.PlanNSchedUIEvtS));
                }
                else
                {
                    var requestedPeriodModel = Periods.Where(p => p.PeriodID == SelectedPeriodId).FirstOrDefault();
                    await GetRegionalPremiseConstraintAsync(requestedPeriodModel, SelectedConstraint);
                }
                DataLoadingStatusMessage = ConfigurationUI.MultiPeriodDataLoadingCompletedMessage;
                Logger.LogMethodEnd();
            }
            catch (Exception ex)
            {
                Logger.LogMethodError(ex, $"Error occurred in {GetRegionalPremiseDataFromPublisher} method.");
            }
            finally
            {
                UnlockLoading();
                await InvokeAsync(StateHasChanged);
            }
        }

        public async Task GetRegionalPremiseConstraintAsync(RequestedPeriodModel requestedPeriodModel, string selectedConstaint)
        {
            try
            {
                SelectedConstraint = selectedConstaint;
                var result = await UtilityUI.GetRegionalPremiseConstraintDataAsync(requestedPeriodModel, SelectedConstraint, Client);

                if (SelectedConstraint == RegionalPremiseConstraintType.RegionalCapsBounds.Description())
                {
                    RegionalCapsBoundsData = JsonConvert.DeserializeObject<List<Model.RegionalCapsBoundsModel>>(result)?.ToList() ?? [];
                    RegionalCapsBoundsDbState = RegionalCapsBoundsData.Select(item => new RegionalCapsBoundsModel(item)).ToList();
                }
                else
                {
                    RegionalTransferData = JsonConvert.DeserializeObject<List<RegionalTransferModel>>(result)?.ToList() ?? [];
                    RegionalTransferDbState = RegionalTransferData.Select(item => new RegionalTransferModel(item)).ToList();
                }
                IsReady = true;
                await InvokeAsync(RebindGridAsync);
            }
            catch (Exception ex)
            {
                UnlockLoading();
                StateHasChanged();
                Logger.LogMethodError(ex, $"Error occurred in {GetRegionalPremiseConstraint} method.");
            }
        }

        public static decimal? GetValue(decimal? systemValue, decimal? calculatedValue, decimal? overrideValue) =>
         (systemValue != calculatedValue) ? overrideValue : null;

        public async Task<string> SaveRegionalCapsBoundsModelAsync(List<RegionalCapsBoundsModel> regionalCapsBoundsData)
        {
            try
            {
                Logger.LogMethodStart();
                LockLoading();
                StateHasChanged();

                var modifiedItems = regionalCapsBoundsData.Where(item =>
                    RegionalCapsBoundsDbState.Any(originalItem =>
                        originalItem.Id == item.Id &&
                        (originalItem.MinOverrideCalculated != item.MinOverrideCalculated ||
                        originalItem.MaxOverrideCalculated != item.MaxOverrideCalculated ||
                        originalItem.DefaultMinOverrideCalculated != item.DefaultMinOverrideCalculated ||
                        originalItem.DefaultMaxOverrideCalculated != item.DefaultMaxOverrideCalculated)
                    ))
                    .Where(x => !x.IsInvalid)
                    .ToList();

                if (!modifiedItems.Any())
                {
                    UnlockLoading();
                    StatusPopup = true;
                    StatusMessageContent = "No overwrite record was found.";
                    StateHasChanged();
                    return Message;
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

                var response = await Client.PostAsJsonAsync(ConfigurationUI.SaveRegionalCapsBounds, modifiedItems);

                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    UnlockLoading();
                    StatusPopup = true;
                    StatusMessageContent = "Data saved successfully.";
                    RegionalCapsBoundsData.ForEach(regionalCapsBounds =>
                    {
                        regionalCapsBounds.IsRegionalCapsBoundsOverridden = false;
                    });
                    RegionalCapsBoundsDbState = RegionalCapsBoundsData.Select(item => new RegionalCapsBoundsModel(item)).ToList();
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
                Logger.LogMethodError(ex, $"Error occurred in {SaveRegionalCapsBoundsModel} UI method.");
                return Message;
            }
        }

        public async Task CancelRegionalCapsBoundsModelAsync()
        {
            try
            {
                Logger.LogMethodStart();
                LockLoading();
                StateHasChanged();

                var requestedPeriodModel = Periods.FirstOrDefault(p => p.PeriodID == SelectedPeriodId);

                if (requestedPeriodModel != null)
                    await GetRegionalPremiseConstraintAsync(requestedPeriodModel, SelectedConstraint);

                UnlockLoading();
                StateHasChanged();
                Logger.LogMethodEnd();
            }
            catch (Exception ex)
            {
                UnlockLoading();
                StateHasChanged();
                Logger.LogMethodError(ex, "Error occurred in CancelRegionalCapsBoundsModelAsync method.");
            }
        }

        public async Task<string> SaveRegionalTransfarModelAsync(List<RegionalTransferModel> regionalTransferData)
        {
            try
            {
                Logger.LogMethodStart();
                LockLoading();
                StateHasChanged();

                var modifiedItems = regionalTransferData.Where(item =>
                    RegionalTransferDbState.Any(originalItem =>
                        originalItem.Id == item.Id &&
                        (originalItem.Min != item.Min ||
                        originalItem.Max != item.Max ||
                        originalItem.DefaultMin != item.DefaultMin ||
                        originalItem.DefaultMax != item.DefaultMax ||
                        originalItem.DefaultCost != item.DefaultCost ||
                        originalItem.CostOverrideCalculated != item.CostOverrideCalculated
                        )
                    ))
                    .Where(x => !x.IsInvalid)
                    .ToList();

                if (!modifiedItems.Any())
                {
                    UnlockLoading();
                    StatusPopup = true;
                    StatusMessageContent = UIConstants.NoOverwriteRecord;
                    StateHasChanged();
                    return Message;
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

                var response = await Client.PostAsJsonAsync(ConfigurationUI.SaveRegionalTransfer, modifiedItems);

                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    UnlockLoading();
                    StatusPopup = true;
                    StatusMessageContent = UIConstants.SuccessfullySavedData;
                    RegionalTransferData.ForEach(regionalTransfer =>
                    {
                        regionalTransfer.IsRegionalTransferOverridden = false;
                    });
                    RegionalTransferDbState = RegionalTransferData.Select(item => new RegionalTransferModel(item)).ToList();
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
                Message = PlanNSchedConstant.Failure;
                Logger.LogMethodError(ex, "Error occurred in " + SaveRegionalTransferModel + " UI method.");
                return Message;
            }
        }

        public async Task CancelRegionalTransferModelAsync()
        {
            try
            {
                Logger.LogMethodStart();
                LockLoading();
                StateHasChanged();

                var requestedPeriodModel = Periods.FirstOrDefault(p => p.PeriodID == SelectedPeriodId);
                if (requestedPeriodModel != null)
                    await GetRegionalPremiseConstraintAsync(requestedPeriodModel, SelectedConstraint);

                UnlockLoading();
                StateHasChanged();
                Logger.LogMethodEnd();
            }
            catch (Exception ex)
            {
                UnlockLoading();
                StateHasChanged();
                Logger.LogMethodError(ex, "Error occurred in CancelRegionalTransferModelAsync method.");
            }
        }

        public async Task<DataSourceResult> BuildRegionalGridResultAsync<T>(DataSourceRequest request, IList<T> source,
            Func<T, string> productSelector)
        {
            if (ProductOptions.Count == 0)
            {
                ProductOptions = GetRegionalProductsFromService(source, productSelector);
            }

            IEnumerable<T> data = source;

            if (request.Filters.Any())
            {
                var selectors = new Dictionary<string, Func<T, string>>
                {
                    { "ConstraintTag", productSelector }
                };
                ApplyFilters(request.Filters, ref data, selectors);
            }

            return await data.ToDataSourceResultAsync(request);
        }

        public List<ProductFilterOption> GetRegionalProductsFromService<T>(IList<T> source, Func<T, string> productSelector)
        {
            if (source == null || source.Count == 0)
                return [];

            return source
                .Select(productSelector)
                .Distinct()
                .OrderBy(x => x)
                .Select(t => new ProductFilterOption { ConstraintTag = t })
                .ToList();
        }

        private async Task RebindGridAsync()
        {
            GridRegionalCapsBoundsReference?.Rebind();
            GridRegionalTransferReference?.Rebind();
            await Task.CompletedTask;
        }

        #region override calculation and setting the values                

        public void OverrideCapsBoundsMinValue(ChangeEventArgs e, object context, string overrideType)
        {
            var dataRow = (RegionalCapsBoundsModel)context;
            OverrideCapsBoundsValue(e, dataRow, overrideType, dataRow.SystemMin, dataRow.SetMinOverride, dataRow.ClearMinOverride);
        }

        public void OverrideCapsBoundsMaxValue(ChangeEventArgs e, object context, string overrideType)
        {
            var dataRow = (RegionalCapsBoundsModel)context;
            OverrideCapsBoundsValue(e, dataRow, overrideType, dataRow.SystemMax, dataRow.SetMaxOverride, dataRow.ClearMaxOverride);
        }

        public void OverrideCapsBoundsDefaultMinValue(ChangeEventArgs e, object context, string overrideType)
        {
            var dataRow = (RegionalCapsBoundsModel)context;
            OverrideCapsBoundsValue(e, dataRow, overrideType, dataRow.SystemDefaultMin, dataRow.SetDefaultMinOverride, dataRow.ClearDefaultMinOverride);
        }

        public void OverrideCapsBoundsDefaultMaxValue(ChangeEventArgs e, object context, string overrideType)
        {
            var dataRow = (RegionalCapsBoundsModel)context;
            OverrideCapsBoundsValue(e, dataRow, overrideType, dataRow.SystemDefaultMax, dataRow.SetDefaultMaxOverride, dataRow.ClearDefaultMaxOverride);
        }

        public void OverrideCapsBoundsValue(ChangeEventArgs e, RegionalCapsBoundsModel dataRow, string overrideType, decimal? systemBoundedValue, Action<OverrideCalculationResult> setOverrideValues, Action clearOverride)
        {
            if (!decimal.TryParse(e.Value?.ToString(), out var value))
            {
                clearOverride();
                GridRegionalCapsBoundsReference?.Rebind();
                return;
            }

            var calculatedResult = OverrideValueCalculatorService.CalculateUsing(overrideType).CalculateOverride(value, systemBoundedValue);
            setOverrideValues(calculatedResult);
            GridRegionalCapsBoundsReference?.Rebind();
        }

        public void OverrideTransferDefaultMinValue(ChangeEventArgs e, object context)
        {
            var dataRow = (RegionalTransferModel)context;
            var overrideValue = decimal.TryParse(e.Value?.ToString(), out var value) ? value : (decimal?)null;
            dataRow.DefaultMin = overrideValue;
            dataRow.SetOverrideFlag();
            dataRow.ValidateConstraintValue();
            GridRegionalTransferReference?.Rebind();
        }

        public void OverrideTransferMinValue(ChangeEventArgs e, object context)
        {
            var dataRow = (RegionalTransferModel)context;
            var overrideValue = decimal.TryParse(e.Value?.ToString(), out var value) ? value : (decimal?)null;
            dataRow.Min = overrideValue;
            dataRow.SetOverrideFlag();
            dataRow.ValidateConstraintValue();
            GridRegionalTransferReference?.Rebind();
        }

        public void OverrideTransferDefaultMaxValue(ChangeEventArgs e, object context)
        {
            var dataRow = (RegionalTransferModel)context;
            var overrideValue = decimal.TryParse(e.Value?.ToString(), out var value) ? value : (decimal?)null;
            dataRow.DefaultMax = overrideValue;
            dataRow.SetOverrideFlag();
            dataRow.ValidateConstraintValue();
            GridRegionalTransferReference?.Rebind();
        }

        public void OverrideTransferMaxValue(ChangeEventArgs e, object context)
        {
            var dataRow = (RegionalTransferModel)context;
            var overrideValue = decimal.TryParse(e.Value?.ToString(), out var value) ? value : (decimal?)null;
            dataRow.Max = overrideValue;
            dataRow.SetOverrideFlag();
            dataRow.ValidateConstraintValue();
            GridRegionalTransferReference?.Rebind();
        }

        public void OverrideTransferDefaultCostValue(ChangeEventArgs e, object context)
        {
            var dataRow = (RegionalTransferModel)context;
            var overrideValue = decimal.TryParse(e.Value?.ToString(), out var value) ? value : (decimal?)null;
            dataRow.DefaultCost = overrideValue;
            dataRow.SetOverrideFlag();
            dataRow.ValidateCostValue();
            GridRegionalTransferReference?.Rebind();
        }

        public void OverrideTransferCostValue(ChangeEventArgs e, object context, string overrideType)
        {
            var dataRow = (RegionalTransferModel)context;
            OverrideTransferValue(e, dataRow, overrideType, dataRow.SystemCost, dataRow.SetCostOverride, dataRow.ClearCostOverride);
        }

        public void OverrideTransferValue(ChangeEventArgs e, RegionalTransferModel dataRow, string overrideType, decimal? systemBoundedValue, Action<OverrideCalculationResult> setOverrideValues, Action clearOverride)
        {
            if (!decimal.TryParse(e.Value?.ToString(), out var value))
            {
                clearOverride();
                GridRegionalTransferReference?.Rebind();
                return;
            }

            var calculatedResult = OverrideValueCalculatorService.CalculateUsing(overrideType).CalculateOverride(value, systemBoundedValue);
            setOverrideValues(calculatedResult);
            GridRegionalTransferReference?.Rebind();
        }
        #endregion override calculation and setting the values
        protected async Task ToggleRegionalCapsBoundsGridGroupByCollapseAsync()
        {
            LoadGroupsOnRegionalCapsBounds = !LoadGroupsOnRegionalCapsBounds;
            GridRegionalCapsBoundsReference.LoadGroupsOnDemand = LoadGroupsOnRegionalCapsBounds;
            await GridRegionalCapsBoundsReference.SetStateAsync(GridRegionalCapsBoundsReference.GetState());
        }
    }
}
