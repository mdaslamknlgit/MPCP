using Microsoft.AspNetCore.Components;
using Microsoft.FeatureManagement;
using MPC.PlanSched.Model;
using MPC.PlanSched.Service;
using MPC.PlanSched.Shared.Common;
using MPC.PlanSched.Shared.Common.Extensions;
using MPC.PlanSched.UI.ViewModel;
using Telerik.Blazor.Components;

namespace MPC.PlanSched.UI.Shared
{
    public partial class RefineryPremiseRefreshDialog
    {
        public bool isModalVisible = false;
        public DateTime? PriceEffectiveDate { get; set; } = DateTime.UtcNow.AddDays(-1);
        public DateTime? PriceEffectiveDateOriginal { get; set; } = DateTime.UtcNow.AddDays(-1);
        public bool allSelected = false, isDatePickerEnabled = false;
        [Inject]
        ISessionService? SessionService { get; set; }
        [Inject]
        public IFeatureManager FeatureManager { get; set; } = default!;
        public List<ROInterfaces>? InterfaceListRefineryPremise { get; set; }
        public TelerikDialog? dialogRef;
        public bool _isOkEnabled = false;
        public async void OpenAsync(RefineryModel refineryModel)
        {
            var refineryPremiseInterfaces = new List<string>
            {
                Constant.PriceAndCost,
                Constant.SellPrice,
                Constant.Buy
            };

            if (await FeatureManager.IsEnabledAsync(FeatureFlags.PlanPremise))
            {
                refineryPremiseInterfaces.Add(Constant.RefineryPremise);
            }

            InterfaceListRefineryPremise = CreateInterfaceList(refineryPremiseInterfaces);

            var pageUri = NavigationManager.Uri;

            Client = ClientFactory.CreateClient(PlanNSchedConstant.WebAPI);
            Client.Timeout = TimeSpan.FromSeconds(PlanNSchedConstant.Timeout);
            UserName = await ActiveUser.GetNameAsync() ?? RefineryModel.UpdatedBy;

            isModalVisible = true;
            await InvokeAsync(() =>
            {
                StateHasChanged();
            });
            RefineryModel = refineryModel;
            RequestedPeriods = await UtilityUI.GetPeriodDataByBusinessCaseId(RefineryModel.BusinessCase.Id, SessionService.GetCorrelationId(), Client, UserName);
            PriceEffectiveDate = DateTime.Parse(RequestedPeriods[0].PriceEffectiveDate?.ToString("MM/dd/yy"));
            PriceEffectiveDateOriginal = PriceEffectiveDate;
        }

        public void AllPlanCheckBoxChange(object value)
        {
            var isSelected = (bool)value;
            var interfaces = GetInterfacesList();
            isDatePickerEnabled = false;
            interfaces.ForEach(eq =>
            {
                if (eq.IsSelected != isSelected && !eq.IsDisabled)
                    eq.IsSelected = isSelected;
                if (eq.InterfaceName == Constant.PriceAndCost && !eq.IsDisabled && isSelected)
                    isDatePickerEnabled = true;
            });
            PriceEffectiveDate = allSelected ? PriceEffectiveDate : PriceEffectiveDateOriginal;
            UpdateOkButtonState();
            dialogRef.Refresh();
        }

        public async Task RefreshDataAsync()
        {
            var businessCase = await Client.GetFromJsonAsync<PlanSched.Shared.Service.Schema.BusinessCase>(string.Format(ConfigurationUI.api_Plan_GetLatestBusinessCase, RefineryModel.DomainNamespace.DestinationApplication.Id, Service.Model.State.Forecast.Description()));
            var isPriceEffectiveDateUpdatedInDb = false;

            UserName = await ActiveUser.GetNameAsync() ?? RefineryModel.UpdatedBy;
            RefineryModel.CorrelationId = SessionService.GetCorrelationId();

            var interfaces = GetInterfacesList();
            var interfaceDictionary = interfaces.ToDictionary(i => i.InterfaceName, i => i.IsSelected);
            var updatePriceSelection = interfaces.FirstOrDefault(i => i.InterfaceName == Constant.PriceAndCost);

            if (PriceEffectiveDateOriginal.Value.Date == PriceEffectiveDate.Value.Date)
                updatePriceSelection.IsSelected = false;

            var requestUri = string.Format(ConfigurationUI.RefreshInterfaceData, businessCase.Id, PriceEffectiveDate, null, UserName, Service.Model.State.Forecast.Description());
            var response = await Client.PostAsJsonAsync(requestUri, interfaceDictionary);

            if (response != null)
                isPriceEffectiveDateUpdatedInDb = Convert.ToBoolean(response.Content.ReadAsStringAsync().Result);
            if (isPriceEffectiveDateUpdatedInDb)
            {
                isModalVisible = false;
                await InvokeAsync(() =>
                {
                    StateHasChanged();
                });

                var userSelectedRole = UtilityUI.GetUserRoleFromURI(NavigationManager.Uri);
                var businessCaseId = businessCase.Id;
                NavigateToRefineryPremiseProduct(userSelectedRole, businessCaseId);
            }
            else
            {
                isModalVisible = false;
                StatusMessageContent = ConfigurationUI.msg_RefreshDataFailure;
                StatusPopup = true;
                await InvokeAsync(() =>
                {
                    StateHasChanged();
                });
            }
        }

        public void InterfaceCheckboxChange(object value)
        {
            allSelected = false;
            var interfaces = GetInterfacesList();
            var isPriceSelected = interfaces.Any(o => o.InterfaceName == Constant.PriceAndCost && o.IsSelected);
            if (isPriceSelected)
                isDatePickerEnabled = true;
            else
            {
                isDatePickerEnabled = false;
                PriceEffectiveDate = (DateTime)PriceEffectiveDateOriginal;
            }
            var allInterfaceSelected = interfaces.All(i => i.IsSelected);

            if (allInterfaceSelected)
                allSelected = true;
            UpdateOkButtonState();
            dialogRef.Refresh();
        }

        private void UpdateOkButtonState()
        {
            var interfaces = GetInterfacesList();
            _isOkEnabled = interfaces.Any(i => i.IsSelected && !i.IsDisabled);
        }

        private List<ROInterfaces> GetInterfacesList()
        {
            return InterfaceListRefineryPremise;
        }

        private List<ROInterfaces> CreateInterfaceList(List<string> constants, List<string> disabledInterfaces = null)
        {
            disabledInterfaces ??= new List<string>();
            return constants.Select(constant => new ROInterfaces(constant)
            {
                IsDisabled = disabledInterfaces.Contains(constant)
            }).ToList();
        }

        private void PriceEffectiveDateChange()
        {
            var interfaces = GetInterfacesList();
            var isPriceSelected = interfaces.Where(o => o.InterfaceName == Constant.PriceAndCost).FirstOrDefault().IsSelected;
            if (!isPriceSelected)
                PriceEffectiveDate = PriceEffectiveDateOriginal;
        }
    }
}
