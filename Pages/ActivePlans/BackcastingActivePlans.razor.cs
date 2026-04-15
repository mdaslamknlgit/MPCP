using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MPC.PlanSched.Model;
using MPC.PlanSched.Service;
using MPC.PlanSched.Shared.Common;
using MPC.PlanSched.Shared.Common.Extensions;
using MPC.PlanSched.UI.Shared;
using BusinessCase = MPC.PlanSched.Shared.Service.Schema.BusinessCase;

namespace MPC.PlanSched.UI.Pages.ActivePlans
{
    [Authorize(Policy = PlanNSchedConstant.ViewPimsBackcasting)]
    public partial class BackcastingActivePlans
    {
        [Parameter]
        public string SelectedRole { get; set; } = string.Empty;
        public int DomainNamespaceId { get; set; }
        public int BusinessCaseId { get; set; }
        [Inject]
        public ILogger<BackcastingActivePlans> Logger { get; set; } = default!;

        private ApplicationArea Area => ApplicationArea.regionalbackcasting;
        private ExcelDownloadDialog? _excelDialogRef = default!;
        private Dialog _archivedialog = default!;
        private RefreshDialog _refreshModal = default!;
        private BusinessCase? _selectedBusinessCase;
        private List<RegionModel> _regions = [];
        private string _localTimezone = string.Empty;
        private string _localTimeZoneName = PlanNSchedConstant.DefaultTimeZone;
        private const string _getRegions = "GetRegions";
        public IEnumerable<BusinessCase>? BusinessCases { get; set; } = null;
        private const string _archivePlan = "ArchivePlan";
        private bool IsLoading { get; set; } = false;

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
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

        private Task OnPriceTypeChanged(PriceType newPriceType) =>
            InvokeAsync(() => SelectedPriceType = newPriceType);

