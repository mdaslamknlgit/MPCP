using static MPC.PlanSched.UI.UtilityUI;
using Telerik.Blazor;
using Telerik.Blazor.Components;
namespace MPC.PlanSched.UI.Shared
{
    public class PopupService : IPopupService
    {
        private INotificationWrapper? _notificationRef;
        private readonly Queue<(NotificationType type, string message)> _pending = new();

        public void Register(TelerikNotification notificationRef) => Register(new TelerikNotificationWrapper(notificationRef));

        public void Register(INotificationWrapper notificationWrapper)
        {
            _notificationRef = notificationWrapper;
            // Flush pending notifications
            while (_pending.Count > 0)
            {
                var (type, message) = _pending.Dequeue();
                ShowNotification(type, message);
            }
        }

        public void ShowNotification(NotificationType type, string message)
        {
            if (_notificationRef == null)
            {
                _pending.Enqueue((type, message));
                return;
            }

            var themeColor = type switch
            {
                NotificationType.Normal => ThemeConstants.Notification.ThemeColor.Primary,
                NotificationType.Success => ThemeConstants.Notification.ThemeColor.Success,
                NotificationType.Error => ThemeConstants.Notification.ThemeColor.Error,
                _ => ThemeConstants.Notification.ThemeColor.Primary
            };

            _notificationRef.Show(new NotificationModel()
            {
                Text = message,
                ThemeColor = themeColor,
                Closable = true,
                ShowIcon = true
            });
        }
    }
}
