

using Microsoft.AspNetCore.Components;
using MPC.PlanSched.Model;
using MPC.PlanSched.Service;
using Telerik.Blazor.Components;

namespace MPC.PlanSched.UI.Pages.RefineryPremise
{
    public partial class RefineryPremiseConstraint
    {
        [Parameter]
        public bool IsFirstLoad { get; set; }
        [Parameter]
        public new RefineryModel RefineryModel { get; set; } = new();

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);
            if (firstRender)
            {
                LockLoading();
                StateHasChanged();
                Client = ClientFactory.CreateClient(PlanNSchedConstant.WebAPI);
                Client.Timeout = TimeSpan.FromSeconds(PlanNSchedConstant.Timeout);
                base.RefineryModel = RefineryModel;
                Periods = RequestedPeriods;
                BusinessCaseId = SelectedBusinessCaseId;
                BusinessCase = await UtilityUI.GetBusinessCaseByBusinessCaseIdAsync(BusinessCaseId, SessionService.GetCorrelationId(), Client);
                if (BusinessCase != null)
                {
                    PlanName = BusinessCase.Name;
                    PlanUpdatedOn = PlanNSchedConstant.LastSaved + (BusinessCase.UpdatedOn?.ToString(PlanNSchedConstant.DateFormatMMDDYY) ?? string.Empty);
                    PlanDescription = BusinessCase.Description;
                    IsHistoricalData = IsHistoricalPlan;
                    ReadOnlyFlag = IsHistoricalData;
                }
                await GetRefineryPremiseDataFromPublisherAsync(IsFirstLoad);

                UnlockLoading();
                StateHasChanged();
            }
        }

        public async Task OnReadHandlerAsync(GridReadEventArgs args)
        {
            if (!IsReady)
            {
                args.Data = Enumerable.Empty<Model.RefineryPremiseConstraintModel>();
                args.Total = 0;
                return;
            }

            var result = await BuildGridResultAsync(args.Request, RefineryConstraintData, x => x.ConstraintTag);

            args.Data = result.Data;
            args.Total = result.Total;
            args.AggregateResults = result.AggregateResults;
        }
    }
}
