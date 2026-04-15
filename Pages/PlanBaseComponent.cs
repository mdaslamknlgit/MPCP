using Microsoft.AspNetCore.Components;
using MPC.PlanSched.Model;
using MPC.PlanSched.Service;
using MPC.PlanSched.Shared.Common;
using MPC.PlanSched.Shared.Notification.Model;
using MPC.PlanSched.UI.Shared;
using MPC.PlanSched.UI.ViewModel;
using Telerik.DataSource;
using Telerik.DataSource.Extensions;

namespace MPC.PlanSched.UI.Pages
{
    public class PlanBaseComponent : CommonBase
    {
        [Parameter]
        public string? RegionTitle { get; set; }
        [Parameter]
        public string? PlanUpdatedOn { get; set; }
        public int SelectedPeriodId { get; set; } = 1;
        [Parameter]
        public string? RegionName { get; set; }
        [Parameter]
        public int BusinessCaseId { get; set; } = 0;
        [Parameter]
        public bool IsHistoricalPlan { get; set; } = false;
        [CascadingParameter]
        public INavigationDrawer NavigationDrawer { get; set; } = default!;
        [CascadingParameter(Name = "SelectedRole")]
        public string UserSelectedRole { get; set; } = string.Empty;
        public PlanSched.Shared.Service.Schema.BusinessCase BusinessCase { get; set; } = new();
        public bool NoneSelected { get; set; } = true;
        public void ResetGroupByButtonSelection() => NoneSelected = true;
        public string DataLoadingStatusMessage { get; set; } = string.Empty;
        public List<LocationFilterOption> LocationOptions { get; set; } = [];
        public List<ProductFilterOption> ProductOptions { get; set; } = [];

        protected override Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                NavigationDrawer.SetExpanded(false);

                if (UserSelectedRole.Contains(PlanNSchedConstant.DPO, StringComparison.InvariantCultureIgnoreCase))
                {
                    NavigationDrawer.SetActiveMenuItemByUrl(string.Format
                        (IsHistoricalPlan ? PlanNSchedConstant.HistoricalDp : PlanNSchedConstant.RegionsDp, UserSelectedRole));
                }
                else
                {
                    if (NavigationManager.Uri.Contains(Constant.Backcasting, StringComparison.InvariantCultureIgnoreCase))
                    {
                        NavigationDrawer.SetActiveMenuItemByUrl(string.Format
                        (IsHistoricalPlan ? PlanNSchedConstant.HistoricalBackcasting : PlanNSchedConstant.RegionsBackcasting, UserSelectedRole));
                    }
                    else if (NavigationManager.Uri.Contains(Constant.RefineryPremise, StringComparison.InvariantCultureIgnoreCase) || NavigationManager.Uri.Contains(Constant.RefineryPlanning, StringComparison.InvariantCultureIgnoreCase))
                    {
                        NavigationDrawer.SetActiveMenuItemByUrl(string.Format
                            (IsHistoricalPlan ? PlanNSchedConstant.HistoricalRefineryPlanning : PlanNSchedConstant.Refineryplanning, UserSelectedRole));
                    }
                    else
                    {
                        NavigationDrawer.SetActiveMenuItemByUrl(string.Format
                        (IsHistoricalPlan ? PlanNSchedConstant.HistoricalRp : PlanNSchedConstant.RegionsRp, UserSelectedRole));
                    }
                }
            }

