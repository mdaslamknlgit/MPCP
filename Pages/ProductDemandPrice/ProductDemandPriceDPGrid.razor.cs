using Microsoft.AspNetCore.Components;
using MPC.PlanSched.Model;
using MPC.PlanSched.Service;
using MPC.PlanSched.Shared.Common;
using Telerik.Blazor.Components;
using Telerik.DataSource;
using Telerik.DataSource.Extensions;

namespace MPC.PlanSched.UI.Pages.ProductDemandPrice
{
    public partial class ProductDemandPriceDPGrid
    {
        [Parameter]
        public new List<ProductDemandAndPrice> ProductDemandAndPriceData { get; set; } = [];
        [Parameter]
        public new bool IsHistoricalData { get; set; } = false;
        [Parameter]
        public bool IsDataInputDateVisible { get; set; } = false;
        [Parameter]
        public bool IsOverrideVisible { get; set; } = true;
        [Parameter]
        public bool IsPIMSLocationVisible { get; set; } = true;
        [Parameter]
        public new bool IsAggregatedTier { get; set; }
        [Parameter]
        public new bool IsAggregatedTierVisible { get; set; }
        [Parameter]
        public new bool ReadOnlyFlag { get; set; } = false;
        [Parameter]
        public new bool IsReady { get; set; } = false;
        [Parameter]
        public EventCallback<bool> HandleTierAggregationChange { get; set; } = default!;

        public decimal? BulkMinDemandFactorValue { get; set; }
        public decimal? BulkMaxDemandFactorValue { get; set; }
        public bool IsManualFooter => ProductDemandAndPriceData.Any(x => x.DataSource == PlanNSchedConstant.PlannerManualExcel);
        public bool IsSouthRegion => ProductDemandAndPriceData.FirstOrDefault()?.BusinessCaseName?.Contains(Constant.SouthRegion, StringComparison.OrdinalIgnoreCase) ?? false;
        public new TelerikGrid<ProductDemandAndPrice> GridDemandReference { get; set; } = default!;

        public Task RebindGridAsync()
        {
            GridDemandReference!.Rebind();
            return Task.CompletedTask;
        }
        protected async Task OnCheckBoxChangedAsync(bool isAggregatedTier)
        {
            if (HandleTierAggregationChange.HasDelegate)
            {
                await HandleTierAggregationChange.InvokeAsync(isAggregatedTier);
            }
        }

        public List<ProductDemandAndPrice> GetFilteredRecordsForBulkEdit()
        {
            if (GridDemandReference == null || ProductDemandAndPriceData == null || ProductDemandAndPriceData.Count == 0)
            {
                Logger?.LogWarning("Grid reference or data is empty in GetFilteredRecordsForBulkEdit");
                return ProductDemandAndPriceData ?? new List<ProductDemandAndPrice>();
            }

            var state = GridDemandReference.GetState();

            if (state?.FilterDescriptors == null || state.FilterDescriptors.Count == 0)
            {
                return ProductDemandAndPriceData;
            }

            try
            {
                var query = ProductDemandAndPriceData.AsQueryable();

                var request = new DataSourceRequest
                {
                    Filters = state.FilterDescriptors.ToList()
                };

                var result = query.ToDataSourceResult(request);

                return result.Data.Cast<ProductDemandAndPrice>().ToList();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Error applying filters in GetFilteredRecordsForBulkEdit");
                return ProductDemandAndPriceData;
            }
        }

        public void ApplyBulkOverride(string overrideType, decimal? value, string demandType)
        {
            if (!value.HasValue)
            {
                Logger?.LogWarning("Bulk override value is empty for {DemandType} - {OverrideType}", demandType, overrideType);
                return;
            }

            var filteredRecords = GetFilteredRecordsForBulkEdit();
            if(demandType == PlanNSchedConstant.MinDemand)
            {
                filteredRecords = filteredRecords
                    .Where(x => x.SystemMinDemand.HasValue && x.SystemMinDemand.Value != 0)
                    .ToList();
            }
            else
            {
                filteredRecords = filteredRecords
                    .Where(x => x.SystemMaxDemand.HasValue && x.SystemMaxDemand.Value != 0)
                    .ToList();
            }

            if (filteredRecords.Count == 0)
            {
                Logger?.LogWarning("No filtered records found for bulk override");
                return;
            }

            var changeEventArgs = new ChangeEventArgs { Value = value.Value.ToString() };

            foreach (var item in filteredRecords)
            {
                if (demandType == PlanNSchedConstant.MinDemand)
                {
                    OverrideMinDemandValue(changeEventArgs, item, overrideType);
                }
                else if (demandType == PlanNSchedConstant.MaxDemand)
                {
                    OverrideMaxDemandValue(changeEventArgs, item, overrideType);
                }
            }

            Logger?.LogInformation("Bulk override operation completed: {Operation} applied to {RecordCount} records with {DemandType} - {OverrideType} = {Value}",
                "Apply", filteredRecords.Count, demandType, overrideType, value);
            GridDemandReference?.Rebind();
            StateHasChanged();
        }

