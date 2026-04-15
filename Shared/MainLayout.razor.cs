using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace MPC.PlanSched.UI.Shared
{
    public partial class MainLayout
    {
        public NavigationDrawer NavDrawer { get; set; } = default!;
        public string SelectedRole { get; set; } = string.Empty;
        public ErrorBoundary? ErrorBoundary { get; set; }
    }
}
