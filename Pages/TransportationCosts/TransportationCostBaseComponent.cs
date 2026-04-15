using Microsoft.AspNetCore.Components;
using MPC.PlanSched.Model;
using MPC.PlanSched.Shared.Common;
using MPC.PlanSched.Shared.Common.Service;
using MPC.PlanSched.UI.ViewModel;
using Telerik.Blazor.Components;
using Telerik.DataSource;
using Telerik.DataSource.Extensions;

namespace MPC.PlanSched.UI.Pages.TransportationCosts
{
    public class TransportationCostBaseComponent : PlanBaseComponent
    {
        [Inject]
        public IOverrideValueCalculatorService? OverrideValueCalculatorService { get; set; } = default!;
        public TelerikGrid<TransportationCost> GridTransportationCostReference { get; set; } = new();
        public const int TransportCostDecimalPlaces = 4;
        public List<LocationFilterOption> ToLocationOptions { get; set; } = [];
        public List<LocationFilterOption> FromLocationOptions { get; set; } = [];

        public async Task<DataSourceResult> BuildTransportationGridResultAsync<T>(DataSourceRequest request, IList<T> source,
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

        public List<LocationFilterOption> GetToLocationsFromService<T>(IList<T> source, Func<T, string> locationSelector)
        {
            if (source == null || source.Count == 0)
                return [];

            return source
                .Select(locationSelector)
                .Distinct()
                .OrderBy(x => x)
                .Select(t => new LocationFilterOption { ToLocationName = t })
                .ToList();
        }

        public List<LocationFilterOption> GetFromLocationsFromService<T>(IList<T> source, Func<T, string> locationSelector)
        {
            if (source == null || source.Count == 0)
                return [];

            return source
                .Select(locationSelector)
                .Distinct()
                .OrderBy(x => x)
                .Select(t => new LocationFilterOption { FromLocationName = t })
                .ToList();
        }

        #region override calculation and setting the values

        public void OverrideCostValue(ChangeEventArgs e, object context, string overrideType)
        {
            var dataRow = (TransportationCost)context;
            OverrideValue(e, dataRow, overrideType, dataRow.SystemCost, dataRow.SetCostOverride, dataRow.ClearCostOverride);
        }

        public void OverrideValue(ChangeEventArgs e, TransportationCost dataRow, string overrideType, decimal? systemBoundedValue, Action<OverrideCalculationResult> setOverrideValues, Action clearOverride)
        {
            if (!decimal.TryParse(e.Value?.ToString(), out var value))
            {
                clearOverride();
                GridTransportationCostReference?.Rebind();
                return;
            }

            value = Math.Round(value, TransportCostDecimalPlaces, MidpointRounding.AwayFromZero);
            var calculatedResult = OverrideValueCalculatorService.CalculateUsing(overrideType).CalculateOverride(value, systemBoundedValue);
            setOverrideValues(calculatedResult);
            GridTransportationCostReference?.Rebind();
        }

        public static decimal? GetValue(decimal? systemValue, decimal? calculatedValue, decimal? overrideValue, int precision) =>
         CommonHelper.IsValueDifferent(systemValue, calculatedValue, precision) ? overrideValue : null;

        #endregion override calculation and setting the values
    }
}
