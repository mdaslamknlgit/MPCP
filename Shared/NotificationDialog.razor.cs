using System.Net.Http.Headers;
using Microsoft.AspNetCore.Components;
using MPC.PlanSched.Model;
using MPC.PlanSched.Service;
using MPC.PlanSched.Shared.Common;
using MPC.PlanSched.Shared.Common.Extensions;
using MPC.PlanSched.Shared.Common.Service;
using MPC.PlanSched.Shared.Service.Schema;
using MPC.PlanSched.UI.Services;

namespace MPC.PlanSched.UI.Shared
{
    public partial class NotificationDialog
    {
        [Inject]
        public ILogger Logger { get; set; } = default!;
        [Inject]
        public IUINotificationService UINotificationService { get; set; } = default!;
        [Parameter]
        public string Title { get; set; } = string.Empty;
        [Parameter]
        public RenderFragment ChildContent { get; set; } = default!;
        [CascadingParameter]
        public string SelectedRole { get; set; }
        private RegionModel Region { get; set; }
        public string NotificationError { get; set; } = string.Empty;
        public string NotificationStatus { get; set; } = string.Empty;
        public List<NotificationEventsModelUI> Notifications { get; set; } = [];
        private const string _getBackcastingNotificationsAsync = "GetBackcastingNotificationsAsync";

        public async Task OpenAsync(RegionModel region, string selectedRole, string entityName)
        {
            Region = region;
            SelectedRole = selectedRole;
            string[] entities;
            entities = (entityName == PlanNSchedConstant.Inventory
             ? region.Refinery
             : entityName ?? string.Empty)
             .Split(Constant.CommaSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            Notifications = await GetBackcastingNotificationsAsync(entities);
            StatusPopup = true;
            UnlockLoading();
            await InvokeAsync(StateHasChanged);
        }

        public async Task<List<NotificationEventsModelUI>> GetBackcastingNotificationsAsync(string[] entities)
        {
            Logger.LogMethodInfo($"Start of method {_getBackcastingNotificationsAsync}.");
            LockLoading();

            try
            {
                return await FetchAndFilterBackcastingNotificationsAsync(entities);
            }
            catch (Exception ex)
            {
                Logger.LogMethodError(ex, $"Exception occurred in {_getBackcastingNotificationsAsync}.");
                NotificationError = "Error while getting notifications";
                return [];
            }
            finally
            {
                UnlockLoading();
                Logger.LogMethodInfo($"End of method {_getBackcastingNotificationsAsync}.");
            }
        }

        private async Task<List<NotificationEventsModelUI>> FetchAndFilterBackcastingNotificationsAsync(string[] entities)
        {
            var email = await ActiveUser.GetEmailAddressAsync();
            if (string.IsNullOrEmpty(email))
            {
                Logger.LogMethodWarning($"Email address is null or empty in {nameof(FetchAndFilterBackcastingNotificationsAsync)}. Returning empty notification list.");
                return [];
            }

            var planName = Region?.BusinessCase?.Name ?? string.Empty;

            var filterStrategy = new Func<List<NotificationEventsModelUI>, List<NotificationEventsModelUI>>(
                notifications => UINotificationService.FilterBackcastingNotificationsByRole(notifications, SelectedRole.Trim(), entities, planName)
            );

            var result = await UINotificationService.FetchAndFilterNotificationsAsync(
                email,
                filterStrategy
            );

            NotificationError = result.Error;
            NotificationStatus = result.Status;
            return result.Notifications;
        }
    }
}
