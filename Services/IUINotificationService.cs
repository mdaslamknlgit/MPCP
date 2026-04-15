using MPC.PlanSched.Model;
using MPC.PlanSched.Shared.Common.Service;
using MPC.PlanSched.UI.ViewModel;

namespace MPC.PlanSched.UI.Services
{
    public interface IUINotificationService
    {
        NotificationRequestUI BuildNotificationRequest(string email);
        void SetNotificationStatus(List<NotificationEventsModelUI> notifications);
        List<NotificationEventsModelUI> FilterNotificationsByRole(List<NotificationEventsModelUI> notifications, string role);
        List<NotificationEventsModelUI> FilterBackcastingNotificationsByRole(List<NotificationEventsModelUI> notifications, string role, string[] entities, string planName);
        Task<NotificationFetchResult> FetchAndFilterNotificationsAsync(
            string email,
            Func<List<NotificationEventsModelUI>, List<NotificationEventsModelUI>> filterStrategy);
    }
}
