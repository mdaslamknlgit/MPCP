using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MPC.PlanSched.Model;
using MPC.PlanSched.Service;
using MPC.PlanSched.Shared.Common;
using MPC.PlanSched.Shared.Common.Extensions;
using MPC.PlanSched.UI.Shared;
using Telerik.SvgIcons;
using static MPC.PlanSched.UI.UtilityUI;
using BusinessCase = MPC.PlanSched.Shared.Service.Schema.BusinessCase;

namespace MPC.PlanSched.UI.Pages.ActivePlans
{
    public partial class MidtermActivePlans
    {
        [Parameter]
        public string SelectedRole { get; set; } = string.Empty;
        [Parameter]
        public string AreaValue { get; set; } = default!;

        public int DomainNamespaceId { get; set; }
        public int BusinessCaseId { get; set; }

        [Inject]
        public ILogger<MidtermActivePlans> Logger { get; set; } = default!;


        private ApplicationArea Area => AreaValue.Parse<ApplicationArea>();
        private ExcelDownloadDialog? _excelDialogRef = default!;
        private RefreshDialog _refreshModal = default!;
        private Dialog _archivedialog = default!;
        private Dialog _deletedialog = default!;
        private Dialog _sharePlanDialog = default!;
        private BusinessCase? _selectedBusinessCase;
        private BusinessCase? _newPlanBusinessCase;
        private List<RegionModel> _regions = [];
        private string _localTimezone = string.Empty;
        private string _localTimeZoneName = PlanNSchedConstant.DefaultTimeZone;
        private const string _getRegions = "GetRegions";
        private const string _archivePlan = "ArchivePlan";

        public IEnumerable<BusinessCase>? BusinessCases { get; set; } = null;

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                NavigationDrawer?.SetExpanded(true);

