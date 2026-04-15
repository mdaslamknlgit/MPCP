using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using MPC.PlanSched.Model;
using MPC.PlanSched.Service;
using MPC.PlanSched.Shared.Common.Extensions;
using Telerik.Blazor.Components;

namespace MPC.PlanSched.UI.Pages.ProductDemandPrice
{
    public partial class ProductDemandPriceRPGrid
    {
        [Inject]
        public new ILogger<ProductDemandPriceRPGrid> Logger { get; set; } = default!;
        [Parameter]
        public new List<ProductDemandAndPrice> ProductDemandAndPriceData { get; set; } = [];
        [Parameter]
        public new bool ReadOnlyFlag { get; set; } = false;
        [Parameter]
        public new bool IsReady { get; set; } = false;
        [Parameter]
        public bool IsDataInputDateVisible { get; set; } = false;
        [Parameter]
        public bool IsOverrideVisible { get; set; } = true;
        [Parameter]
        public new bool IsTierVisible { get; set; } = false;
        [Parameter]
        public new bool IsLocationVisible { get; set; } = true;
        public bool IsManualFooter => ProductDemandAndPriceData.Any(x => x.DataSource == PlanNSchedConstant.PlannerManualExcel);
        public new TelerikGrid<ProductDemandAndPrice> GridDemandReference { get; set; } = default!;

        public Task RebindGridAsync()
        {
            GridDemandReference!.Rebind();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Update total min. demand value for single product during a single period
        /// </summary>
        /// <param name"record"></param> <param name"newValue"></param>
        /// <returns></returns>
        public void UpdateLineChartProductMinDemandData(ProductDemandAndPrice record, decimal? newValue, decimal? oldTotal)
        {
            var data = new List<object>();
            if (!TotalMinDemandLineChartSeries.Any()) return;
            try
            {
                Logger.LogMethodStart();
                var periodNumber = Regex.Match(record.PeriodName, @"\d+").Value;
                var periodIndex = int.Parse(periodNumber) - 1;

                var updateIndex = TotalMinDemandLineChartSeries.FindIndex(x => x.name == record.ProductDescription);
                data = TotalMinDemandLineChartSeries[updateIndex].data;

                if (data == null)
                {
                    Logger.LogMethodError(new Exception("Data is null while updating Line Chart for Min Demand Data."));
                    return;
                }

                var newTotal = (decimal)data[periodIndex] + newValue;
                var latestTotal = newTotal - oldTotal;

                data[periodIndex] = latestTotal;
                Logger.LogMethodEnd();
            }
            catch (Exception)
            {
                data = null;
                Logger.LogMethodError(new Exception("Data is null before updating Line Chart for Min Demand Data."));
            }
        }

        protected new async Task ToggleDemandGridGroupByCollapseAsync()
        {
            LoadGroupsOnDemand = !LoadGroupsOnDemand;
            GridDemandReference.LoadGroupsOnDemand = LoadGroupsOnDemand;
            await GridDemandReference.SetStateAsync(GridDemandReference.GetState());
        }
    }
}