        public async Task ExcelDownloadAsync(BusinessCase businessCase)
        {
            if (businessCase is null) return;
            try
            {
                businessCase.PriceType = SelectedPriceType.Description();
                Logger.LogMethodStart();
                LockLoading();
                _excelDialogRef.Close();
                var region = CreateRegion(businessCase);
                region.PriceType = SelectedPriceType.Description();
                region.ApplicationState = Service.Model.State.Actual.Description();
                var data = await _excelCommon.GetExcelBase64ByRegion(region, Area);
                var fileName = region?.DomainNamespace?.DestinationApplication.Name + PlanNSchedConstant.BackcastingPlanningFile;
                await JsRuntime.InvokeVoidAsync("saveAsFile", data, fileName, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            }
            catch (Exception ex)
            {
                ex.Data.Add("Area", Area.AppDescription());
                ex.Data.Add("BusinessCaseId", businessCase.Id);
                ex.Data.Add("Region", businessCase.Region);
                ex.Data.Add("PriceType", SelectedPriceType.Description());
                ex.Data.Add("ApplicationState", Service.Model.State.Actual.Description());
                Logger.LogErrorAndNotify(PopupService, ex, "Error occurred in Backcasting ExcelDownload method.");
            }
            finally
            {
                UnlockLoading();
                Logger.LogMethodEnd();
            }
        }

        public async Task ArchivePlan()
        {
            Logger.LogMethodStart();
            _archivedialog.Close();
            LockLoading();
            try
            {
                IsLoading = true;
                StateHasChanged();
                await UtilityUI.ArchivePlanAsync(BusinessCaseId, Client);
                await GetRegionsAsync();
            }
            catch (Exception ex)
            {
                Logger.LogErrorAndNotify(PopupService, ex, "Error occurred in " + _archivePlan + " method.");
            }
            finally
            {
                IsLoading = false;
                UnlockLoading();
                StateHasChanged();
                Logger.LogMethodEnd();
            }
        }

        private async Task GetRegionsAsync()
        {
            Logger.LogMethodInfo("Start of " + _getRegions + ".");

            try
            {
                LockLoading();

                _regions = await UtilityUI.GetRegionListAsync(ActiveUser, PlanNSchedConstant.PIMS, Client,
                    _localTimeZoneName, SessionService.GetCorrelationId(), false, true);

                BusinessCases = _regions?.SelectMany(x =>
                {
                    var businessCasesForRegion = x.BackcastingBusinessCases?.Select(businessCase =>
                    {
                        businessCase.RefineryName = businessCase.RefineryName?.Replace(PlanNSchedConstant.TerminalSuffix, string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
                        return businessCase;
                    }).ToList() ?? new List<BusinessCase>();

                    businessCasesForRegion.Add(new BusinessCase
                    {
                        Id = 0,
                        Region = x.RegionName,
                        RefineryName = x.Refinery?.Replace(PlanNSchedConstant.TerminalSuffix, string.Empty, StringComparison.OrdinalIgnoreCase).Trim(),
                        CreatedOn = null,
                        UpdatedOn = null,
                    });

                    return businessCasesForRegion;
                }).ToList();

                ConvertUTCDateToLocal();
            }
            catch (Exception ex)
            {
                ex.Data.Add("Area", Area.AppDescription());
                Logger.LogErrorAndNotify(PopupService, ex);
            }
            finally
            {
                UnlockLoading();
                Logger.LogMethodEnd("End of " + _getRegions + ".");
            }
        }

        private void EditPlan(BusinessCase businessCase)
        {
            var region = CreateRegion(businessCase);
            CommonHelper.UpdateBaggageWOPeriod(SessionService.GetCorrelationId(),
               region.BusinessCase?.Id,
               region.DomainNamespace?.DestinationApplication.Name);
            LockLoading();
            region.CorrelationId = SessionService?.GetCorrelationId();
            var businessCaseId = region.BusinessCase.Id;
            NavigateToBackcastingProduct(SelectedRole, businessCaseId);
        }

        private void StartNewPlan(BusinessCase businessCase)
        {
            DomainNamespaceId = _regions.FirstOrDefault(x => x.RegionName == businessCase.Region)?.DomainNamespace?.DestinationApplication?.Id ?? DomainNamespaceId;
            if (DomainNamespaceId == 0) return;

            NavigateToPeriod(SelectedRole, DomainNamespaceId, Area);
        }

        private void RefreshModel(BusinessCase businessCase)
        {
            if (businessCase == null) return;
            _refreshModal.OpenAsync(CreateRegion(businessCase));
        }

        private RegionModel CreateRegion(BusinessCase businessCase)
        {
            var regionModel = _regions.Find(x => x.RegionName == businessCase.Region);
            regionModel.BusinessCase = businessCase;
            regionModel.IsHistoricalPlan = false;
            return regionModel;
        }
        private void ConvertUTCDateToLocal()
        {
            DateTime UTC_CreatedOn, UTC_UpdatedOn;
            foreach (var _businessCaseObj in BusinessCases)
            {
                if (_businessCaseObj.CreatedOn != null && _businessCaseObj.UpdatedOn != null)
                {
                    UTC_CreatedOn = DateTime.Parse(Convert.ToString(_businessCaseObj.CreatedOn));
                    UTC_UpdatedOn = DateTime.Parse(Convert.ToString(_businessCaseObj.UpdatedOn));
                    if (_localTimeZoneName == PlanNSchedConstant.DefaultTimeZone)
                    {
                        _businessCaseObj.CreatedOn = UTC_CreatedOn.ToLocalTime();
                        _businessCaseObj.UpdatedOn = UTC_UpdatedOn.ToLocalTime();
                    }
                    else
                    {
                        var cstZone = TimeZoneInfo.FindSystemTimeZoneById(_localTimeZoneName);
                        _businessCaseObj.CreatedOn = TimeZoneInfo.ConvertTimeFromUtc(UTC_CreatedOn, cstZone);
                        _businessCaseObj.UpdatedOn = TimeZoneInfo.ConvertTimeFromUtc(UTC_UpdatedOn, cstZone);
                    }
                }
            }
        }

        private string EditPermissionName(string region) => $"Edit{region}{Area.AppDescription()}{Constant.Backcasting}";
        private string ViewPermissionName(string region) => $"View{region}{Area.AppDescription()}{Constant.Backcasting}";
    }
}