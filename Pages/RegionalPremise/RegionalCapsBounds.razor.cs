using Microsoft.AspNetCore.Components;
using MPC.PlanSched.Model;
using MPC.PlanSched.Service;
using MPC.PlanSched.Shared.Common;
using Telerik.Blazor.Components;

namespace MPC.PlanSched.UI.Pages.RegionalPremise
{
    public partial class RegionalCapsBounds
    {
        [Parameter]
        public int SelectedBusinessCaseId { get; set; }
        [Parameter]
        public bool IsFirstLoad { get; set; }
        private string _localTimeZoneName = PlanNSchedConstant.DefaultTimeZone;

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);
            if (firstRender)
            {
                LockLoading();
                StateHasChanged();
                Periods = RequestedPeriods;
                BusinessCaseId = SelectedBusinessCaseId;
                _localTimeZoneName = SessionService.GetLocalTimezoneName();
                Client = ClientFactory.CreateClient("WebAPI");
                Client.Timeout = TimeSpan.FromSeconds(600);
                OverrideTypes = await UtilityUI.GetOverrideTypes(Client);
                RegionModel = await UtilityUI.GetRegionByBusinessCaseIdAsync(BusinessCaseId, SessionService.GetCorrelationId(), Client);
                Refineries = await UtilityUI.GetAllRefineryPremiseActivePlansAsync(Client, _localTimeZoneName, SessionService.GetCorrelationId());
                if (RegionModel != null)
                {
                    RegionTitle = RegionModel.BusinessCase.Name;
                    PlanUpdatedOn = "Last Saved: " + (RegionModel.BusinessCase.UpdatedOn?.ToString("MM/dd/yy") ?? "");
                    RegionName = RegionModel.RegionName;
                    PlanDescription = RegionModel.BusinessCase.Description;
                    ReadOnlyFlag = IsHistoricalData = IsHistoricalPlan;
                    if (!ConfigurationUI.IsMidtermEnabled)
                        PlanType = RegionModel.BusinessCase.PlanType ?? string.Empty;
                }

                CommonHelper.UpdateBaggage(SessionService.GetCorrelationId(),
                    RegionModel?.BusinessCase?.Id,
                    RequestedPeriods?.FirstOrDefault()?.PeriodID,
                    RegionModel?.DomainNamespace?.DestinationApplication.Name);

                await GetRegionalPremiseDataFromPublisherAsync(IsFirstLoad);

                UnlockLoading();
                StateHasChanged();
            }
        }

        public async Task OnRegionalCapsBoundsReadHandlerAsync(GridReadEventArgs args)
        {
            if (!IsReady)
            {
                args.Data = Enumerable.Empty<Model.RegionalCapsBoundsModel>();
                args.Total = 0;
                return;
            }

            var result = await BuildRegionalGridResultAsync(args.Request, RegionalCapsBoundsData, x => x.ConstraintTag);

            args.Data = result.Data;
            args.Total = result.Total;
            args.AggregateResults = result.AggregateResults;
        }
    }
}
