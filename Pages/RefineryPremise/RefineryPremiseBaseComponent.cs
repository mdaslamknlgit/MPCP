using System.Collections.Concurrent;
using System.Reflection.Metadata;
using Microsoft.AspNetCore.Components;
using MPC.PlanSched.Model;
using MPC.PlanSched.Service;
using MPC.PlanSched.Shared.Common.Extensions;
using MPC.PlanSched.Shared.Service.Schema;
using MPC.PlanSched.UI.ViewModel;
using Newtonsoft.Json;
using Telerik.Blazor.Components;
using Telerik.DataSource;
using Telerik.DataSource.Extensions;

namespace MPC.PlanSched.UI.Pages.RefineryPremise
{
    public class RefineryPremiseBaseComponent : PlanBaseComponent
    {
        [Inject]
        public ILogger<RefineryPremiseBaseComponent> Logger { get; set; } = default!;
        public List<PinvConstraintModel> PinvConstraintModelData { get; set; } = [];
        public List<RefineryPremiseConstraintModel> RefineryConstraintData { get; set; } = [];
        [Parameter]
        public string SelectedConstraint { get; set; } = string.Empty;
        [Parameter]
        public int SelectedPeriodId { get; set; } = 0;
        [Parameter]
        public List<RequestedPeriodModel> RequestedPeriods { get; set; }
        [Parameter]
        public int SelectedBusinessCaseId { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public bool IsReady { get; set; } = false;
        public string PlanDescription { get; set; } = string.Empty;
        public bool IsHistoricalData { get; set; } = true;
        public bool LoadGroupsOnRefinery { get; set; } = true;
        public const string GetRefineryPremiseDataFromPublisher = "GetRefineryPremiseDataFromPublisherAsync";
        public const string GetRefineryPremiseConstraint = "GetRefineryPremiseConstraintAsync";
        public const string RefineryPremisePeriodSelectionChanged = "RefineryPremisePeriodSelectionChanged";
        public TelerikGrid<PinvConstraintModel> GridRefineryConstraintPinvReference { get; set; } = default!;
        public TelerikGrid<RefineryPremiseConstraintModel> GridRefineryConstraintReference { get; set; } = default!;

        public async Task GetRefineryPremiseDataFromPublisherAsync(bool isFirstLoad)
        {
            try
            {
                if (isFirstLoad)
                {
                    LockLoading();
                    StateHasChanged();
                    isFirstLoad = false;
                    var tasks = Periods.Select(async (requestedPeriodModel, index) =>
                    {
                        var functionResponses = new ConcurrentBag<AZFunctionResponse>();
                        requestedPeriodModel.CorrelationId = SessionService.GetCorrelationId();
                        requestedPeriodModel.QuantityEntityType = ConfigurationUI.DemandTypeName;
                        requestedPeriodModel.PriceEntityType = ConfigurationUI.PriceTypeName;
                        requestedPeriodModel.ApplicationState = Service.Model.State.Forecast.Description();
                        requestedPeriodModel.BusinessCase = BusinessCase;
                        requestedPeriodModel.DomainNamespace.SourceApplication.Name = PlanNSchedConstant.RefineryPremiseExcel;

                        var isConstraintExistResult = await UtilityUI.IsRefineryPremiseDataLoadedForPeriodAsync(requestedPeriodModel, SelectedConstraint, RefineryModel.RefineryCode, Client);

                        if (isConstraintExistResult.Values.Where(e => e.Equals(false)).Any() && !IsHistoricalData)
                        {
                            var refineryCode = requestedPeriodModel.DomainNamespace.DestinationApplication.Name.Split()[0];
                            (await UtilityUI.LoadRefineryPremiseConstraintDataFromPublisherByPeriodAsync(requestedPeriodModel, isConstraintExistResult, refineryCode, Client, Logger)).ForEach(functionResponses.Add);
                        }
                        if (index == 0)
                            await GetRefineryPremiseConstraintAsync(requestedPeriodModel, SelectedConstraint);
                    }).ToList();
                    await Task.WhenAll(tasks);
                }
                else
                {
                    var requestedPeriodModel = Periods.Where(p => p.PeriodID == SelectedPeriodId).FirstOrDefault();
                    await GetRefineryPremiseConstraintAsync(requestedPeriodModel, SelectedConstraint);
                }

                UnlockLoading();
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Logger.LogMethodError(ex, "Error occurred in " + GetRefineryPremiseDataFromPublisher + " method.");
            }
        }

        public async Task GetRefineryPremiseConstraintAsync(RequestedPeriodModel requestedPeriodModel, string selectedConstaint)
        {
            try
            {
                if (requestedPeriodModel.DomainNamespace.DestinationApplication.Name.Contains("XPIMS"))
                {
                    SelectedConstraint = selectedConstaint.Split(" ")[1];
                }
                else
                {
                    SelectedConstraint = selectedConstaint;
                }
                var result = await UtilityUI.GetRefineryPremiseConstraintDataAsync(requestedPeriodModel, SelectedConstraint, Client);

                if (SelectedConstraint == RefineryPremiseConstraintType.Pinv.Description())
                {
                    PinvConstraintModelData = JsonConvert.DeserializeObject<List<Model.PinvConstraintModel>>(result)?.ToList() ?? [];
                }
                else
                {
                    RefineryConstraintData = JsonConvert.DeserializeObject<List<RefineryPremiseConstraintModel>>(result)?.ToList() ?? [];
                }

                if (requestedPeriodModel.DomainNamespace.DestinationApplication.Name.Contains(PlanNSchedConstant.PIMS))
                {
                    var refineryCode = SelectedConstraint = selectedConstaint.Split(" ")[0];
                    RefineryConstraintData = RefineryConstraintData.Where(r => r.Refinery == refineryCode).ToList();
                    PinvConstraintModelData = PinvConstraintModelData.Where(r => r.Refinery == refineryCode).ToList();
                }
                IsReady = true;
                await InvokeAsync(RebindGridAsync);
            }
            catch (Exception ex)
            {
                UnlockLoading();
                StateHasChanged();
                Logger.LogMethodError(ex, "Error occurred in " + GetRefineryPremiseConstraint + " method.");
            }
        }

        public async Task<DataSourceResult> BuildGridResultAsync<T>(DataSourceRequest request, IList<T> source,
           Func<T, string> productSelector)
        {
            if (ProductOptions.Count == 0)
            {
                ProductOptions = GetRefineryProductsFromService(source, productSelector);
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

        public List<ProductFilterOption> GetRefineryProductsFromService<T>(IList<T> source, Func<T, string> productSelector)
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
            GridRefineryConstraintPinvReference?.Rebind();
            GridRefineryConstraintReference?.Rebind();
            await Task.CompletedTask;
        }
    }
}
