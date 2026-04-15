using System.Runtime.CompilerServices;
using System.Text;
using MPC.PlanSched.Model;
using MPC.PlanSched.Shared.Common;
using MPC.PlanSched.Shared.Common.Extensions;
using MPC.PlanSched.UI.Shared;

namespace MPC.PlanSched.UI
{
    public static class UIExtensions
    {
        public static ServiceData SetupServiceData(this RequestedPeriodModel reqPeriodModel, string entityName, string eventSource)
        {
            var serviceData = new ServiceData();
            serviceData.CorrelationId = reqPeriodModel?.CorrelationId;
            serviceData.DestinationApplication = reqPeriodModel?.DomainNamespace?.DestinationApplication?.Name;
            serviceData.RegionName = reqPeriodModel?.DomainNamespace?.DestinationApplication?.Name?.Split(Constant.EmptySpace)[0];
            serviceData.PlanName = reqPeriodModel?.BusinessCase?.Name;
            serviceData.PeriodId = reqPeriodModel?.PeriodID;
            serviceData.PeriodName = reqPeriodModel?.PeriodName;
            serviceData.StartDate = reqPeriodModel?.DateTimeRange?.FromDateTime;
            serviceData.EndDate = reqPeriodModel?.DateTimeRange?.ToDateTime;
            serviceData.CurrentUser = reqPeriodModel?.CreatedBy;
            serviceData.IsNorthDPO = reqPeriodModel?.DomainNamespace?.DestinationApplication?.Name?.Trim() == Constant.NorthDPO;
            serviceData.SBSuccessful = reqPeriodModel?.DomainNamespace?.DestinationApplication?.Name + Constant.EmptySpace + eventSource +
                Constant.SuccessMsg + " at " + DateTime.Now + " by " + serviceData.CurrentUser + " for period " + serviceData.PeriodName;
            serviceData.SBFailed = serviceData.CurrentUser + eventSource + Constant.ErrorMsg +
                " at " + DateTime.Now + " by " + serviceData.CurrentUser + " for period " + serviceData.PeriodName;
            serviceData.EntityType = entityName;

            serviceData.SBError = new StringBuilder();
            serviceData.PriceEffectiveDate = reqPeriodModel?.PriceEffectiveDate;
            serviceData.EventSource = eventSource;

            if (string.IsNullOrEmpty(serviceData.CurrentUser)) serviceData.CurrentUser = Constant.DefaultUser;
            return serviceData;
        }


        /// <summary>
        /// Logs the error and shows a notification using PopupService.
        /// </summary>
        /// <param name="logger">ILogger instance</param>
        /// <param name="popupService">PopupService instance</param>
        /// <param name="ex">Exception to log</param>
        /// <param name="methodName">Method name for logging context</param>
        public static void LogErrorAndNotify(this ILogger logger, IPopupService popupService, Exception ex, [CallerMemberName] string methodName = "")
        {
            logger.LogMethodError(ex, $"Error occurred in {methodName} method.");
            popupService.ShowNotification(NotificationType.Error, ex.GetErrorMessage());
        }

        /// <summary>
        // Logs the warning and shows a notification using PopupService.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="popupService"></param>
        /// <param name="warningMessage"></param>
        /// <param name="methodName"></param>
        public static void LogWarningAndNotify(this ILogger logger, IPopupService popupService, string warningMessage, [CallerMemberName] string methodName = "")
        {
            logger.LogMethodWarning(warningMessage, methodName);
            popupService.ShowNotification(NotificationType.Error, warningMessage);
        }
    }
}
