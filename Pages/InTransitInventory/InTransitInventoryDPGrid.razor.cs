using Microsoft.AspNetCore.Components;
using MPC.PlanSched.Model;
using MPC.PlanSched.Service;
using MPC.PlanSched.Shared.Common;
using MPC.PlanSched.Shared.Common.Service;
using MPC.PlanSched.UI.ViewModel;
using Telerik.Blazor.Components;
using Telerik.DataSource;
using Telerik.DataSource.Extensions;

namespace MPC.PlanSched.UI.Pages.InTransitInventory
{
    public partial class InTransitInventoryDPGrid
    {
        [Parameter]
        public List<Model.InTransitInventory> InTransitInventories { get; set; } = [];
        [Parameter]
        public List<ValueTypes> OverrideTypes { get; set; } = [];
        [Parameter]
        public bool IsReady { get; set; } = false;
        [Parameter]
        public bool IsDataInputDateVisible { get; set; } = false;
        [Parameter]
        public bool IsOverrideVisible { get; set; } = true;
        [Inject]
        public IOverrideValueCalculatorService? OverrideValueCalculatorService { get; set; } = default!;
        public bool ReadOnlyFlag { get; set; } = false;
        public bool LoadGroupsOnDemand { get; set; } = true;
        public const int inventoryQuantityDecimalPlaces = 6;
        public TelerikGrid<Model.InTransitInventory> GridInTransitInventoryReference { get; set; } = default!;
        public List<LocationFilterOption> ToLocationOptions { get; set; } = [];
        public List<LocationFilterOption> FromLocationOptions { get; set; } = [];
        public bool IsManualFooter => InTransitInventories.Any(x => x.DataSource == PlanNSchedConstant.PlannerManualExcel);

        public Task RebindGridAsync()
        {
            GridInTransitInventoryReference!.Rebind();
            return Task.CompletedTask;
        }

        public async Task OnReadHandlerAsync(GridReadEventArgs args)
        {
            if (!IsReady)
            {
                args.Data = Enumerable.Empty<Model.InTransitInventory>();
                args.Total = 0;
                return;
            }

            var result = await BuildInTransitGridResultAsync(args.Request, InTransitInventories, x => x.ToLocationName, x => x.FromLocationName, x => x.ProductName);

            args.Data = result.Data;
            args.Total = result.Total;
            args.AggregateResults = result.AggregateResults;
        }

        public async Task<DataSourceResult> BuildInTransitGridResultAsync<T>(DataSourceRequest request, IList<T> source,
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

        private async Task ToggleInventoryGridGroupByCollapseAsync()
        {
            LoadGroupsOnDemand = !LoadGroupsOnDemand;
            GridInTransitInventoryReference.LoadGroupsOnDemand = LoadGroupsOnDemand;
            await GridInTransitInventoryReference.SetStateAsync(GridInTransitInventoryReference.GetState());
        }
        #region Override
        public void OnInventoryTypeChanged(ChangeEventArgs e, object context)
        {
            var dataRow = (Model.InTransitInventory)context;
            OverrideInventoryValues(e, dataRow, dataRow.SetInventoriesOverrideOnTypeChange);
        }

        public void OverrideInventoryValues(ChangeEventArgs e, Model.InTransitInventory dataRow, Action<OverrideCalculationResult> setOverrideValues)
        {
            var calculatedResult = OverrideValueCalculatorService.CalculateUsing(e.Value.ToString()).CalculateInventoryOverride(dataRow.OverrideQty, dataRow.SystemQty);
            setOverrideValues(calculatedResult);
            GridInTransitInventoryReference?.Rebind();
        }

        public void OnInventoryOverride(ChangeEventArgs e, object context)
        {
            var dataRow = (Model.InTransitInventory)context;
            OverrideValue(e, dataRow, dataRow.OverrideQtyValueTypeName, dataRow.SystemQty, dataRow.SetInventoryOverride, dataRow.ClearIntransitOverride);
        }

        public void OverrideValue(ChangeEventArgs e, Model.InTransitInventory dataRow, string? overrideType, decimal? systemBoundedValue,
           Action<OverrideCalculationResult> setOverrideValues, Action setOverrideFlag)
        {
            if (!decimal.TryParse(e.Value?.ToString(), out var value))
            {
                setOverrideFlag();
                GridInTransitInventoryReference?.Rebind();
                return;
            }

            value = Math.Round(value, inventoryQuantityDecimalPlaces, MidpointRounding.AwayFromZero);
            var calculatedResult = OverrideValueCalculatorService.CalculateUsing(overrideType).CalculateInventoryOverride(value, systemBoundedValue);
            setOverrideValues(calculatedResult);
            GridInTransitInventoryReference?.Rebind();
        }

        public void OnLeadDayInventoryOverride(ChangeEventArgs e, object context)
        {
            var dataRow = (Model.InTransitInventory)context;
            OverrideLeadDayValue(e, dataRow, dataRow.SystemLeadDay, dataRow.SetLeadDayInventoryOverride, dataRow.ClearLeadDaysOverride);
        }

        public void OverrideLeadDayValue(ChangeEventArgs e, Model.InTransitInventory dataRow, int? systemBoundedValue,
          Action<int> setOverrideValues, Action setOverrideFlag)
        {
            if (!int.TryParse(e.Value?.ToString(), out var value))
            {
                setOverrideFlag();
                GridInTransitInventoryReference?.Rebind();
                return;
            }
            setOverrideValues(value);
            GridInTransitInventoryReference?.Rebind();
        }
        #endregion Override
    }
}
