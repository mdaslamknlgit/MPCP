using Microsoft.AspNetCore.Components;

namespace MPC.PlanSched.UI.Shared
{
    public partial class Dialog
    {
        [Parameter]
        public string Title { get; set; } = string.Empty;
        [Parameter]
        public RenderFragment ChildContent { get; set; } = default!;
        [Parameter]
        public EventCallback OnConfirm { get; set; }
        public bool IsVisible { get; set; }
        public void Open() => IsVisible = true;
        public void Close() => IsVisible = false;
    }
}