            return Task.CompletedTask;
        }

        public DateTime ConvertUTCDateToLocal(DateTime dateInUTC)
        {
            DateTime LocalDateTime;
            var _localTimeZoneName = SessionService.GetLocalTimezoneName();
            var cstZone = TimeZoneInfo.FindSystemTimeZoneById(_localTimeZoneName);

            if (_localTimeZoneName == PlanNSchedConstant.DefaultTimeZone)
            {
                LocalDateTime = dateInUTC.ToLocalTime();
            }
            else
            {
                LocalDateTime = TimeZoneInfo.ConvertTimeFromUtc(dateInUTC, cstZone);
            }
            return LocalDateTime;
        }

        public static PlanStatus GetPlanStatus(List<AZFunctionResponse> functionResponses)
        {
            if (functionResponses.Count == 0 || functionResponses.TrueForAll(t => t.Status))
                return PlanStatus.SUCCESS;
            return functionResponses.Exists(t => t.Status) ? PlanStatus.PARTIAL : PlanStatus.FAILED;
        }

        public async Task<DataSourceResult> BuildGridResultAsync<T>(DataSourceRequest request, IList<T> source,
            Func<T, string> locationSelector, Func<T, string> productSelector)
        {
            if (LocationOptions.Count == 0 || ProductOptions.Count == 0)
            {
                LocationOptions = GetLocationsFromService(source, locationSelector);
                ProductOptions = GetProductsFromService(source, productSelector);
            }

            IEnumerable<T> data = source;

            if (request.Filters.Any())
            {
                var selectors = new Dictionary<string, Func<T, string>>
                {
                    { "LocationName", locationSelector },
                    { "ProductName", productSelector }
                };
                ApplyFilters(request.Filters, ref data, selectors);
            }

            return await data.ToDataSourceResultAsync(request);
        }

        public List<LocationFilterOption> GetLocationsFromService<T>(IList<T> source, Func<T, string> locationSelector)
        {
            if (source == null || source.Count == 0)
                return [];

            return source
                .Select(locationSelector)
                .Distinct()
                .OrderBy(x => x)
                .Select(t => new LocationFilterOption { LocationName = t })
                .ToList();
        }

        public List<ProductFilterOption> GetProductsFromService<T>(IList<T> source, Func<T, string> productSelector)
        {
            if (source == null || source.Count == 0)
                return [];

            return source
                .Select(productSelector)
                .Distinct()
                .OrderBy(x => x)
                .Select(t => new ProductFilterOption { ProductName = t })
                .ToList();
        }

        public static void ApplyFilters<T>(IEnumerable<IFilterDescriptor> filters, ref IEnumerable<T> data,
            Dictionary<string, Func<T, string>> selectors)
        {
            foreach (var filter in filters)
            {
                if (filter is CompositeFilterDescriptor composite)
                    ApplyCompositeFilterDescriptor(composite, ref data, selectors);

                if (filter is FilterDescriptor simple)
                    SimpleFilterDescriptor(simple, ref data, selectors);
            }
        }

        public static void ApplyCompositeFilterDescriptor<T>(CompositeFilterDescriptor composite, ref IEnumerable<T> data,
            Dictionary<string, Func<T, string>> selectors)
        {
            foreach (var selector in selectors)
            {
                var values = composite.FilterDescriptors
                    .OfType<FilterDescriptor>()
                    .Where(f => f.Member == selector.Key)
                    .Select(f => f.Value?.ToString())
                    .Where(v => v != null)
                    .Cast<string>()
                    .ToList();

                if (values.Count > 0)
                {
                    var valueSet = new HashSet<string>(values, StringComparer.Ordinal);

                    data = data.Where(x =>
                    {
                        var fieldValue = selector.Value(x);
                        return fieldValue != null && valueSet.Contains(fieldValue);
                    });
                }
            }

            var nestedComposites = composite.FilterDescriptors
                .OfType<CompositeFilterDescriptor>();

            foreach (var nested in nestedComposites)
            {
                ApplyFilters(nested.FilterDescriptors, ref data, selectors);
            }
        }

        public static void SimpleFilterDescriptor<T>(FilterDescriptor simple, ref IEnumerable<T> data, Dictionary<string, Func<T, string>> selectors)
        {
            var value = simple.Value?.ToString();
            if (string.IsNullOrEmpty(value)) return;

            if (!selectors.TryGetValue(simple.Member, out var selector))
                return;

            data = data.Where(x =>
            {
                var fieldValue = selector(x);
                return fieldValue != null &&
                       fieldValue.Equals(value, StringComparison.Ordinal);
            });
        }
    }
}