                LockLoading();
                StateHasChanged();
                CommonHelper.UpdateBaggage(SessionService.GetCorrelationId());
                _localTimeZoneName = SessionService.GetLocalTimezoneName();
                _localTimezone = "Last Saved Plan (" + _localTimeZoneName + ")";
                SessionService.SetPlanType("MidtermPlan");
                Client = ClientFactory.CreateClient("WebAPI");
                Client.Timeout = TimeSpan.FromSeconds(600);
                await GetRegionsAsync();
                _newPlanBusinessCase = new BusinessCase
                {
                    Region = ConfigurationUI.MidtermRegion
                };
                UnlockLoading();
                StateHasChanged();
            }
            await Task.CompletedTask;
        }

        private Task OnPriceTypeChanged(PriceType newPriceType) =>
            Task.Run(() => SelectedPriceType = newPriceType);

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
                region.ApplicationState = Service.Model.State.Forecast.Description();

                var data = await _excelCommon.GetExcelBase64ByRegion(region, Area);
                var fileName = region?.DomainNamespace?.DestinationApplication.Name + "_MidtermPlan.xlsx";

                await JsRuntime.InvokeVoidAsync("saveAsFile", data, fileName,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            }
            catch (Exception ex)
            {
                ex.Data.Add("Area", Area.AppDescription());
                ex.Data.Add("BusinessCaseId", businessCase.Id);
                ex.Data.Add("Region", businessCase.Region);
                ex.Data.Add("PriceType", SelectedPriceType.Description());
                Logger.LogErrorAndNotify(PopupService, ex, "Error occurred in Midterm ExcelDownload method.");
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
                var domainNamespaceType = Area == ApplicationArea.distributionplanning ? PlanNSchedConstant.DPO : PlanNSchedConstant.PIMS;
                _regions = await UtilityUI.GetRegionListAsync(
                    ActiveUser,
                    domainNamespaceType,
                    Client,
                    _localTimeZoneName,
                    SessionService.GetCorrelationId(),
                    includeHistoricalPlanList: false,
                    isActualData: false
                );

                var currentUserName = await ActiveUser.GetNameAsync();
                _regions = _regions.Where(x => x.RegionName == ConfigurationUI.MidtermRegion).ToList();

                BusinessCases = _regions?.SelectMany(x =>
                {
                    var businessCasesForRegion = x.ActiveBusinessCases?.Select(businessCase =>
                    {
                        businessCase.RefineryName = x.Refinery?.Trim();
                        return businessCase;
                    }).Where(bc => bc.CreatedBy == currentUserName)
                    .ToList() ?? new List<BusinessCase>();

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
            if (Area == ApplicationArea.distributionplanning)
            {
                NavigateToProductDP(SelectedRole, businessCaseId);
            }
            else
            {
                NavigateToProductRP(SelectedRole, businessCaseId);
            }
        }

        private async Task DeletePlanAsync()
        {
            Logger.LogMethodStart();
            _deletedialog.Close();
            LockLoading();
            try
            {
                StateHasChanged();
                await UtilityUI.DeletePlanAsync(BusinessCaseId, Client);
                await GetRegionsAsync();
                StatusMessageContent = "Plan deleted. Transactional data removed; plan moved to Historical.";
                StatusPopup = true;
            }
            catch (Exception ex)
            {
                Logger.LogErrorAndNotify(PopupService, ex, "Error occurred in DeletePlanAsync method.");
            }
            finally
            {
                UnlockLoading();
                StateHasChanged();
                Logger.LogMethodEnd();
            }
        }

        public async Task SharePlanAsync(BusinessCase businessCase)
        {
            Logger.LogMethodStart();

            if (businessCase is null)
            {
                Logger.LogMethodEnd();
                return;
            }

            _sharePlanDialog.Close();
            StateHasChanged();
            LockLoading();

            var originalValue = businessCase.IsShared ?? false;
            var newValue = !originalValue;
            businessCase.IsShared = newValue;

            try
            {
                StateHasChanged();
                await UtilityUI.SharePlanAsync(businessCase.Id, newValue, Client);
                StatusMessageContent = newValue ? "Plan shared successfully" : "Plan removed from Shared Plans";
                StatusPopup = true;
            }
            catch (Exception ex)
            {
                businessCase.IsShared = originalValue;
                Logger.LogErrorAndNotify(PopupService, ex, "Error occurred while sharing plan.");
                StatusMessageContent = "Failed to update plan flag. Please try again.";
                StatusPopup = true;
            }
            finally
            {
                UnlockLoading();
                StateHasChanged();
                Logger.LogMethodEnd();
            }
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

        private async Task PublishPlan(BusinessCase businessCase)
        {
            try
            {
                LockLoading();
                Logger.LogInformation($"Publishing midterm plan: {businessCase.Id}");
                StatusMessageContent = "Plan published successfully.";
                StatusPopup = true;
            }
            catch (Exception ex)
            {
                Logger.LogErrorAndNotify(PopupService, ex, "Error occurred while publishing plan.");
            }
            finally
            {
                UnlockLoading();
            }
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
            foreach (var businessCaseObj in BusinessCases)
            {
                if (businessCaseObj.CreatedOn != null && businessCaseObj.UpdatedOn != null)
                {
                    var utcCreatedOn = DateTime.Parse(Convert.ToString(businessCaseObj.CreatedOn));
                    var utcUpdatedOn = DateTime.Parse(Convert.ToString(businessCaseObj.UpdatedOn));

                    if (_localTimeZoneName == PlanNSchedConstant.DefaultTimeZone)
                    {
                        businessCaseObj.CreatedOn = utcCreatedOn.ToLocalTime();
                        businessCaseObj.UpdatedOn = utcUpdatedOn.ToLocalTime();
                    }
                    else
                    {
                        var cstZone = TimeZoneInfo.FindSystemTimeZoneById(_localTimeZoneName);
                        businessCaseObj.CreatedOn = TimeZoneInfo.ConvertTimeFromUtc(utcCreatedOn, cstZone);
                        businessCaseObj.UpdatedOn = TimeZoneInfo.ConvertTimeFromUtc(utcUpdatedOn, cstZone);
                    }
                }
            }
        }

        private string EditPermissionName(string region) => $"Edit{region}{Area.AppDescription()}";
        private string ViewPermissionName(string region) => $"View{region}{Area.AppDescription()}";

        public async Task ToggleFlagAsync(BusinessCase businessCase)
        {
            if (businessCase is null)
            {
                return;
            }

            var originalValue = businessCase.IsFlagged ?? false;
            var newValue = !originalValue;
            businessCase.IsFlagged = newValue;

            try
            {
                var correlationId = SessionService.GetCorrelationId();
                var response = await UtilityUI.UpdateBusinessCaseFlagAsync(businessCase.Id, correlationId, Client, newValue);

                if(response)
                {
                    StatusMessageContent = newValue ? "Plan flagged" : "Flag removed";
                    StatusPopup = true;
                }
                else
                {
                    businessCase.IsFlagged = originalValue;
                    StatusMessageContent = "Failed to update plan flag. Please try again.";
                    StatusPopup = true;
                }

            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error toggling flag for BusinessCase {Id}", businessCase.Id);

                // Revert UI if server call fails
                businessCase.IsFlagged = originalValue;

                StatusMessageContent = "Failed to update plan flag. Please try again.";
                StatusPopup = true;
            }
            finally
            {
                StateHasChanged();
            }
        }
    }
}