using System.ComponentModel;
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
    public partial class RefineryPremise
    {
        [Parameter]
        public string SelectedRole { get; set; }
        [Inject]
        public ILogger<RefineryPremise> Logger { get; set; } = default!;
        public const string OnConstraintChanged = "OnConstraintChangedAsync";
        public MPC.PlanSched.Shared.Service.Schema.BusinessCase BusinessCase { get; set; } = new();
        public List<string> RefineryPremiseConstraints { get; set; } = [];
        private string _selectedConstraint = string.Empty;
        private int _selectedPeriodId = 0;
        private bool _isParentInitialized = false;
        private bool _isFirstLoad = true;
        public const string GetPeriods = "GetPeriodsAsync";

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                LockLoading();
                StateHasChanged();
                Client = ClientFactory.CreateClient("WebAPI");
                Client.Timeout = TimeSpan.FromSeconds(600);

                BusinessCase = await UtilityUI.GetBusinessCaseByBusinessCaseIdAsync(BusinessCaseId, SessionService.GetCorrelationId(), Client);
                if (BusinessCase != null)
                {
                    PlanName = BusinessCase.Name;
                    PlanUpdatedOn = "Last Saved: " + (BusinessCase.UpdatedOn?.ToString("MM/dd/yy") ?? "");
                    PlanDescription = BusinessCase.Description;
                    IsHistoricalData = IsHistoricalPlan;
                    ReadOnlyFlag = IsHistoricalData;
                }
                RefineryModel = await UtilityUI.GetRefineryModelByBusinessCaseIdAsync(BusinessCaseId, SessionService.GetCorrelationId(), Client);
                _selectedConstraint = RefineryPremiseConstraintType.Caps.Description();

                await GetConstraintsAsync();
                await GetPeriodsAsync();
                _isParentInitialized = true;
                _isFirstLoad = true;
                UnlockLoading();
                StateHasChanged();
            }
            await base.OnAfterRenderAsync(firstRender);
        }

        public async Task GetConstraintsAsync()
        {
            RefineryPremiseConstraints = Enum.GetValues(typeof(RefineryPremiseConstraintType))
                .Cast<RefineryPremiseConstraintType>()
                .Select(e =>
                {
                    var fi = typeof(RefineryPremiseConstraintType).GetField(e.ToString());
                    var descriptionAttribute = fi?.GetCustomAttributes(typeof(DescriptionAttribute), false)
                    .FirstOrDefault() as DescriptionAttribute;
                    return descriptionAttribute?.Description ?? e.ToString();
                })
                .ToList();
            _selectedConstraint = RefineryPremiseConstraints[0];
        }

        public async Task OnConstraintChangedAsync(string selectedConstraint)
        {
            _selectedConstraint = selectedConstraint;
            _isFirstLoad = false;
        }

        public async Task OnRefineryPremisePeriodSelectionChangedAsync(int periodId)
        {
            SelectedPeriodId = periodId;
            _selectedPeriodId = periodId;
            _isFirstLoad = false;
        }

        public async Task<List<RequestedPeriodModel>> GetPeriodsAsync()
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
                    Periods = RequestedPeriods.Where(x => x.PeriodName != PlanNSchedConstant.LastPeriodOCostName).ToList();
                    _selectedPeriodId = Periods[0].PeriodID;
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

        public string GetCompositeKey(string selectedConstraint, int selectedPeriodId) =>
            $"{selectedConstraint}_{selectedPeriodId}";
    }
}
