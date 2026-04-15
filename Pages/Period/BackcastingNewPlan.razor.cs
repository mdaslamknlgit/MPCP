using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using MPC.PlanSched.Model;
using MPC.PlanSched.Service;
using MPC.PlanSched.Shared.Common;
using MPC.PlanSched.Shared.Common.Extensions;
using MPC.PlanSched.Shared.Service.Schema;
using MPC.PlanSched.UI.Shared;
using Newtonsoft.Json;
using BusinessCase = MPC.PlanSched.Shared.Service.Schema.BusinessCase;

namespace MPC.PlanSched.UI.Pages.Period
{
    [Authorize(Policy = PlanNSchedConstant.ViewPimsBackcasting)]
    public partial class BackcastingNewPlan
    {
        [Parameter]
        public string SelectedRole { get; set; } = string.Empty;
        public string? RegionTitle { get; set; }
        public string PlanDescription { get; set; } = string.Empty;
        [Parameter]
        public int DomainNamespaceId { get; set; }
        [Inject]
        public ILogger<BackcastingNewPlan> Logger { get; set; } = default!;
        [CascadingParameter]
        public INavigationDrawer NavDrawer { get; set; } = default!;
        [CascadingParameter(Name = "SelectedRole")]
        public string UserSelectedRole { get; set; } = string.Empty;
        public DateTime PriceEffectiveDate { get; set; } = DateTime.UtcNow.AddDays(-1);
        public DateTime SupplyEffectiveDate { get; set; } = DateTime.Now.AddDays(-1);
        public List<string>? Rules { get; set; }
        private string _validationMessage = "";

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                try
                {
                    UnlockLoading();
                    StateHasChanged();
                    SetNavMenuDrawer();
                    await SetRules("Settings/periodrules.json");
                    Client = ClientFactory.CreateClient("WebAPI");
                    Client.Timeout = TimeSpan.FromSeconds(600);
                    RegionModel = await UtilityUI.GetRegionAsync(DomainNamespaceId, SessionService.GetCorrelationId(),
                        Client, Service.Model.State.Actual);
                    UserName = await ActiveUser.GetNameAsync();
                    if (RegionModel != null)
                    {
                        var firstDayOfPreviousMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-1);
                        var lastDayOfPreviousMonth = firstDayOfPreviousMonth.AddMonths(1).AddDays(-1);

                        RegionModel.BusinessCase = new BusinessCase
                        {
                            Name = $"{RegionModel?.DomainNamespace?.DestinationApplication.Name}_ACTUALSDATA_{firstDayOfPreviousMonth:dd-MMM-yy}_{lastDayOfPreviousMonth:dd-MMM-yy}".ToUpper(),
                            Description = string.Empty,
                            Id = -1,
                            CreatedBy = UserName,
                            CreatedOn = DateTime.UtcNow,
                            UpdatedBy = UserName,
                            UpdatedOn = DateTime.UtcNow
                        };

                        CommonHelper.UpdateBaggageWOPeriod(SessionService.GetCorrelationId(),
                            RegionModel.BusinessCase?.Id, RegionModel?.DomainNamespace?.DestinationApplication.Name);
                        RegionTitle = RegionModel?.BusinessCase?.Name;

                        var firstPeriod = new Model.RequestedPeriodModel
                        {
                            DateTimeRange = new DateTimeRange { FromDateTime = firstDayOfPreviousMonth, ToDateTime = lastDayOfPreviousMonth },
                            CreatedBy = UserName,
                            CreatedOn = DateTime.UtcNow,
                            ModifiedBy = UserName,
                            ModifiedOn = DateTime.UtcNow,
                            DomainNamespace = RegionModel.DomainNamespace,
                            BusinessCase = RegionModel.BusinessCase
                        };
                        Periods.Add(firstPeriod);
                    }
                    UnlockLoading();
                    StateHasChanged();
                }
                catch (FileNotFoundException fnfex)
                {
                    _validationMessage = fnfex.Message;
                }
                catch (Exception ex)
                {
                    Logger.LogMethodError(ex, "Error occurred in period initialization.");
                }
            }
            await base.OnAfterRenderAsync(firstRender);
        }

        public async Task SetRules(string filePath)
        {
            var jsonString = await System.IO.File.ReadAllTextAsync(filePath);
            var definition = new[] { new { PeriodRule = "" } };
            var periodRules = JsonConvert.DeserializeAnonymousType(jsonString, definition);
            if (periodRules != null && periodRules.Count() > 0)
            {
                Rules = new List<string>();
                foreach (var rule in periodRules)
                {
                    Rules.Add(rule.PeriodRule);
                }
            }
        }

        public void AddPeriod()
        {
            if (Periods.Count == PlanNSchedConstant.MaxPeriodCount)
            {
                _validationMessage = $"You can't add more than {PlanNSchedConstant.MaxPeriodCount} periods";
                return;
            }
            try
            {
                var lastPeriodEndDate = Periods.LastOrDefault()?.DateTimeRange.ToDateTime;
                var newPeriod = new Model.RequestedPeriodModel
                {
                    DateTimeRange = new DateTimeRange
                    {
                        FromDateTime = lastPeriodEndDate.HasValue ? lastPeriodEndDate.Value.AddDays(1) : null,
                        ToDateTime = null
                    },
                    CreatedBy = UserName,
                    CreatedOn = DateTime.UtcNow,
                    ModifiedBy = UserName,
                    ModifiedOn = DateTime.UtcNow,
                    DomainNamespace = RegionModel.DomainNamespace
                };

                Periods.Add(newPeriod);
            }
            catch (Exception ex)
            {
                Logger.LogMethodError(ex, "Error occurred in AddPeriod method.");
            }
        }

        public void UpdateBusinessCaseNameDescription()
        {
            var earliestStartDate = Periods.Min(p => p.DateTimeRange.FromDateTime);
            var latestEndDate = Periods.Max(p => p.DateTimeRange.ToDateTime);

            if (earliestStartDate.HasValue && latestEndDate.HasValue)
                RegionTitle = $"{RegionModel?.DomainNamespace?.DestinationApplication.Name}_ACTUALSDATA_{earliestStartDate.Value:dd-MMM-yy}_{latestEndDate.Value:dd-MMM-yy}".ToUpper();
            else
                RegionTitle = RegionModel?.BusinessCase?.Name ?? $"BaseCase_{DateTime.UtcNow:dd-MMM-yy}".ToUpper();
        }

        public async Task SubmitPeriodAsync()
        {
            try
            {
                LockLoading();
                Logger.LogMethodStart();
                if (await IsOverlappingWithExistingPlanPeriodsAsync())
                    _validationMessage = "The selected plan period conflicts with an existing active plan. Please select alternative dates.";
                else
                    _validationMessage = ValidatePeriods();

                if (!string.IsNullOrEmpty(_validationMessage))
                    return;

                await SaveNewPlanAsync();
            }
            catch (Exception ex)
            {
                Logger.LogMethodError(ex, "Error in SubmitPeriodAsync.");
            }
            finally
            {
                UnlockLoading();
                Logger.LogMethodEnd();
            }
        }

        private async Task<bool> IsOverlappingWithExistingPlanPeriodsAsync()
        {
            var activeBackcastingBusinessCases = await Client.GetFromJsonAsync<List<BusinessCase>>(ConfigurationUI.api_Plan_GetAllActiveActualBusinessCasesAsync + RegionModel.DomainNamespace.DestinationApplication.Id);
            var minDate = Periods.Min(x => x.DateTimeRange.FromDateTime).Value.Date;
            var maxDate = Periods.Max(x => x.DateTimeRange.ToDateTime).Value.Date;
            foreach (var businessCase in activeBackcastingBusinessCases)
            {                
                var businessCaseParts = businessCase.Name.Split(Constant.EmptyUnderscore);
                if (businessCaseParts.Length < 4)
                {
                    Logger.LogInformation($"Invalid businessCase Name format{{0}}", businessCase.Name);
                    return false;
                }

                var minDateStr = businessCaseParts[^2]; 
                var maxDateStr = businessCaseParts[^1];

                if (!DateTime.TryParseExact(minDateStr, "dd-MMM-yy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var existingMinDate) ||
                    !DateTime.TryParseExact(maxDateStr, "dd-MMM-yy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var existingMaxDate))
                {
                    Logger.LogInformation("Date format mismatch Error in SubmitPeriodAsync");
                    return false;
                }

                var isOverlapping = IsWithinDateRange(minDate, existingMinDate, existingMaxDate) ||
                                    IsWithinDateRange(maxDate, existingMinDate, existingMaxDate) ||
                                    IsWithinDateRange(existingMinDate, minDate, maxDate) ||
                                    IsWithinDateRange(existingMaxDate, minDate, maxDate);
                if (isOverlapping) return true;
            }
            return false;
        }
        private static bool IsWithinDateRange(DateTime date, DateTime startDate, DateTime endDate) =>
             date >= startDate && date <= endDate;

        private string ValidatePeriods()
        {
            if (PriceEffectiveDate > DateTime.UtcNow)
                return "Future effective date is not allowed.";

            var invalidPeriodIndex = Periods.FindIndex(o => o.DateTimeRange.FromDateTime == null || o.DateTimeRange.ToDateTime == null);
            if (invalidPeriodIndex != -1)
                return "Please add valid periods.";

            var index = Periods.FindIndex(a => a.DateTimeRange.FromDateTime != null && a.DateTimeRange.ToDateTime == null);
            if (index != -1)
                return "Please add an end date.";

            var invalidPeriod = Periods.FindIndex(d => d.DateTimeRange.FromDateTime >= d.DateTimeRange.ToDateTime);
            if (invalidPeriod != -1)
                return "Start date of each period should be less than end date.";

            return "";
        }
        private void FinalizePeriods()
        {
            var clearDate = new DateTime(0001, 1, 1);
            var currentLocalTime = DateTime.Now.TimeOfDay;
            SupplyEffectiveDate = SupplyEffectiveDate.Date.Add(currentLocalTime);
            var periodId = 1;
            foreach (var period in Periods)
            {
                period.PeriodID = periodId;
                period.PeriodName = Constant.P + periodId;
                period.PriceEffectiveDate = PriceEffectiveDate == clearDate ? DateTime.UtcNow.AddDays(-1) : PriceEffectiveDate;
                period.SupplyEffectiveDate = SupplyEffectiveDate == clearDate ? DateTime.UtcNow.AddDays(-1) : SupplyEffectiveDate.ToUniversalTime();
                period.DateTimeRange.ToDateTime = Convert.ToDateTime(period.DateTimeRange.ToDateTime?.ToString("dd-MMM-yy 23:59:59"));
                period.BusinessCase = RegionModel.BusinessCase;
                period.ApplicationState = Service.Model.State.Actual.Description();
                periodId++;
            }
        }

        public void OnDescriptionChanged(string value)
        {
            ValidateTextLength(value, v => PlanDescription = v);
            StateHasChanged();
        }

        private async Task SaveNewPlanAsync()
        {
            RegionModel.BusinessCase.Name = RegionTitle;
            RegionModel.BusinessCase.Description = PlanDescription != string.Empty ? PlanDescription : RegionTitle;
            RegionModel.ApplicationState = Service.Model.State.Actual.Description();
            FinalizePeriods();
            try
            {
                LockLoading();
                var response = await Client.PostAsJsonAsync(ConfigurationUI.SavePeriods, Periods);

                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                    _validationMessage = "Error occured while submitting periods";

                var latestBusinessCaseObj = await Client.GetFromJsonAsync<BusinessCase>(string.Format(ConfigurationUI.api_Plan_GetLatestBusinessCase, RegionModel.DomainNamespace.DestinationApplication.Id, RegionModel.ApplicationState));
                RegionModel.BusinessCase = latestBusinessCaseObj;
                latestBusinessCaseObj?.Id.UpdateBusinessCaseId();
                NavigateToBackcastingProduct(SelectedRole, latestBusinessCaseObj.Id);
                Logger.LogMethodEnd();

            }
            catch (Mpc.Helios.Exceptions.NullReferenceException exNull)
            {
                Logger.LogMethodError(exNull, Constant.NullReferenceException + " occurred in SubmitPeriod method.");
            }
            catch (Exception ex)
            {
                Logger.LogMethodError(ex, "Error occurred in SubmitPeriod method.");
            }
            finally
            {
                UnlockLoading();
                StateHasChanged();
            }
        }

        public void UpdateNextPeriodStartDate(int index)
        {
            UpdateBusinessCaseNameDescription();
            if (index + 1 >= Periods.Count)
                return;

            if (Periods[index + 1].DateTimeRange.FromDateTime == null || Periods[index].DateTimeRange.ToDateTime < Periods[index + 1].DateTimeRange.FromDateTime)
            {
                Periods[index + 1].DateTimeRange.FromDateTime = Convert.ToDateTime(Periods[index].DateTimeRange.ToDateTime).AddDays(1);
                _validationMessage = "";
            }
            else
            {
                Periods[index].DateTimeRange.ToDateTime = null;
                _validationMessage = "The period end date should be less than the next period start date.";
            }
        }

        public void DeletePeriod(int index)
        {
            _validationMessage = "";
            if (index > 0 && Periods[index].DateTimeRange.FromDateTime.HasValue && Periods.Count > index + 1)
            {
                Periods[index + 1].DateTimeRange.FromDateTime = Periods[index].DateTimeRange.FromDateTime;
                if (Periods[index].DateTimeRange.ToDateTime.HasValue)
                    Periods[index + 1].DateTimeRange.ToDateTime = Periods[index].DateTimeRange.ToDateTime;
            }

            Periods.RemoveAt(index);
            UpdateBusinessCaseNameDescription();
        }

        public void CancelPeriod() => NavigateToRegionBackcasting(SelectedRole);

        public DateTime GetMinToDate(int periodIndex)
        {
            if (Periods[periodIndex].DateTimeRange.FromDateTime.HasValue)
                return ((DateTime)Periods[periodIndex].DateTimeRange.FromDateTime).AddDays(1);

            return new DateTime(2000, 1, 1);
        }

        public void SetInitialEndDate(int periodIndex)
        {
            if (Periods[periodIndex].DateTimeRange.FromDateTime.HasValue)
                Periods[periodIndex].DateTimeRange.ToDateTime = Periods[periodIndex].DateTimeRange.FromDateTime.GetValueOrDefault().AddDays(1);
        }

        public void SetNavMenuDrawer()
        {
            NavDrawer.SetExpanded(false);
            NavDrawer.SetActiveMenuItemByUrl(string.Format(PlanNSchedConstant.RegionsBackcasting, UserSelectedRole));
        }
    }
}
