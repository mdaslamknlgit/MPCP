using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MPC.PlanSched.Model;
using MPC.PlanSched.ORM.Models;
using MPC.PlanSched.Service;
using MPC.PlanSched.Shared.Common;
using MPC.PlanSched.Shared.Common.Extensions;
using MPC.PlanSched.Shared.Service.Schema;
using MPC.PlanSched.UI.Pages.ActivePlans;
using MPC.PlanSched.UI.Services;
using MPC.PlanSched.UI.Shared;
using MPC.PlanSched.UI.ViewModel;
using BusinessCase = MPC.PlanSched.Shared.Service.Schema.BusinessCase;

namespace MPC.PlanSched.UI.Pages.SharePlans
{
    public partial class SharePlans
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
        private BusinessCase? _selectedBusinessCase;
        private BusinessCase? _newPlanBusinessCase;
        private List<RegionModel> _regions = [];
        private string _localTimezone = string.Empty;
        private string _localTimeZoneName = PlanNSchedConstant.DefaultTimeZone;
        private const string _getRegions = "GetRegions";
        private const string _archivePlan = "ArchivePlan";
        private Dialog _createFromSharedDialog = default!;

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
                    }).Where(bc => bc.IsShared == true)
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

        private void ViewPlan(BusinessCase businessCaseObj)
        {
            LockLoading();
            var regionObj = CreateRegion(businessCaseObj);
            if (regionObj?.BusinessCase == null)
            {
                UnlockLoading();
                logger.LogMethodError(new InvalidOperationException("Region or BusinessCase not found"), "Error in ViewPlan method.");
                return;
            }
            SessionService.SetPlanType("SharedPlan");
            CommonHelper.UpdateBaggageWOPeriod(SessionService.GetCorrelationId(),
               regionObj.BusinessCase.Id,
               regionObj.DomainNamespace?.DestinationApplication.Name);
            regionObj.CorrelationId = SessionService.GetCorrelationId();
            var businessCaseId = regionObj.BusinessCase.Id;
            if (Area == ApplicationArea.distributionplanning)
            {
                NavigateToProductDP(SelectedRole, businessCaseId, isHistoricalPlan: true);
            }
            else
            {
                NavigateToProductRP(SelectedRole, businessCaseId, isHistoricalPlan: true);
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

        public async Task SharePlanAsync(BusinessCase businessCase)
        {
            if (businessCase is null)
            {
                return;
            }

            var originalValue = businessCase.IsShared ?? false;
            var newValue = !originalValue;
            businessCase.IsShared = newValue;

            try
            {
                StateHasChanged();
                await UtilityUI.SharePlanAsync(businessCase.Id, newValue, Client);
                StatusMessageContent = "Plan shared successfully";
                StatusPopup = true;
            }
            catch (Exception ex)
            {
                Logger.LogErrorAndNotify(PopupService, ex, "Error occurred while sharing plan.");
            }
            finally
            {
                UnlockLoading();
            }
        }

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
                var url =
                    $"api/Plan/UpdateBusinessCaseFlag?correlationId={Uri.EscapeDataString(correlationId)}&businessCaseId={businessCase.Id}";

                var response = await Client.PutAsJsonAsync(url, newValue);
                response.EnsureSuccessStatusCode();

                StatusMessageContent = newValue ? "Plan flagged" : "Flag removed";
                StatusPopup = true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error toggling flag for BusinessCase {Id}", businessCase.Id);

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

        public void EditPlan(BusinessCase businessCase)
        {
            if (businessCase is null)
            {
                Logger.LogMethodError(new ArgumentNullException(nameof(businessCase)), "BusinessCase cannot be null in EditPlan.");
                return;
            }

            try
            {
                Logger.LogMethodStart();
                LockLoading();

                var region = CreateRegion(businessCase);
                if (region?.BusinessCase == null)
                {
                    UnlockLoading();
                    Logger.LogMethodError(new InvalidOperationException("Region or BusinessCase not found"), "Error in EditPlan method.");
                    return;
                }

                // Set the business case to be edited
                _selectedBusinessCase = businessCase;

                // Open the dialog to create a new plan from this shared plan
                _createFromSharedDialog?.Open();

                Logger.LogMethodEnd();
            }
            catch (Exception ex)
            {
                Logger.LogErrorAndNotify(PopupService, ex, "Error occurred in EditPlan method.");
            }
            finally
            {
                UnlockLoading();
                StateHasChanged();
            }
        }

        public async Task CreatePlanFromSharedAsync()
        {
            try
            {
                Logger.LogMethodStart();
                _createFromSharedDialog?.Close();
                LockLoading();
                StateHasChanged();

                var userName = await ActiveUser.GetNameAsync();
                var correlationId = SessionService.GetCorrelationId();
                var createdBusinessCaseId = await UtilityUI.CreateFromSharedAsync(
                    _selectedBusinessCase.Id,
                    correlationId,
                    userName,
                    Client);


                if (createdBusinessCaseId)
                {
                    _createFromSharedDialog?.Close();

                    StatusMessageContent = "Plan created from shared plan successfully";
                    StatusPopup = true;
                    await GetRegionsAsync();
                    _selectedBusinessCase = null;
                }
                else
                {
                    StatusMessageContent = "Failed to create plan from shared plan. Please try again.";
                    StatusPopup = true;
                }

                Logger.LogMethodEnd();
            }
            catch (Exception ex)
            {
                StatusMessageContent = "Failed to create plan from shared plan.";
                Logger.LogErrorAndNotify(PopupService, ex, "Error occurred in CreatePlanFromSharedAsync method.");
                StatusPopup = true;
            }
            finally
            {
                UnlockLoading();
                StateHasChanged();
            }
        }
    }
}

