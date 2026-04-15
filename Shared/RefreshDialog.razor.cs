using Microsoft.AspNetCore.Components;
using Microsoft.FeatureManagement;
using MPC.PlanSched.Model;
using MPC.PlanSched.Service;
using MPC.PlanSched.Shared.Common;
using MPC.PlanSched.Shared.Common.Extensions;
using MPC.PlanSched.Shared.Service.Schema;
using MPC.PlanSched.UI.ViewModel;
using Telerik.Blazor.Components;

namespace MPC.PlanSched.UI.Shared
{
    public partial class RefreshDialog
    {
        public bool isModalVisible = false;
        public DateTime? PriceEffectiveDate { get; set; } = DateTime.UtcNow.AddDays(-1);
        public DateTime? SupplyEffectiveDate { get; set; } = DateTime.UtcNow.AddDays(-1);
        public DateTime? PriceEffectiveDateOriginal { get; set; } = DateTime.UtcNow.AddDays(-1);
        public DateTime? SupplyEffectiveDateOriginal { get; set; } = DateTime.UtcNow.AddDays(-1);
        public bool isRegionalNav = false, isDistributionNav = false, isBackcastingNav = false;
        public bool allSelected = false, isDatePickerEnabled = false;
        public bool isAvailsCostDatePickerEnabled = false, isAvailsVolumeDatePickerEnabled = false;
        [Inject]
        ISessionService? SessionService { get; set; }
        [Inject]
        public IFeatureManager FeatureManager { get; set; } = default!;
        public List<ROInterfaces>? InterfaceListRp { get; set; }
        public List<ROInterfaces>? InterfaceListDp { get; set; }
        public List<ROInterfaces>? InterfaceListBackcasting { get; private set; }
        public TelerikDialog? dialogRef;
        private bool _isOkEnabled = false;

        public async void OpenAsync(RegionModel regionModelObj)
        {
            var interfacesRp = new List<string>
            {
                Constant.PriceAndCost,
                Constant.Demand,
                Constant.Supply,
                Constant.AvailsVolume,
                Constant.TerminalInventory,
                Constant.RefineryInventory,
            };

            var regionalCapsBounds = regionModelObj.RegionName == Constant.NorthRegion ? Constant.RegionalBonuds : Constant.RegionalCaps;
            if (await FeatureManager.IsEnabledAsync(FeatureFlags.PlanPremise))
            {
                interfacesRp.Add(regionalCapsBounds);
                interfacesRp.Add(Constant.RegionalTransfer);
                interfacesRp.Add(Constant.RefineryPremise);
            }

            InterfaceListRp = CreateInterfaceList(interfacesRp);
            InterfaceListDp = CreateInterfaceList(new List<string>
            {
                Constant.PriceAndCost,
                Constant.Demand,
                Constant.Supply,
                Constant.OpeningInventory,
                Constant.InTransit_Inventory,
                Constant.TransportationCost,
            });
            if(ConfigurationUI.IsMidtermEnabled)
            {
                InterfaceListDp.RemoveAll(x => x.InterfaceName == Constant.OpeningInventory || x.InterfaceName == Constant.InTransit_Inventory || x.InterfaceName == Constant.TransportationCost);
                InterfaceListRp.RemoveAll(x => x.InterfaceName == Constant.AvailsVolume || x.InterfaceName == Constant.TerminalInventory || x.InterfaceName == Constant.RefineryInventory || x.InterfaceName == Constant.RefineryPremise);
            }

            var pageUri = NavigationManager.Uri;
            var disabledInterfaceList = GetDisabledInterfaceList(pageUri);
            InterfaceListBackcasting = CreateInterfaceList(new List<string>
            {
                Constant.PriceAndCost,
                Constant.Demand,
                Constant.Supply,
                Constant.TerminalInventory,
                Constant.RefineryInventory,
                Constant.TransferVolume,
                Constant.AvailsVolume,
                Constant.AvailsCost,
            }, disabledInterfaceList);

            if (regionModelObj.ApplicationState == Service.Model.State.Actual.Description())
                isBackcastingNav = true;

            isRegionalNav = regionModelObj.DomainNamespace?.DestinationApplication.Name.Contains(PlanNSchedConstant.PIMS) ?? false;
            isDistributionNav = regionModelObj.DomainNamespace?.DestinationApplication.Name.Contains(PlanNSchedConstant.DPO) ?? false;
            Client = ClientFactory.CreateClient("WebAPI");
            Client.Timeout = TimeSpan.FromSeconds(600);
            UserName = await ActiveUser.GetNameAsync() ?? RegionModel.UpdatedBy;
            regionModelObj.IsHierarchy = false;
            RequestedPeriods = UtilityUI.GetPeriodData(RequestedPeriods, regionModelObj, SessionService, Client, UserName);
            PriceEffectiveDate = DateTime.Parse(RequestedPeriods[0].PriceEffectiveDate?.ToString("MM/dd/yy"));
            SupplyEffectiveDate = DateTime.Parse(RequestedPeriods[0].SupplyEffectiveDate?.ToString("MM/dd/yy"));
            PriceEffectiveDateOriginal = PriceEffectiveDate;
            SupplyEffectiveDateOriginal = SupplyEffectiveDate;
            isModalVisible = true;
            await InvokeAsync(() =>
            {
                StateHasChanged();
            });
            RegionModel = regionModelObj;
            UpdateOkButtonState();
        }

