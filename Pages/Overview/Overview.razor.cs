using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MPC.PlanSched.Model;
using MPC.PlanSched.Service;
using MPC.PlanSched.Shared.Common.Extensions;
using MPC.PlanSched.Shared.Common.Service;
using MPC.PlanSched.UI.Services;
using Telerik.Blazor.Components;
using Telerik.SvgIcons;

namespace MPC.PlanSched.UI.Pages.Overview
{
    public class GridCardState
    {
        public bool Expanded { get; set; }
        public string ExpandIcon { get; set; } = default!;
        public TelerikAnimationContainer ContainerRef { get; set; } = default!;
    }

    public partial class Overview
    {
        [Parameter]
        public List<Alerts> Alerts { get; set; } = [];

        [Parameter]
        public List<JobStatus> JobStatus { get; set; } = [];

        [Parameter]
        public List<DataSync> DataSync { get; set; } = [];

        [Parameter]
        public List<UserActionFailures> ActionFailures { get; set; } = [];

        [Parameter]
        public string SelectedRole { get; set; } = string.Empty;

        public List<NotificationEventsModelUI> NotificationList { get; set; } = [];
        public string NotificationError { get; set; } = string.Empty;
        public string NotificationStatus { get; set; } = string.Empty;
        public TelerikGrid<NotificationEventsModelUI> GridNotificationRef { get; set; } = default!;
        [Inject]
        public IUINotificationService UINotificationService { get; set; } = default!;
        public List<GridCardState> GridCardStates { get; set; } = [
                new GridCardState { Expanded = false, ExpandIcon = SvgIcon.ChevronRight.ToString(), ContainerRef = new TelerikAnimationContainer() },
                new GridCardState { Expanded = false, ExpandIcon = SvgIcon.ChevronRight.ToString(), ContainerRef = new TelerikAnimationContainer() },
                new GridCardState { Expanded = false, ExpandIcon = SvgIcon.ChevronRight.ToString(), ContainerRef = new TelerikAnimationContainer() },
                new GridCardState { Expanded = false, ExpandIcon = SvgIcon.ChevronRight.ToString(), ContainerRef = new TelerikAnimationContainer() }
            ];

        private bool _uiLoading = false;
        private const string _getNotifications = "GetNotifications";

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                ///Setting LocalTimeZone Name in SessionService.
                var timeZoneLocalNameTemp = await JsRuntime.InvokeAsync<string>("getLocalTimeZoneName");
                var timeZoneLocalName = string.Empty;
                try
                {
                    var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneLocalNameTemp);
                    if (timeZone != null)
                        timeZoneLocalName = timeZone.StandardName;
                }
                catch (Exception)
                {
                    timeZoneLocalName = PlanNSchedConstant.DefaultTimeZone;
                }
                SessionService.SetLocalTimezoneName(timeZoneLocalName);
                logger.LogMethodInfo("Local Time zone Name: " + timeZoneLocalName);
            }
            await base.OnAfterRenderAsync(firstRender);
        }

        private async Task<List<NotificationEventsModelUI>> GetNotificationsAsync()
        {
            logger.LogMethodInfo("Start of method " + _getNotifications + ".");
            LockLoading();

            try
            {
                return await FetchAndFilterNotificationsAsync();
            }
            catch (Exception ex)
            {
                logger.LogMethodError(ex, "Exception occurred in " + _getNotifications + " method.");
                NotificationError = "Error while getting notifications";
                return [];
            }
            finally
            {
                UnlockLoading();
                logger.LogMethodInfo("End of method " + _getNotifications + ".");
            }
        }

        private async Task<List<NotificationEventsModelUI>> FetchAndFilterNotificationsAsync()
        {
            var email = await ActiveUser.GetEmailAddressAsync();
            if (string.IsNullOrEmpty(email))
            {
                logger.LogMethodWarning($"Email address is null or empty in {nameof(FetchAndFilterNotificationsAsync)}. Returning empty notification list.");
                return [];
            }

            var filterStrategy = new Func<List<NotificationEventsModelUI>, List<NotificationEventsModelUI>>(
                notifications => UINotificationService.FilterNotificationsByRole(notifications, SelectedRole.Trim())
            );

            var result = await UINotificationService.FetchAndFilterNotificationsAsync(
                email,
                filterStrategy
            );

            NotificationError = result.Error;
            NotificationStatus = result.Status;
            return result.Notifications;
        }

        private async Task ToggleAnimationContainerAsync(int cardIndex)
        {
            _uiLoading = true;
            StateHasChanged();
            var cardState = GridCardStates[cardIndex];
            if (cardState.Expanded)
            {
                await cardState.ContainerRef?.HideAsync();
                cardState.ExpandIcon = SvgIcon.ChevronRight.ToString();
            }
            else
            {
                if (cardIndex == 0 && NotificationList?.Count == 0)
                    NotificationList = await GetNotificationsAsync();
                await cardState.ContainerRef?.ShowAsync();
                cardState.ExpandIcon = SvgIcon.ChevronDown.ToString();
            }
            _uiLoading = false;
            cardState.Expanded = !cardState.Expanded;
            StateHasChanged();
        }
    }
}