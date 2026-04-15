using Microsoft.AspNetCore.Components;
using Telerik.Blazor;
using Telerik.Blazor.Components;

namespace MPC.PlanSched.UI.Shared
{
    public interface INotificationWrapper
    {
        void Show(NotificationModel model);
    }
}
