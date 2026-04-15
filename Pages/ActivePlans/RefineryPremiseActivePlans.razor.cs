using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MPC.PlanSched.Model;
using MPC.PlanSched.Service;
using MPC.PlanSched.Shared.Common;
using MPC.PlanSched.Shared.Common.Extensions;
using MPC.PlanSched.UI.Shared;
using static MPC.PlanSched.UI.UtilityUI;

namespace MPC.PlanSched.UI.Pages.ActivePlans
{
    public partial class RefineryPremiseActivePlans
    {
        [Parameter]
        public string SelectedRole { get; set; } = string.Empty;
        [Inject]
        public ILogger<RefineryPremiseActivePlans> Logger { get; set; } = default!;
        public int DomainNamespaceId { get; set; }
        private List<RefineryModel> Refineries { get; set; } = [];
        private ApplicationArea Area => ApplicationArea.refineryplanning;
        private string _localTimezone = string.Empty;
        private string _localTimeZoneName = PlanNSchedConstant.DefaultTimeZone;
        private Dialog _dialog = default!;
        private RefineryPremiseRefreshDialog _refreshModal = default!;

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                LockLoading();
                StateHasChanged();
                CommonHelper.UpdateBaggage(SessionService.GetCorrelationId());
                _localTimeZoneName = SessionService.GetLocalTimezoneName();
                _localTimezone = "Last Saved Plan (" + _localTimeZoneName + ")";
                SessionService.SetPlanType(PlanNSchedConstant.ActivePlan);
                Client = ClientFactory.CreateClient(PlanNSchedConstant.WebAPI);
                Client.Timeout = TimeSpan.FromSeconds(600);
                await GetAllRefineryPremiseActivePlan();
                UnlockLoading();
                StateHasChanged();
            }
            await base.OnAfterRenderAsync(firstRender);
        }

        private async Task GetAllRefineryPremiseActivePlan()
        {
            Logger.LogMethodInfo("Start of " + PlanNSchedConstant.GetRefineries + ".");
            LockLoading();
            try
            {
                Refineries = await UtilityUI.GetAllRefineryPremiseActivePlansAsync(Client, _localTimeZoneName, SessionService.GetCorrelationId(), ActiveUser);
            }
            catch (Exception ex)
            {
                Logger.LogErrorAndNotify(PopupService, ex);
            }
            finally
            {
                UnlockLoading();
                Logger.LogMethodInfo("End of " + PlanNSchedConstant.GetRefineries + ".");
            }
        }

        public async Task ExcelDownloadAsync(RefineryModel refineryModel)
        {
            try
            {
                Logger.LogMethodStart();
                LockLoading();
                var response = await Client.PostAsJsonAsync(ConfigurationUI.GetExcelPlanByRefinery, refineryModel);
                if (!response.IsSuccessStatusCode)
                {
                    Logger.LogMethodWarning($"Failed to download Excel for {refineryModel.RefineryCode}. Status Code: {response.StatusCode}, Reason: {response.Content.ReadAsStringAsync().Result}");
                    return;
                }

                var stream = await response.Content.ReadAsStreamAsync();
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                var base64String = Convert.ToBase64String(memoryStream.ToArray());
                var fileName = refineryModel.DomainNamespace.DestinationApplication.Name + PlanNSchedConstant.PlanningFile;
                await JsRuntime.InvokeVoidAsync("saveAsFile", base64String, fileName, PlanNSchedConstant.ExcelDownloadContentType);
            }
            catch (Exception ex)
            {
                Logger.LogErrorAndNotify(PopupService, ex);
            }
            finally
            {
                UnlockLoading();
                Logger.LogMethodEnd();
            }
        }

        private async Task StartNewPlanAsync()
        {
            if (DomainNamespaceId == 0) return;

            NavigateToPeriod(SelectedRole, DomainNamespaceId, Area);
        }

        private void ViewPlan(RefineryModel refinery) => NavigateToRefineryPremiseProduct(SelectedRole, refinery.BusinessCase.Id);

        private static string ViewPPIMSPermissionName(string refinery) => $"View{refinery}PPIMS";

        private static string EditPPIMSPermissionName(string refinery) => $"Edit{refinery}PPIMS";
    }
}
