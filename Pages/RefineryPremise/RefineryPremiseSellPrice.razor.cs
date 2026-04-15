using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MPC.PlanSched.Model;
using MPC.PlanSched.Service;
using MPC.PlanSched.Shared.Common;
using MPC.PlanSched.Shared.Common.Extensions;
using MPC.PlanSched.Shared.Service.Schema;
using Newtonsoft.Json;

namespace MPC.PlanSched.UI.Pages.RefineryPremise
{
    [Authorize(Policy = PlanNSchedConstant.ViewPPims)]
    public partial class RefineryPremiseSellPrice
    {
        [Inject]
        public new ILogger<RefineryPremiseSellPrice> Logger { get; set; } = default!;

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                LockLoading();
                StateHasChanged();
                IsAggregatedTier = false;
                Client = ClientFactory.CreateClient(PlanNSchedConstant.WebAPI);
                Client.Timeout = TimeSpan.FromSeconds(PlanNSchedConstant.Timeout);
                BusinessCase = await UtilityUI.GetBusinessCaseByBusinessCaseIdAsync(BusinessCaseId, SessionService.GetCorrelationId(), Client);
                if (BusinessCase != null)
                {
                    RegionTitle = BusinessCase.Name;
                    PlanUpdatedOn = PlanNSchedConstant.LastSaved + (BusinessCase.UpdatedOn?.ToString(PlanNSchedConstant.DateFormatMMDDYY) ?? "");
                    PlanDescription = BusinessCase.Description;
                    ReadOnlyFlag = IsHistoricalData = IsHistoricalPlan;
                }
                RefineryModel = await UtilityUI.GetRefineryModelByBusinessCaseIdAsync(BusinessCaseId, SessionService.GetCorrelationId(), Client);

                await GetPeriodsForPPIMSAsync();

                CommonHelper.UpdateBaggage(SessionService.GetCorrelationId(),
                    RegionModel?.BusinessCase?.Id,
                    RequestedPeriods?.FirstOrDefault()?.PeriodID,
                    RegionModel?.DomainNamespace?.DestinationApplication.Name);

                await GetDemandAndPriceDataFromPublisherAsync();

                if (RegionName != null && !RegionName.Contains(Constant.SouthRegion))
                {
                    IsTierVisible = true;
                }
                UnlockLoading();
                StateHasChanged();
            }
            await base.OnAfterRenderAsync(firstRender);
        }

        public async Task<List<RequestedPeriodModel>> GetPeriodsForPPIMSAsync()
        {
            try
            {
                Logger.LogMethodStart();
                LockLoading();
                StateHasChanged();

                RequestedPeriods = new List<RequestedPeriodModel>();
                Periods = new List<RequestedPeriodModel>();
                UserName = await ActiveUser.GetNameAsync();
                RequestedPeriods = await UtilityUI.GetPeriodDataByBusinessCaseId(BusinessCaseId, SessionService.GetCorrelationId(), Client, UserName);
                if (RequestedPeriods.Any())
                {
                    RequestedPeriods.ForEach(d => d.DomainNamespace.SourceApplication.Name = PlanNSchedConstant.Application_CIP);
                    RequestedPeriods.ForEach(d => d.RegionName = RefineryModel.RegionName);
                    Periods = RequestedPeriods.Where(x => x.PeriodName != PlanNSchedConstant.LastPeriodOCostName).ToList();
                    SelectedPeriodId = Periods[0].PeriodID;
                    PriceEffectiveDate = RequestedPeriods[0].PriceEffectiveDate?.ToString(Constant.DateFormat);
                }
                UnlockLoading();
                StateHasChanged();
                Logger.LogMethodEnd();
                return Periods;
            }
            catch (JsonSerializationException exSz)
            {
                UnlockLoading();
                StateHasChanged();
                Logger.LogMethodError(exSz, Constant.SerializationException + " occurred in " + GetPeriods + " UI method.");
                return Periods;
            }
            catch (Exception ex)
            {
                UnlockLoading();
                StateHasChanged();
                Logger.LogMethodError(ex, "Error occurred in " + GetPeriods + " method.");
                return Periods;
            }
        }
        public async Task ExcelDownloadAsync()
        {
            try
            {
                Logger.LogMethodStart();
                LockLoading();
                var response = await Client.PostAsJsonAsync(ConfigurationUI.GetExcelPlanByRefinery, RefineryModel);
                if (!response.IsSuccessStatusCode)
                {
                    var content = response.Content.ReadAsStringAsync().Result;
                    Logger.LogMethodInfo("GetExcelPlanByRefinery api does not return success code. Details:" + content);
                    return;
                }

                var stream = await response.Content.ReadAsStreamAsync();
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                var base64String = Convert.ToBase64String(memoryStream.ToArray());
                var fileName = RefineryModel.DomainNamespace.DestinationApplication.Name + PlanNSchedConstant.PlanningFile;
                await JsRuntime.InvokeVoidAsync(PlanNSchedConstant.ExcelDownloadJavascriptFunction, base64String, fileName, PlanNSchedConstant.ExcelDownloadContentType);
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
