using Telerik.Blazor.Components;

namespace MPC.PlanSched.UI.Shared
{
    public interface IPopupService
    {
        void Register(TelerikNotification notificationRef);
        void ShowNotification(NotificationType type, string message);
    }
}