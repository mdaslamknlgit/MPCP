using System.Diagnostics.CodeAnalysis;

namespace MPC.PlanSched.UI.ViewModel
{
    [ExcludeFromCodeCoverage]
    public class LocationFilterOption
    {
        public string LocationName { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string ToLocationName { get; set; } = string.Empty;
        public string FromLocationName { get; set; } = string.Empty;
    }

    [ExcludeFromCodeCoverage]
    public class ProductFilterOption
    {
        public string ProductName { get; set; } = string.Empty;
        public string CommodityName { get; set; } = string.Empty;
        public string ConstraintTag { get; set; } = string.Empty;
    }
}
