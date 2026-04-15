using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MPC.PlanSched.Model;
using MPC.PlanSched.Service;
using MPC.PlanSched.Shared.Common;
using MPC.PlanSched.Shared.Common.Extensions;
using MPC.PlanSched.UI.ViewModel;
using Telerik.Blazor.Components;

namespace MPC.PlanSched.UI.Pages.HistoricalPlans
{
    public partial class RefineryPremiseHistoricalPlans
    {
        [Parameter]
        public string SelectedRole { get; set; }
        [Inject]
        ISessionService SessionService { get; set; }
        [Inject]
        ILogger<RefineryPremiseHistoricalPlans> Logger { get; set; }
        public IEnumerable<RefineryModel>? RefineriesList { get; set; } = null;
        private string LocalTimeZoneName { get; set; } = string.Empty;
        public TelerikGrid<RefineryModel> HistoricalGridRef { get; set; }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                LockLoading();
                StateHasChanged();
                CommonHelper.UpdateBaggage(SessionService.GetCorrelationId());
                LocalTimeZoneName = SessionService.GetLocalTimezoneName();
                if (LocalTimeZoneName == string.Empty)
                {
                    LocalTimeZoneName = PlanNSchedConstant.DefaultTimeZone;
                }

                SessionService.SetPlanType("HistoricalPlansRefineryPremise");
                Client = ClientFactory.CreateClient("WebAPI");
                Client.Timeout = TimeSpan.FromSeconds(600);

                await GetRefineriesAsync();
                UnlockLoading();
                StateHasChanged();
            }
            await base.OnAfterRenderAsync(firstRender);
        }

        private async Task GetRefineriesAsync()
        {
            Logger.LogMethodInfo("Start of GetRefineries");
            LockLoading();
            try
            {
                RefineriesList = await UtilityUI.GetAllRefineryPremiseHistoricalPlansAsync(Client, LocalTimeZoneName, SessionService.GetCorrelationId(), ActiveUser);
                UnlockLoading();
                Logger.LogMethodInfo("End of GetRefineriesAsync");
            }
            catch (Exception ex)
            {
                UnlockLoading();
                Logger.LogMethodError(ex, "Error occurred in GetRefineries method.");
            }
        }

        private void ViewPlan(RefineryModel refinery) => NavigateToRefineryPremiseProduct(SelectedRole, refinery.BusinessCase.Id, true);

        private async Task ExcelDownloadAsync(RefineryModel refineryModel)
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
                Logger.LogMethodError(ex);
            }
            finally
            {
                UnlockLoading();
                Logger.LogMethodEnd();
            }
        }
    }
}
