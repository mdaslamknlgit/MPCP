using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MPC.PlanSched.Model;
using MPC.PlanSched.Service;
using MPC.PlanSched.Shared.Common;
using MPC.PlanSched.Shared.Common.Extensions;
using MPC.PlanSched.Shared.Service.Schema;
using Newtonsoft.Json;

namespace MPC.PlanSched.UI.Pages.RegionalPremise
{
    [Authorize(Policy = PlanNSchedConstant.ViewPimsRegionalPremise)]
    public partial class RegionalPremise
    {
        [Parameter]
        public string SelectedRole { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public string PlanDescription { get; set; } = string.Empty;
        public bool IsHistoricalData { get; set; } = true;
        public string? ApplicationState { get; set; }
        public MPC.PlanSched.Shared.Service.Schema.BusinessCase BusinessCase { get; set; } = new();
        public List<KeyValuePair<string, string>> RegionalPremiseConstraints { get; set; } = new();
        private string _localTimeZoneName = PlanNSchedConstant.DefaultTimeZone;
        private bool _isParentInitialized = false;
        private bool _isFirstLoad = true;
        private const string _regionalPremisePeriodSelectionChanged = "RegionalPremisePeriodSelectionChanged";

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                LockLoading();
                StateHasChanged();
                _localTimeZoneName = SessionService.GetLocalTimezoneName();
                Client = ClientFactory.CreateClient(PlanNSchedConstant.WebAPI);
                Client.Timeout = TimeSpan.FromSeconds(PlanNSchedConstant.Timeout);
                OverrideTypes = await UtilityUI.GetOverrideTypes(Client);
                RegionModel = await UtilityUI.GetRegionByBusinessCaseIdAsync(BusinessCaseId, SessionService.GetCorrelationId(), Client);
                Refineries = await UtilityUI.GetAllRefineryPremiseActivePlansAsync(Client, _localTimeZoneName, SessionService.GetCorrelationId());
                if (RegionModel != null)
                {
                    RegionTitle = RegionModel.BusinessCase.Name;
                    PlanUpdatedOn = string.Format("Last Saved: {0}", (RegionModel.BusinessCase.UpdatedOn?.ToString("MM/dd/yy") ?? ""));
                    RegionName = RegionModel.RegionName;
                    PlanDescription = RegionModel.BusinessCase.Description;
                    ReadOnlyFlag = IsHistoricalData = IsHistoricalPlan;
                    RegionModel.IsHierarchy = false;
                    if (!ConfigurationUI.IsMidtermEnabled)
                        PlanType = RegionModel.BusinessCase.PlanType ?? string.Empty;
                }

                await GetPeriodsAsync();
                await GetConstraintsAsync();

                _isParentInitialized = true;
                _isFirstLoad = true;

                CommonHelper.UpdateBaggage(SessionService.GetCorrelationId(),
                    RegionModel?.BusinessCase?.Id,
                    RequestedPeriods?.FirstOrDefault()?.PeriodID,
                    RegionModel?.DomainNamespace?.DestinationApplication.Name);

                UnlockLoading();
                StateHasChanged();
            }
            await base.OnAfterRenderAsync(firstRender);
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
                if (RegionModel != null)
                {
                    UserName = await ActiveUser.GetNameAsync();
                    RegionModel.IsHierarchy = false;
                    RequestedPeriods = UtilityUI.GetPeriodData(RequestedPeriods, RegionModel, SessionService, Client, UserName);
                    if (RequestedPeriods.Any())
                    {
                        Periods = RequestedPeriods.Where(x => x.PeriodName != PlanNSchedConstant.LastPeriodOCostName).ToList();
                        SelectedPeriodId = Periods[0].PeriodID;
                        PriceEffectiveDate = RequestedPeriods[0].PriceEffectiveDate?.ToString(Constant.DateFormat);
                    }
                    UnlockLoading();
                    StateHasChanged();
                    Logger.LogMethodEnd();
                    return Periods;
                }
                else
                {
                    UnlockLoading();
                    StateHasChanged();
                    Logger.LogMethodError(new Exception($"RegionModel is null in {GetPeriods} method."));
                    return Periods;
                }
            }
            catch (JsonSerializationException exSz)
            {
                UnlockLoading();
                StateHasChanged();
                Logger.LogMethodError(exSz, $"{Constant.SerializationException} occurred in {GetPeriods} UI method.");
                return Periods;
            }
            catch (Exception ex)
            {
                UnlockLoading();
                StateHasChanged();
                Logger.LogMethodError(ex, $"Error occurred in {GetPeriods} method.");
                return Periods;
            }
        }

        public async Task GetConstraintsAsync()
        {
            var destinationApplicationName = RegionModel?.DomainNamespace?.DestinationApplication.Name;
            var selectedRegion = string.Empty;
            switch (destinationApplicationName)
            {
                case Constant.SouthPIMS:
                    RegionalPremiseConstraints = new()
                    {
                        new("Regional Caps", RegionalPremiseConstraintType.RegionalCapsBounds.Description()),
                        new("Transfer", RegionalPremiseConstraintType.RegionalTransfer.Description()),
                    };
                    selectedRegion = Constant.SouthRegion;
                    break;
                case Constant.NorthPIMS:
                    RegionalPremiseConstraints = new()
                    {
                        new("Regional Bounds", RegionalPremiseConstraintType.RegionalCapsBounds.Description()),
                        new("Transfer", RegionalPremiseConstraintType.RegionalTransfer.Description())
                    };
                    selectedRegion = Constant.NorthRegion;
                    break;
                case Constant.WestPIMS:
                    RegionalPremiseConstraints = new()
                    {
                        new("Regional Caps", RegionalPremiseConstraintType.RegionalCapsBounds.Description()),
                        new("Transfer", RegionalPremiseConstraintType.RegionalTransfer.Description())
                    };
                    selectedRegion = Constant.WestRegion;
                    break;
            }

            var regionSpecificRefineryConstraints = Refineries.Where(r => r.RegionName == selectedRegion).Select(r => new List<KeyValuePair<string, string>>()
            {
                new($"{r.Name} {RefineryPremiseConstraintType.Caps.Description()}", $"{r.Name} {RefineryPremiseConstraintType.Caps.Description()}"),
                new($"{r.Name} {RefineryPremiseConstraintType.Bounds.Description()}", $"{r.Name} {RefineryPremiseConstraintType.Bounds.Description()}"),
                new($"{r.Name} {RefineryPremiseConstraintType.Proclim.Description()}", $"{r.Name} {RefineryPremiseConstraintType.Proclim.Description()}"),
                new($"{r.Name} {RefineryPremiseConstraintType.Pinv.Description()}", $"{r.Name} {RefineryPremiseConstraintType.Pinv.Description()}"),
            }).ToList();

            foreach (var refineryConstraint in regionSpecificRefineryConstraints)
            {
                RegionalPremiseConstraints.AddRange(refineryConstraint);
            }

            SelectedConstraint = RegionalPremiseConstraints[0].Value;
        }
        public async Task RegionalPremisePeriodSelectionChangedAsync(int periodId)
        {
            SelectedPeriodId = periodId;
            _isFirstLoad = false;
        }

        public async Task RegionalPremiseConstraintSelectionChangedAsync(string constraint)
        {
            SelectedConstraint = constraint;
            _isFirstLoad = false;
        }

        public async Task ExcelDownloadAsync()
        {
            try
            {
                logger?.LogMethodStart();
                LockLoading();
                var data = await _excelCommon.GetExcelBase64ByRegion(RegionModel, ApplicationArea.regionalplanning);
                var fileName = RegionModel?.DomainNamespace?.DestinationApplication.Name + PlanNSchedConstant.PlanningFile;
                await JsRuntime.InvokeVoidAsync(PlanNSchedConstant.ExcelDownloadJavascriptFunction, data, fileName, PlanNSchedConstant.ExcelDownloadContentType);
            }
            catch (Exception ex)
            {
                logger?.LogMethodError(ex, "Error occurred in ExcelDownload method for PIMS.");
            }
            finally
            {
                UnlockLoading();
                logger?.LogMethodEnd();
            }
        }

    }
}
