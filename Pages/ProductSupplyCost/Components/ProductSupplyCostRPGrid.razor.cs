using Microsoft.AspNetCore.Components;
using MPC.PlanSched.Model;
using MPC.PlanSched.Service;
using MPC.PlanSched.Shared.Common.Service;
using Telerik.Blazor.Components;

namespace MPC.PlanSched.UI.Pages.ProductSupplyCost.Components
{
    public partial class ProductSupplyCostRPGrid
    {
        [Inject]
        public IOverrideValueCalculatorService? OverrideValueCalculatorService { get; set; } = default!;
        [Parameter]
        public new List<ProductSupplyAndCost> ProductSupplyAndCostData { get; set; } = [];
        [Parameter]
        public new bool ReadOnlyFlag { get; set; }
        [Parameter]
        public new bool IsReady { get; set; } = false;
        [Parameter]
        public bool IsDataInputDateVisible { get; set; } = false;
        [Parameter]
        public bool IsOverrideVisible { get; set; } = true;
        [Parameter]
        public bool IsLocationVisible { get; set; } = true;
        public bool IsManualFooter => ProductSupplyAndCostData.Any(x => x.DataSource == PlanNSchedConstant.PlannerManualExcel);

        public new TelerikGrid<ProductSupplyAndCost> GridSupplyReference { get; set; } = default!;

        public Task RebindGridAsync()
        {
            GridSupplyReference!.Rebind();
            return Task.CompletedTask;
        }

        protected new async Task ToggleSupplyGridGroupByCollapseAsync()
        {
            LoadGroupsOnSupply = !LoadGroupsOnSupply;
            GridSupplyReference.LoadGroupsOnDemand = LoadGroupsOnSupply;
            await GridSupplyReference.SetStateAsync(GridSupplyReference.GetState());
        }
    }
}
