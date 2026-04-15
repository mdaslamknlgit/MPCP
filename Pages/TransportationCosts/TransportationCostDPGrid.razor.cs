using Microsoft.AspNetCore.Components;
using MPC.PlanSched.Model;
using MPC.PlanSched.Service;
using MPC.PlanSched.Shared.Common.Service;
using Telerik.Blazor.Components;

namespace MPC.PlanSched.UI.Pages.TransportationCosts
{
    public partial class TransportationCostDPGrid
    {
        [Inject]
        public IOverrideValueCalculatorService? OverrideValueCalculatorService { get; set; } = default!;
        [Parameter]
        public List<TransportationCost> TransportationCostData { get; set; } = [];

        public bool LoadGroupsOnDemand { get; set; } = true;
        [Parameter]
        public bool ReadOnlyFlag { get; set; }
        [Parameter]
        public bool IsDataInputDateVisible { get; set; } = false;
        [Parameter]
        public bool IsOverrideVisible { get; set; } = true;
        [Parameter]
        public bool IsReady { get; set; } = false;
        public const int TransportCostDecimalPlaces = 4;
        public bool IsManualFooter => TransportationCostData.Any(x => x.DataSource == PlanNSchedConstant.PlannerManualExcel);
        public new TelerikGrid<TransportationCost> GridTransportationCostReference { get; set; } = default!;

        public Task RebindGridAsync()
        {
            GridTransportationCostReference!.Rebind();
            return Task.CompletedTask;
        }

        public async Task OnReadHandlerAsync(GridReadEventArgs args)
        {
            if (!IsReady)
            {
                args.Data = Enumerable.Empty<TransportationCost>();
                args.Total = 0;
                return;
            }

            var result = await BuildTransportationGridResultAsync(args.Request, TransportationCostData, x => x.ToLocationName, x => x.FromLocationName, x => x.ProductName);

            args.Data = result.Data;
            args.Total = result.Total;
            args.AggregateResults = result.AggregateResults;
        }

        private async Task ToggleTransportationCostGridGroupByCollapseAsync()
        {
            LoadGroupsOnDemand = !LoadGroupsOnDemand;
            GridTransportationCostReference.LoadGroupsOnDemand = LoadGroupsOnDemand;
            await GridTransportationCostReference.SetStateAsync(GridTransportationCostReference.GetState());
        }
    }
}
