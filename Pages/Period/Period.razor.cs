using Microsoft.AspNetCore.Components;
using MPC.PlanSched.Model;
using MPC.PlanSched.Service;
using MPC.PlanSched.Shared.Common;
using MPC.PlanSched.Shared.Common.Extensions;
using MPC.PlanSched.Shared.Service.Schema;
using MPC.PlanSched.UI.Shared;
using Newtonsoft.Json;
using Telerik.Blazor.Components;

namespace MPC.PlanSched.UI.Pages.Period
{
    public partial class Period
    {
        [Parameter]
        public string SelectedRole { get; set; } = string.Empty;
        [Parameter]
        public string? RegionTitle { get; set; }
        [Parameter]
        public int DomainNamespaceId { get; set; }
        public string? DomainNamespaceName { get; set; }
        public string? RegionName { get; set; }
        [Inject]
        public ILogger<Period> Logger { get; set; } = default!;
        public List<Model.RequestedPeriodModel> Periods { get; set; } = new();
        [CascadingParameter]
        public INavigationDrawer NavDrawer { get; set; } = default!;
        [CascadingParameter(Name = "SelectedRole")]
        public string UserSelectedRole { get; set; } = string.Empty;
        public class AddNewPeriodBusinessRule
        {
            public string? PeriodRule { get; set; }
        }
        public DateTime PriceEffectiveDate { get; set; } = DateTime.UtcNow.AddDays(-1);
        public DateTime SupplyEffectiveDate { get; set; } = DateTime.Now.AddDays(-1);
        public List<AddNewPeriodBusinessRule>? Rules { get; set; }
        public TelerikDatePicker<DateTime?> DatePickerRef { get; set; } = default!;
        private Dialog _dialog = default!;
        private string _description = "";
        private string _validationMessage = "";

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                try
                {
                    LockLoading();
                    StateHasChanged();
                    SetNavMenuDrawer();
                    Rules = await ReadJsonFileAsync("Settings/periodrules.json");
                    Client = ClientFactory.CreateClient("WebAPI");
                    Client.Timeout = TimeSpan.FromSeconds(600);
                    RegionModel = await UtilityUI.GetRegionAsync(DomainNamespaceId, SessionService.GetCorrelationId(), Client, Service.Model.State.Forecast);
                    RegionModel.ApplicationState = Service.Model.State.Forecast.Description();
                    UserName = await ActiveUser.GetNameAsync();
                    if (RegionModel.PlanTypes != null && !ConfigurationUI.IsMidtermEnabled)
                        RegionModel.PlanType = RegionModel.PlanTypes.FirstOrDefault(pt => pt.IsDefault)?.Name;
                    var businessCase = new MPC.PlanSched.Shared.Service.Schema.BusinessCase
                    {
                        Name = $"{RegionModel?.DomainNamespace?.DestinationApplication.Name}_BaseCase_{DateTime.UtcNow:dd-MMM-yy}".ToUpper(),
                        Description = string.Empty,
                        Id = -1,
                        CreatedBy = UserName,
                        CreatedOn = DateTime.UtcNow,
                        UpdatedBy = UserName,
                        UpdatedOn = DateTime.UtcNow,
                    };
                    RegionModel.BusinessCase = businessCase;

                    CommonHelper.UpdateBaggageWOPeriod(SessionService.GetCorrelationId(),
                        RegionModel.BusinessCase?.Id, RegionModel?.DomainNamespace?.DestinationApplication.Name);

                    if (RegionModel != null)
                    {
                        RegionName = RegionModel.RegionName;
                        DomainNamespaceName = RegionModel.DomainNamespace.DestinationApplication.Name;
                        RegionTitle = RegionModel?.BusinessCase?.Name ?? $"BaseCase_{DateTime.UtcNow:dd - MMM - yy}";
                    }
                    for (var PeriodIndex = 0; PeriodIndex < 3; PeriodIndex++)
                    {
                        Periods.Add(new Model.RequestedPeriodModel
                        {
                            DateTimeRange = new DateTimeRange { FromDateTime = null, ToDateTime = null },
                            CreatedBy = UserName,
                            CreatedOn = DateTime.UtcNow,
                            ModifiedBy = UserName,
                            ModifiedOn = DateTime.UtcNow,
                            DomainNamespace = RegionModel.DomainNamespace,
                            BusinessCase = businessCase,
                            ApplicationState = Service.Model.State.Forecast.Description()
                        });
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

        public async Task<List<AddNewPeriodBusinessRule>> ReadJsonFileAsync(string filePath)
        {
            var jsonString = await System.IO.File.ReadAllTextAsync(filePath);
            return JsonConvert.DeserializeObject<List<AddNewPeriodBusinessRule>>(jsonString);
        }

        public void AddPeriod()
        {
            try
            {
                if (Periods.Count == 12)
                {
                    _validationMessage = "You can't add more than 12 periods";
                    return;
                }

                var currentTime = DateTime.UtcNow;
                var currentUser = UserName;
                var businessCase = new MPC.PlanSched.Shared.Service.Schema.BusinessCase
                {
                    Name = $"{RegionModel.DomainNamespace.DestinationApplication.Name}_BaseCase_{currentTime:dd-MMM-yy}".ToUpper(),
                    Description = string.Empty,
                    Id = -1,
                    CreatedBy = currentUser,
                    CreatedOn = currentTime,
                    UpdatedBy = currentUser,
                    UpdatedOn = currentTime
                };
                RegionModel.BusinessCase = businessCase;

                Periods.Add(new Model.RequestedPeriodModel
                {
                    DateTimeRange = new DateTimeRange { FromDateTime = Periods[Periods.Count - 1].DateTimeRange.ToDateTime == null ? null : Convert.ToDateTime(Periods[Periods.Count - 1].DateTimeRange.ToDateTime).AddDays(1), ToDateTime = null },
                    CreatedBy = currentUser,
                    CreatedOn = currentTime,
                    ModifiedBy = currentUser,
                    ModifiedOn = currentTime,
                    DomainNamespace = RegionModel.DomainNamespace,
                    BusinessCase = businessCase
                });
                _validationMessage = "";
            }
            catch (Exception ex)
            {
                Logger.LogMethodError(ex, "Error occurred in AddPeriod method.");
            }
        }

        public async Task SubmitPeriod()
        {
            Logger.LogMethodStart();

            _validationMessage = ValidatePeriods();

            if (!string.IsNullOrEmpty(_validationMessage))
                return;

            if (Periods.FirstOrDefault().DateTimeRange.FromDateTime < DateTime.UtcNow)
            {
                _dialog.Open();
            }
            else
            {
                await SaveNewPlanAsync();
            }
        }

        private string ValidatePeriods()
        {
            if (PriceEffectiveDate > DateTime.UtcNow)
                return "Future effective date is not allowed.";

            if (SupplyEffectiveDate > DateTime.Now)
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

        public void OnDescriptionChanged(string value)
        {
            ValidateTextLength(value, v => _description = v);
            StateHasChanged();
        }

        private async Task SaveNewPlanAsync()
        {
            if (_dialog.IsVisible) _dialog.Close();

            var currentLocalTime = DateTime.Now.TimeOfDay;
            SupplyEffectiveDate = SupplyEffectiveDate.Date.Add(currentLocalTime);

            var periodId = 1;
            var currentTime = DateTime.UtcNow;
            var defaultDescripiton = $"{RegionModel.DomainNamespace.DestinationApplication.Name}_BaseCase_{currentTime:dd-MMM-yy}".ToUpper();
            var clearDate = new DateTime(0001, 1, 1);
            var PlanTypeId = RegionModel.PlanTypes?.FirstOrDefault(pt => pt.Name == RegionModel.PlanType)?.Id ?? null;
            foreach (var period in Periods)
            {
                period.PeriodID = periodId;
                period.PeriodName = Constant.P + periodId;
                period.PriceEffectiveDate = PriceEffectiveDate == clearDate ? DateTime.UtcNow.AddDays(-1) : PriceEffectiveDate;
                period.SupplyEffectiveDate = SupplyEffectiveDate == clearDate
                    ? DateTime.UtcNow.AddDays(-1)
                    : SupplyEffectiveDate.ToUniversalTime();
                period.DateTimeRange.ToDateTime = Convert.ToDateTime(period.DateTimeRange.ToDateTime?.ToString("dd-MMM-yy 23:59:59"));
                period.BusinessCase.Description = string.IsNullOrEmpty(_description) ? defaultDescripiton : _description;
                period.BusinessCase.PlanTypeId = PlanTypeId;
                periodId++;
            }

            ///Pn period is only used to fetch demand price of last period for CPrice of opening inventory
            var nextMonthCPriceDate = Periods.LastOrDefault().DateTimeRange.ToDateTime.Value.AddMonths(1);
            Periods.Add(new Model.RequestedPeriodModel
            {
                PeriodID = periodId,
                PeriodName = "Pn",
                PeriodDescription = PlanNSchedConstant.LastPeriodOCostDesc,
                PriceEffectiveDate = PriceEffectiveDate == clearDate ? DateTime.UtcNow.AddDays(-1) : PriceEffectiveDate,
                SupplyEffectiveDate = SupplyEffectiveDate == clearDate
                    ? DateTime.UtcNow.AddDays(-1)
                    : SupplyEffectiveDate.ToUniversalTime(),
                DateTimeRange = new DateTimeRange
                {
                    ToDateTime = nextMonthCPriceDate,
                    FromDateTime = nextMonthCPriceDate
                },
                BusinessCase = new MPC.PlanSched.Shared.Service.Schema.BusinessCase
                {
                    Description = _description ?? defaultDescripiton,
                    Id = Periods.FirstOrDefault().BusinessCase.Id,
                    Name = Periods.FirstOrDefault().BusinessCase.Name,
                    CreatedBy = Periods.FirstOrDefault().BusinessCase.CreatedBy,
                    CreatedOn = Periods.FirstOrDefault().BusinessCase.CreatedOn,
                    PlanTypeId = PlanTypeId,
                },
                DomainNamespace = Periods.FirstOrDefault().DomainNamespace,
                CreatedOn = Periods.FirstOrDefault().CreatedOn,
                CreatedBy = Periods.FirstOrDefault().CreatedBy,
                ModifiedOn = Periods.FirstOrDefault().ModifiedOn,
                ModifiedBy = Periods.FirstOrDefault().ModifiedBy,
                CorrelationId = SessionService?.GetCorrelationId()
            });

            try
            {
                LockLoading();
                var response = await Client.PostAsJsonAsync(ConfigurationUI.SavePeriods, Periods);

                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    var latestBusinessCaseObj = await Client.GetFromJsonAsync<MPC.PlanSched.Shared.Service.Schema.BusinessCase>(string.Format(ConfigurationUI.api_Plan_GetLatestBusinessCase, RegionModel.DomainNamespace.DestinationApplication.Id, RegionModel.ApplicationState));
                    RegionModel.BusinessCase = latestBusinessCaseObj;
                    latestBusinessCaseObj?.Id.UpdateBusinessCaseId();

                    RegionModel.CreatedBy = latestBusinessCaseObj.CreatedBy;
                    RegionModel.CreatedOn = latestBusinessCaseObj.CreatedOn;
                    RegionModel.CorrelationId = SessionService?.GetCorrelationId();
                    var businessCaseId = latestBusinessCaseObj.Id;

                    if (RegionModel.Type.ToString() == PlanNSchedConstant.DPO)
                    {
                        NavigateToProductDP(SelectedRole, businessCaseId);
                    }
                    else
                    {
                        NavigateToProductRP(SelectedRole, businessCaseId);
                    }
                    Logger.LogMethodEnd();
                }
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

        public void ChangeStartDate(int index)
        {
            if (index < Periods.Count - 1)
                Periods[index + 1].DateTimeRange.FromDateTime = Convert.ToDateTime(Periods[index].DateTimeRange.ToDateTime).AddDays(1);
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
        }

        public void CancelPeriod()
        {
            if (RegionModel.Type.ToString() == PlanNSchedConstant.DPO)
            {
                NavigateToRegionDP(SelectedRole);
            }
            else
            {
                NavigateToRegionRP(SelectedRole);
            }
        }

        public DateTime GetMinToDate(int periodIndex)
        {
            if (Periods[periodIndex].DateTimeRange.FromDateTime.HasValue)
                return ((DateTime)Periods[periodIndex].DateTimeRange.FromDateTime).AddDays(1);

            return new DateTime(2000, 1, 1);
        }

        public void RefreshEndDate(int periodIndex)
        {
            if (Periods[periodIndex].DateTimeRange.FromDateTime.HasValue)
                Periods[periodIndex].DateTimeRange.ToDateTime = Periods[periodIndex].DateTimeRange.FromDateTime.GetValueOrDefault().AddDays(1);

            DatePickerRef.Refresh();
        }

        public void SetNavMenuDrawer()
        {
            NavDrawer.SetExpanded(false);
            if (UserSelectedRole.Contains("dpo"))
            {
                NavDrawer.SetActiveMenuItemByUrl(string.Format(PlanNSchedConstant.RegionsDp, UserSelectedRole));
            }
            else
            {
                NavDrawer.SetActiveMenuItemByUrl(string.Format(PlanNSchedConstant.RegionsRp, UserSelectedRole));
            }
        }
    }
}
