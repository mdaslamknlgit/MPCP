using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MPC.PlanSched.Model;
using MPC.PlanSched.Service;
using MPC.PlanSched.Shared.Common;
using MPC.PlanSched.Shared.Common.Extensions;
using MPC.PlanSched.Shared.Service.Schema;
using MPC.PlanSched.UI.Shared;

namespace MPC.PlanSched.UI.Pages.ActivePlans
{
    public partial class ActivePlans
    {
        [Parameter]
        public string SelectedRole { get; set; } = string.Empty;
        [Parameter]
        public string AreaValue { get; set; } = default!;
        [Inject]
        public ILogger<ActivePlans> Logger { get; set; } = default!;
        public int DomainNamespaceId { get; set; }
        public string PlanType { get; set; } = string.Empty;
        private ApplicationArea Area => AreaValue.Parse<ApplicationArea>();
        private Dialog _dialog = default!;
        private Dialog _planTypeDialog = default!;
        private RefreshDialog _refreshModal = default!;
        private List<RegionModel> _regions = [];
        private BusinessCase _selectedBusinessCase = new BusinessCase();
        private string _localTimezone = string.Empty;
        private string _localTimeZoneName = PlanNSchedConstant.DefaultTimeZone;
        private const string _getRegions = "GetRegions";
        private readonly bool _isMidtermEnabled = ConfigurationUI.IsMidtermEnabled;

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                if (_isMidtermEnabled)
                {
                    return;
                }
                LockLoading();
                StateHasChanged();
                CommonHelper.UpdateBaggage(SessionService.GetCorrelationId());
                _localTimeZoneName = SessionService.GetLocalTimezoneName();
                _localTimezone = "Last Saved Plan (" + _localTimeZoneName + ")";
                SessionService.SetPlanType("ActivePlan");
                Client = ClientFactory.CreateClient("WebAPI");
                Client.Timeout = TimeSpan.FromSeconds(600);
                await GetRegionsAsync();
                UnlockLoading();
                StateHasChanged();
            }
            await base.OnAfterRenderAsync(firstRender);
        }

        protected virtual async Task GetRegionsAsync()
        {
            Logger.LogMethodInfo("Start of " + _getRegions + ".");
            LockLoading();
            try
            {
                var domainNamespaceType = Area == ApplicationArea.distributionplanning ? PlanNSchedConstant.DPO : PlanNSchedConstant.PIMS;
                _regions = await UtilityUI.GetRegionListAsync(ActiveUser, domainNamespaceType, Client,
                    _localTimeZoneName, SessionService.GetCorrelationId());
                if (_regions.IsCollectionNullOrEmpty())
                    Logger.LogWarningAndNotify(PopupService, "Database result Empty. No active plans found.");
                foreach (var region in _regions)
                {
                    if (region.BusinessCase != null && region.PlanTypes != null)
                    {
                        region.PlanType = region.BusinessCase.PlanTypeId == null
                        ? region.PlanTypes.FirstOrDefault(pt => pt.IsDefault)?.Name ?? string.Empty
                        : region.PlanTypes
                        .FirstOrDefault(pt => pt.Id == region.BusinessCase.PlanTypeId)?.Name ?? string.Empty;

                        region.PlanTypeDescription = region.BusinessCase.PlanTypeId == null
                        ? region.PlanTypes.FirstOrDefault(pt => pt.IsDefault)?.Description ?? string.Empty
                        : region.PlanTypes
                        .FirstOrDefault(pt => pt.Id == region.BusinessCase.PlanTypeId)?.Description ?? string.Empty;
                    }
                }
                UnlockLoading();
                Logger.LogMethodInfo("End of " + _getRegions + ".");
            }
            catch (Exception ex)
            {
                ex.Data.Add("Area", Area.AppDescription());
                UnlockLoading();
                Logger.LogErrorAndNotify(PopupService, ex);
            }
        }

        private void EditPlan(RegionModel region)
        {
            CommonHelper.UpdateBaggageWOPeriod(SessionService.GetCorrelationId(),
               region.BusinessCase?.Id,
               region.DomainNamespace?.DestinationApplication.Name);
            LockLoading();
            region.CorrelationId = SessionService?.GetCorrelationId();
            var businessCaseId = region.BusinessCase.Id;
            if (Area == ApplicationArea.distributionplanning)
            {
                NavigateToProductDP(SelectedRole, businessCaseId);
            }
            else
            {
                NavigateToProductRP(SelectedRole, businessCaseId);
            }
        }

        public async Task ExcelDownloadAsync(RegionModel region)
        {
            try
            {
                Logger.LogMethodStart();
                LockLoading();
                var data = await _excelCommon.GetExcelBase64ByRegion(region, Area);
                var fileName = region?.DomainNamespace?.DestinationApplication.Name + "_Planning.xlsx";
                await JsRuntime.InvokeVoidAsync("saveAsFile", data, fileName, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            }
            catch (Exception ex)
            {
                ex.Data.Add("RegionId", region?.Id ?? 0);
                ex.Data.Add("Area", Area.AppDescription());
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

        private async Task FlagPlanTypeAsync(BusinessCase businessCase)
        {
            if (_planTypeDialog.IsVisible) _planTypeDialog.Close();
            try
            {
                LockLoading();
                var currentUser = await ActiveUser.GetNameAsync() ?? businessCase.CreatedBy;
                businessCase.UpdatedBy = currentUser;
                businessCase.PlanTypeId = _regions[0]?.PlanTypes?.FirstOrDefault(pt => pt.Name == businessCase.PlanType)?.Id;
                await UtilityUI.FlagPlanTypeAsync(businessCase, Client);
            }
            catch (Exception ex)
            {
                Logger.LogErrorAndNotify(PopupService, ex);
            }
            finally
            {
                UnlockLoading();
                StateHasChanged();
            }
        }

        private static string EditPermissionName(string region, string area) => $"Edit{region}{area}";
        private static string ViewPermissionName(string region, string area) => $"View{region}{area}";
    }
}