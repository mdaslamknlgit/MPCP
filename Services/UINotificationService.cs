using Microsoft.Extensions.Logging;
using MPC.PlanSched.Model;
using MPC.PlanSched.Service;
using MPC.PlanSched.Shared.Common;
using MPC.PlanSched.Shared.Common.Service;
using MPC.PlanSched.UI.ViewModel;

namespace MPC.PlanSched.UI.Services
{
    public class UINotificationService(IHttpClientWrapper httpClientWrapper, ILogger<UINotificationService> logger) : IUINotificationService
    {
        private readonly IHttpClientWrapper _httpClientWrapper = httpClientWrapper;
        private readonly ILogger<UINotificationService> _logger = logger;

        public NotificationRequestUI BuildNotificationRequest(string email) => new()
        {
            CreatedByUserEmailId = email,
            BusinessAreaCode = Constant.RO,
            OccurenceTimeStart = DateTime.Now.AddDays(Constant.TimeWindowLength).Date.ToString(PlanNSchedConstant.DateFormat),
            OccurenceTimeEnd = DateTime.Now.Date.AddDays(1).ToString(PlanNSchedConstant.DateFormat)
        };

        public void SetNotificationStatus(List<NotificationEventsModelUI> notifications) =>
            notifications.ForEach(notification =>
                notification.NotificationStatus = notification.eventData.TryGetValue(PlanNSchedConstant.Success, out var value)
                    ? value.ToString()
                    : GetNotificationExceptionStatus(notification));

        public List<NotificationEventsModelUI> FilterNotificationsByRole(List<NotificationEventsModelUI> notifications, string role) =>
            role switch
            {
                PlanNSchedConstant.DPOEngineer => FilterByApplicationName(notifications, PlanNSchedConstant.DPO),
                PlanNSchedConstant.RVCOEngineer => FilterByApplicationName(notifications, PlanNSchedConstant.PIMS),
                PlanNSchedConstant.Manager => FilterByMultipleApplicationNames(notifications, [PlanNSchedConstant.PIMS, PlanNSchedConstant.DPO]),
                _ => []
            };

        public List<NotificationEventsModelUI> FilterBackcastingNotificationsByRole(List<NotificationEventsModelUI> notifications, string role, string[] entities, string planName) =>
            role switch
            {
                PlanNSchedConstant.DPOEngineer => FilterByApplicationName(notifications, PlanNSchedConstant.DPO),
                PlanNSchedConstant.RVCOEngineer => FilterByRVCOCriteria(notifications, entities, planName),
                PlanNSchedConstant.Manager => FilterByMultipleApplicationNames(notifications, [PlanNSchedConstant.PIMS, PlanNSchedConstant.DPO]),
                _ => []
            };

        /// <summary>
        /// Fetches and filters notifications using a provided filter strategy.
        /// Encapsulates common logic for notification retrieval and filtering.
        /// </summary>
        /// <param name="email">User email address for the notification request</param>
        /// <param name="filterStrategy">Strategy function to apply filtering after fetching notifications</param>
        /// <returns>Result containing filtered notifications, error, and status messages</returns>
        public async Task<NotificationFetchResult> FetchAndFilterNotificationsAsync(
            string email,
            Func<List<NotificationEventsModelUI>, List<NotificationEventsModelUI>> filterStrategy)
        {

            if (string.IsNullOrEmpty(email))
                return new();

            try
            {
                var notificationRequest = BuildNotificationRequest(email);
                var client = await _httpClientWrapper.CreateHttpClientAsync(ConfigurationUI.NotificationServiceScope);
                var notificationResponseUI = UtilityUI.GetNotifications(notificationRequest, client);

                if (notificationResponseUI?.notificationEventList == null)
                {
                    var errorMessage = notificationResponseUI?.error ?? "No notifications returned from service";
                    return new NotificationFetchResult
                    {
                        Error = errorMessage,
                        Status = notificationResponseUI?.status ?? "Failed",
                        Notifications = []
                    };
                }

                SetNotificationStatus(notificationResponseUI.notificationEventList);

                return new NotificationFetchResult
                {
                    Error = notificationResponseUI.error ?? string.Empty,
                    Status = notificationResponseUI.status ?? string.Empty,
                    Notifications = filterStrategy(notificationResponseUI.notificationEventList)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching notifications for email: {Email}", email);
                return new NotificationFetchResult
                {
                    Error = "Error while getting notifications",
                    Status = "Failed"
                };
            }
        }

        private static string GetNotificationExceptionStatus(NotificationEventsModelUI notification) =>
            notification.eventData.TryGetValue(PlanNSchedConstant.Exception, out var exceptionValue)
                ? exceptionValue?.ToString() ?? string.Empty
                : string.Empty;

        private static List<NotificationEventsModelUI> FilterByApplicationName(List<NotificationEventsModelUI> notifications, string applicationName) =>
            [.. notifications
                .Where(x => !string.IsNullOrEmpty(x.DestinationApplicationName) &&
                            x.DestinationApplicationName.Contains(applicationName))];

        private static List<NotificationEventsModelUI> FilterByRVCOCriteria(List<NotificationEventsModelUI> notifications, string[] entities, string planName) =>
            [.. notifications
                .Where(x => !string.IsNullOrEmpty(x.DestinationApplicationName) &&
                            x.DestinationApplicationName.Contains(PlanNSchedConstant.PIMS) &&
                            !string.IsNullOrEmpty(x.impact) &&
                            x.impact.Contains(PlanNSchedConstant.Mdm) &&
                            entities.Any(r => x.EntityName != null && x.EntityName.Contains(r)) &&
                            !string.IsNullOrEmpty(planName) && x.PlanName == planName)];

        private static List<NotificationEventsModelUI> FilterByMultipleApplicationNames(List<NotificationEventsModelUI> notifications, string[] applicationNames) =>
            [.. notifications
                .Where(x => !string.IsNullOrEmpty(x.DestinationApplicationName) &&
                            applicationNames.Any(appName => x.DestinationApplicationName.Contains(appName)))];
    }
}
