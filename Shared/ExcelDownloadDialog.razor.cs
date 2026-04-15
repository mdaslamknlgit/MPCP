using Microsoft.AspNetCore.Components;
using MPC.PlanSched.Model;
using MPC.PlanSched.Shared.Common;
using MPC.PlanSched.Shared.Common.Extensions;

namespace MPC.PlanSched.UI.Shared
{
    public partial class ExcelDownloadDialog
    {
        [Parameter]
        public EventCallback OnConfirm { get; set; }
        [Parameter]
        public EventCallback<PriceType> SelectedPriceTypeChanged { get; set; }
        [Parameter]
        public PriceType SelectedPriceType
        {
            get => _selectedPriceType;
            set
            {
                if (_selectedPriceType != value)
                {
                    _selectedPriceType = value;
                    SelectedPriceTypeChanged.InvokeAsync(_selectedPriceType);
                }
            }
        }
        public List<PriceTypeModel> PriceTypes
        {
            get => _priceTypes ??= new List<PriceTypeModel>
            {
               new PriceTypeModel { Text = PriceType.SettleCost.Description(), Value = PriceType.SettleCost },
               new PriceTypeModel { Text = PriceType.MidCost.Description(), Value = PriceType.MidCost }
            };
        }
        private PriceType _selectedPriceType = PriceType.SettleCost;
        private List<PriceTypeModel> _priceTypes;
        private Dialog _excelDialog;

        public void Open() => _excelDialog.Open();

        public void Close() => _excelDialog.Close();

        private async Task ConfirmDownload()
        {
            if (OnConfirm.HasDelegate)
            {
                await OnConfirm.InvokeAsync();
            }
        }
    }
}
