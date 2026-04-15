using System.Diagnostics.CodeAnalysis;

namespace MPC.PlanSched.UI.ViewModel
{
    [ExcludeFromCodeCoverage]
    public class TopNavMenuItemModel
    {
        public string Text { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public bool Disabled { get; set; } = false;
        public bool CurrentPage { get; set; }
    }
}