        public void ApplyBulkMinDemandFactor()
        {
            ApplyBulkOverride(UIConstants.FactorEntityValue, BulkMinDemandFactorValue, PlanNSchedConstant.MinDemand);
            BulkMinDemandFactorValue = null;
        }

        public void ApplyBulkMaxDemandFactor()
        {
            ApplyBulkOverride(UIConstants.FactorEntityValue, BulkMaxDemandFactorValue, PlanNSchedConstant.MaxDemand);
            BulkMaxDemandFactorValue = null;
        }

        public void ClearMinDemandFilteredOverrides()
        {
            var filteredRecords = GetFilteredRecordsForBulkEdit();

            if (filteredRecords.Count == 0)
            {
                Logger?.LogWarning("No filtered records found for clearing {DemandType} overrides", PlanNSchedConstant.MinDemand);
                return;
            }

            var recordsWithFactorOverrides = filteredRecords
                .Where(item => (item.OverrideMinDemandQtyValueTypeName == UIConstants.PercentageEntityValue || item.OverrideMinDemandQtyValueTypeName == UIConstants.FactorEntityValue) &&
                               (item.MinDemandOverrideFactor.HasValue || CommonHelper.IsValueDifferent(item.SystemMinDemand, item.MinDemandOverrideCalculated, Constant.DisplayQtyDecimalPlaces)))
                .ToList();

            if (recordsWithFactorOverrides.Count == 0)
            {
                Logger?.LogInformation("No records with Min Demand Factor overrides found in filtered set");
                BulkMinDemandFactorValue = null;
                return;
            }

            foreach (var item in recordsWithFactorOverrides)
            {
                item.MinDemandOverrideFactor = null;
                item.ClearMinDemandOverride();
            }

            Logger?.LogInformation("Bulk override operation completed: {Operation} applied to {RecordCount} records with {DemandType}",
                "Clear Factor", recordsWithFactorOverrides.Count, PlanNSchedConstant.MinDemand);
            BulkMinDemandFactorValue = null;
            GridDemandReference?.Rebind();
            StateHasChanged();
        }

        public void ClearMaxDemandFilteredOverrides()
        {
            var filteredRecords = GetFilteredRecordsForBulkEdit();

            if (filteredRecords.Count == 0)
            {
                Logger?.LogWarning("No filtered records found for clearing {DemandType} overrides", PlanNSchedConstant.MaxDemand);
                return;
            }

            var recordsWithFactorOverrides = filteredRecords
                .Where(item => (item.OverrideMaxDemandQtyValueTypeName == UIConstants.PercentageEntityValue || item.OverrideMaxDemandQtyValueTypeName == UIConstants.FactorEntityValue) &&
                               (item.MaxDemandOverrideFactor.HasValue || CommonHelper.IsValueDifferent(item.SystemMaxDemand, item.MaxDemandOverrideCalculated, Constant.DisplayQtyDecimalPlaces)))
                .ToList();

            if (recordsWithFactorOverrides.Count == 0)
            {
                Logger?.LogInformation("No records with Max Demand Factor overrides found in filtered set");
                BulkMaxDemandFactorValue = null;
                return;
            }

            foreach (var item in recordsWithFactorOverrides)
            {
                item.MaxDemandOverrideFactor = null;
                item.ClearMaxDemandOverride();
            }

            Logger?.LogInformation("Bulk override operation completed: {Operation} applied to {RecordCount} records with {DemandType}",
                "Clear Factor", recordsWithFactorOverrides.Count, PlanNSchedConstant.MaxDemand);
            BulkMaxDemandFactorValue = null;
            GridDemandReference?.Rebind();
            StateHasChanged();
        }

        protected new async Task ToggleDemandGridGroupByCollapseAsync()
        {
            LoadGroupsOnDemand = !LoadGroupsOnDemand;
            GridDemandReference.LoadGroupsOnDemand = LoadGroupsOnDemand;
            await GridDemandReference.SetStateAsync(GridDemandReference.GetState());
        }
    }
}