        public async Task RefreshDataAsync()
        {
            var businessCaseObj = RegionModel.ApplicationState != Service.Model.State.Actual.Description()
                                      ? await Client.GetFromJsonAsync<BusinessCase>(string.Format(ConfigurationUI.api_Plan_GetLatestBusinessCase, RegionModel.DomainNamespace.DestinationApplication.Id, RegionModel.ApplicationState))
                                      : RegionModel.BusinessCase;

            var isPriceEffectiveDateUpdatedInDb = false;
            UserName = await ActiveUser.GetNameAsync() ?? RegionModel.UpdatedBy;
            RegionModel.CorrelationId = SessionService.GetCorrelationId();

            var interfaces = GetInterfacesList();
            var updatePriceSelection = interfaces.FirstOrDefault(i => i.InterfaceName == Constant.PriceAndCost);

            if (RegionModel.ApplicationState == Service.Model.State.Forecast.Description() && PriceEffectiveDateOriginal.Value.Date == PriceEffectiveDate.Value.Date)
                updatePriceSelection.IsSelected = false;

            else if (RegionModel.ApplicationState == Service.Model.State.Actual.Description())
            {
                var updateAvailsCostSelection = interfaces.FirstOrDefault(i => i.InterfaceName == Constant.AvailsCost);

                if (PriceEffectiveDateOriginal.Value.Date == PriceEffectiveDate.Value.Date)
                    updateAvailsCostSelection.IsSelected = false;
                else
                    updateAvailsCostSelection.IsSelected = true;
            }

            var currentLocalTime = DateTime.Now.TimeOfDay;
            SupplyEffectiveDate = SupplyEffectiveDate?.Add(currentLocalTime);

            var interfaceDictionary = interfaces.ToDictionary(i => i.InterfaceName, i => i.IsSelected);
            var requestUri = string.Format(ConfigurationUI.RefreshInterfaceData, businessCaseObj.Id, PriceEffectiveDate, SupplyEffectiveDate?.ToUniversalTime(), UserName, RegionModel.ApplicationState);
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
                var businessCaseId = RegionModel.BusinessCase.Id;

                if (isRegionalNav)
                {
                    if (isBackcastingNav)
                    {
                        var pageUri = NavigationManager.Uri;
                        var redirectToSamePage = pageUri.Contains(Constant.Active, StringComparison.InvariantCultureIgnoreCase) ? false : true;
                        if (redirectToSamePage)
                            NavigationManager.NavigateTo(pageUri, true);
                        else
                            NavigateToBackcastingProduct(userSelectedRole, businessCaseId, false);
                    }
                    else
                        NavigateToProductRP(userSelectedRole, businessCaseId);
                }
                else if (isDistributionNav)
                    NavigateToProductDP(userSelectedRole, businessCaseId);
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

        private void AllPlanCheckBoxChange(object value)
        {
            var isSelected = (bool)value;
            var interfaces = GetInterfacesList();
            isDatePickerEnabled = false;
            isAvailsCostDatePickerEnabled = false;
            isAvailsVolumeDatePickerEnabled = false;
            interfaces.ForEach(eq =>
            {
                if (eq.IsSelected != isSelected && !eq.IsDisabled)
                {
                    eq.IsSelected = isSelected;
                }
                if (eq.InterfaceName == Constant.PriceAndCost && !eq.IsDisabled && isSelected)
                    isDatePickerEnabled = true;
                if (eq.InterfaceName == Constant.PriceAndCost && !eq.IsDisabled && isSelected)
                    isAvailsCostDatePickerEnabled = true;
                if (eq.InterfaceName == Constant.AvailsVolume && !eq.IsDisabled && isSelected)
                    isAvailsVolumeDatePickerEnabled = true;
            });
            PriceEffectiveDate = allSelected ? PriceEffectiveDate : PriceEffectiveDateOriginal;
            UpdateOkButtonState();
            dialogRef.Refresh();
        }

        private void InterfaceCheckboxChange(object value)
        {
            allSelected = false;
            var interfaces = GetInterfacesList();
            var isForecast = RegionModel.ApplicationState == Service.Model.State.Forecast.Description();

            bool IsSelected(string name) => interfaces.Any(o => o.InterfaceName == name && o.IsSelected);

            if (isForecast)
            {
                isDatePickerEnabled = IsSelected(Constant.PriceAndCost);
                if (!isDatePickerEnabled)
                    PriceEffectiveDate = (DateTime)PriceEffectiveDateOriginal;            
            }
            else
            {
                isAvailsCostDatePickerEnabled = IsSelected(Constant.PriceAndCost);
                if (!isAvailsCostDatePickerEnabled)
                    PriceEffectiveDate = (DateTime)PriceEffectiveDateOriginal;
               
            }

            isAvailsVolumeDatePickerEnabled = IsSelected(Constant.AvailsVolume);
            if (!isAvailsVolumeDatePickerEnabled)
                SupplyEffectiveDate = (DateTime)SupplyEffectiveDateOriginal;

            allSelected = interfaces.All(i => i.IsSelected);
            UpdateOkButtonState();
            dialogRef.Refresh();
        }

        private void UpdateOkButtonState()
        {
            var interfaces = GetInterfacesList();
            _isOkEnabled = interfaces.Any(i => i.IsSelected && !i.IsDisabled);
        }

        private void PriceEffectiveDateChange()
        {
            var interfaces = GetInterfacesList();
            var isPriceSelected = interfaces.Where(o => o.InterfaceName == Constant.PriceAndCost).FirstOrDefault().IsSelected;
            if (!isPriceSelected)
                PriceEffectiveDate = PriceEffectiveDateOriginal;

        }

        private void AvailsCostEffectiveDateChange()
        {
            var interfaces = GetInterfacesList();
            var isAvailsCostSelected = interfaces.Any(o => o.InterfaceName == Constant.PriceAndCost && o.IsSelected);
            if (!isAvailsCostSelected)
                PriceEffectiveDate = (DateTime)PriceEffectiveDateOriginal;
        }

        private void AvailsVolumeEffectiveDateChange()
        {
            var interfaces = GetInterfacesList();
            var isAvailsVolumeSelected = interfaces.Where(o => o.InterfaceName == Constant.AvailsVolume).FirstOrDefault().IsSelected;
            if (!isAvailsVolumeSelected)
                SupplyEffectiveDate = (DateTime)SupplyEffectiveDateOriginal;
        }

        private List<ROInterfaces> GetInterfacesList()
        {
            if (isDistributionNav)
            {
                return InterfaceListDp;
            }
            if (isBackcastingNav)
            {
                return InterfaceListBackcasting;
            }
            return InterfaceListRp;
        }

        private List<ROInterfaces> CreateInterfaceList(List<string> constants, List<string> disabledInterfaces = null)
        {
            disabledInterfaces ??= new List<string>();
            return constants.Select(constant => new ROInterfaces(constant)
            {
                IsDisabled = disabledInterfaces.Contains(constant)
            }).ToList();
        }
        private List<string> GetDisabledInterfaceList(string pageUri)
        {
            var interfaceName = pageUri.Split(PlanNSchedConstant.UriDelimiter)[3];

            return interfaceName switch
            {
                PlanNSchedConstant.BackcastingOptimizationAvailsCostPageAttribute => new List<string>
                {
                    Constant.Demand,
                    Constant.Supply,
                    Constant.TerminalInventory,
                    Constant.RefineryInventory,
                    Constant.TransferVolume
                },
                PlanNSchedConstant.BackcastingProductSupplyCostPageAttribute => new List<string>
                {
                    Constant.Demand,
                    Constant.AvailsVolume,
                    Constant.TerminalInventory,
                    Constant.RefineryInventory,
                    Constant.TransferVolume
                },
                PlanNSchedConstant.BackcastingProductDemandPricePageAttribute => new List<string>
                {
                    Constant.Supply,
                    Constant.AvailsVolume,
                    Constant.TerminalInventory,
                    Constant.RefineryInventory,
                    Constant.TransferVolume
                },
                PlanNSchedConstant.BackcastingRefineryInventoryPageAttribute => new List<string>
                {
                    Constant.Supply,
                    Constant.AvailsVolume,
                    Constant.Demand,
                    Constant.PriceAndCost,
                    Constant.TransferVolume
                },
                PlanNSchedConstant.BackcastingTerminalInventoryPageAttribute => new List<string>
                {
                    Constant.Supply,
                    Constant.AvailsVolume,
                    Constant.Demand,
                    Constant.PriceAndCost,
                    Constant.TransferVolume
                },
                PlanNSchedConstant.BackcastingTransferCostPageAttribute => new List<string>
                 {
                     Constant.Supply,
                     Constant.AvailsVolume,
                     Constant.Demand,
                     Constant.TerminalInventory,
                     Constant.RefineryInventory,
                 },
                _ => new List<string>()
            };
        }
    }
}
