using MPC.PlanSched.Model;

namespace MPC.PlanSched.UI.ViewModel
{
    public class NotificationFetchResult
    {
        public List<NotificationEventsModelUI> Notifications { get; set; } = [];
        public string Error { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
