using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MPC.PlanSched.Model;
using MPC.PlanSched.Service;
using MPC.PlanSched.Shared.Common;
using MPC.PlanSched.Shared.Common.Extensions;
using MPC.PlanSched.Shared.Service.Schema;
using MPC.PlanSched.UI.Shared;
using MPC.PlanSched.UI.ViewModel;
using Telerik.Blazor.Components;

namespace MPC.PlanSched.UI.Pages.HistoricalPlans
{
    [Authorize(Policy = PlanNSchedConstant.ViewDpo)]
    public partial class HistoricalPlansDP
    {
        [Parameter]
        public string SelectedRole { get; set; }
        [Inject]
        ISessionService SessionService { get; set; }
        public TelerikGrid<BusinessCase> HistoricalGridDPRef { get; set; }
        private string CreatedOn { get; set; }
        private string UpdatedOn { get; set; }
        private string LocalTimeZoneName { get; set; } = string.Empty;
        private List<RegionModel> _regionsList = new List<RegionModel>();
        public IEnumerable<BusinessCase>? BusinessCasesList { get; set; } = null;
        private Dialog _deletedialog = default!;
        public int BusinessCaseId { get; set; }
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
                CreatedOn = "Plan Create Date (" + LocalTimeZoneName + ")";
                UpdatedOn = "Last Updated On (" + LocalTimeZoneName + ")";
                SessionService.SetPlanType("HistoricalPlansDP");
                Client = ClientFactory.CreateClient("WebAPI");
                Client.Timeout = TimeSpan.FromSeconds(600);
                await GetRegionsAsync();
                UnlockLoading();
                StateHasChanged();
            }
            await base.OnAfterRenderAsync(firstRender);
        }

        private async Task GetRegionsAsync()
        {
            logger.LogMethodInfo("Start of GetRegions");
            LockLoading();
            try
            {
                _regionsList = await UtilityUI.GetRegionListAsync(ActiveUser, ConfigurationUI.domainNamespaceTypeDP, Client, LocalTimeZoneName, SessionService.GetCorrelationId(), true);
                
                if (ConfigurationUI.IsMidtermEnabled)
                {
                    var currentUserName = await ActiveUser.GetNameAsync();
                    _regionsList = _regionsList.Where(x => x.RegionName == ConfigurationUI.MidtermRegion).ToList();

                    BusinessCasesList = _regionsList.SelectMany(x =>
                    {
                        var businessCasesForRegion = x.ActiveBusinessCases?.Where(bc => bc.CreatedBy == currentUserName).ToList() ?? new List<BusinessCase>();
                        return businessCasesForRegion;
                    }).ToList();
                }
                else
                {
                    BusinessCasesList = _regionsList.SelectMany(x => x.ActiveBusinessCases ?? Enumerable.Empty<BusinessCase>());
                }
                
                BusinessCasesList = ConvertUTCDateToLocal(BusinessCasesList, LocalTimeZoneName);
                UnlockLoading();
                logger.LogMethodInfo("End of GetRegions");
            }
            catch (Exception ex)
            {
                UnlockLoading();
                logger.LogMethodError(ex, "Error occurred in GetRegions method.");
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
            CommonHelper.UpdateBaggageWOPeriod(SessionService.GetCorrelationId(),
               regionObj.BusinessCase.Id,
               regionObj.DomainNamespace?.DestinationApplication.Name);
            regionObj.CorrelationId = SessionService.GetCorrelationId();
            var businessCaseId = regionObj.BusinessCase.Id;
            NavigateToProductDP(SelectedRole, businessCaseId, true);
        }

        private async Task ExcelDownloadAsync(BusinessCase businessCaseObj)
        {
            logger.LogMethodInfo("Start of ExcelDownload");
            try
            {
                LockLoading();
                var regionObj = CreateRegion(businessCaseObj);
                var base64String = await _excelCommon.GetExcelBase64ByRegion(regionObj, ApplicationArea.distributionplanning);
                var fileName = regionObj?.DomainNamespace?.DestinationApplication.Name + "_Planning.xlsx";
                await JsRuntime.InvokeVoidAsync("saveAsFile", base64String, fileName, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
                UnlockLoading();
                logger.LogMethodInfo("End of ExcelDownload");
            }
            catch (Exception ex)
            {
                UnlockLoading();
                logger.LogMethodError(ex, "Error occurred in ExcelDownload method for DPO.");
            }
        }

        private void DeletePlan(BusinessCase businessCaseObj)
        {
            LockLoading();
            var regionObj = CreateRegion(businessCaseObj);
            CommonHelper.UpdateBaggageWOPeriod(SessionService.GetCorrelationId(),
               businessCaseObj.Id,
               string.Empty);
            regionObj.CorrelationId = SessionService.GetCorrelationId();
            var businessCaseId = regionObj.BusinessCase!.Id;
            NavigateToProductDP(SelectedRole, businessCaseId, true);
        }

        private RegionModel CreateRegion(BusinessCase businessCaseObj)
        {
            var regionName = businessCaseObj.Name.Split(' ')[0];
            var regionModelObj = _regionsList.Find(x => x.RegionName.ToUpper() == regionName);
            regionModelObj.ActiveBusinessCases = null;
            regionModelObj.BusinessCase = businessCaseObj;
            regionModelObj.IsHistoricalPlan = true;
            return regionModelObj;
        }

        private IEnumerable<BusinessCase>? ConvertUTCDateToLocal(IEnumerable<BusinessCase>? _businessCasesList, string localTimeZone)
        {
            var cstZone = TimeZoneInfo.FindSystemTimeZoneById(localTimeZone);
            var _updatedBusinessCasesList = new List<BusinessCase>();
            DateTime UTC_CreatedOn, UTC_UpdatedOn;
            foreach (var _businessCaseObj in _businessCasesList)
            {
                UTC_CreatedOn = DateTime.Parse(Convert.ToString(_businessCaseObj.CreatedOn));
                UTC_UpdatedOn = DateTime.Parse(Convert.ToString(_businessCaseObj.UpdatedOn));
                if (localTimeZone == PlanNSchedConstant.DefaultTimeZone)
                {
                    _businessCaseObj.CreatedOn = UTC_CreatedOn.ToLocalTime();
                    _businessCaseObj.UpdatedOn = UTC_UpdatedOn.ToLocalTime();
                }
                else
                {
                    _businessCaseObj.CreatedOn = TimeZoneInfo.ConvertTimeFromUtc(UTC_CreatedOn, cstZone);
                    _businessCaseObj.UpdatedOn = TimeZoneInfo.ConvertTimeFromUtc(UTC_UpdatedOn, cstZone);
                }
                _updatedBusinessCasesList.Add(_businessCaseObj);
            }
            return _updatedBusinessCasesList;
        }

        private async Task DeletePlanAsync()
        {
            logger.LogMethodStart();
            _deletedialog.Close();
            LockLoading();
            try
            {
                StateHasChanged();
                await UtilityUI.DeletePlanAsync(BusinessCaseId, Client);
                await GetRegionsAsync();
                StatusMessageContent = "Transactional data removed";
                StatusPopup = true;
            }
            catch (Exception ex)
            {
                logger.LogErrorAndNotify(PopupService, ex, "Error occurred in DeletePlanAsync method.");
            }
            finally
            {
                UnlockLoading();
                StateHasChanged();
                logger.LogMethodEnd();
            }
        }
    }
}