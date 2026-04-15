using Microsoft.AspNetCore.Components;
using Telerik.Blazor;
using Telerik.Blazor.Components;

namespace MPC.PlanSched.UI.Shared
{
    public class TelerikNotificationWrapper(TelerikNotification notification) : INotificationWrapper
    {
        private readonly TelerikNotification _notification = notification;

        public void Show(NotificationModel model)
        {
            _notification.Show(model);
        }
    }
}