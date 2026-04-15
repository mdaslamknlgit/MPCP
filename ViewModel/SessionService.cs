using MPC.PlanSched.UI.Services;
using System.Text;

namespace MPC.PlanSched.UI.ViewModel
{
    public class SessionService : ISessionService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IActiveUser _activeUser;

        public SessionService(IHttpContextAccessor httpContextAccessor, IActiveUser activeUser)
        {
            _httpContextAccessor = httpContextAccessor;
            _activeUser = activeUser;
        }

        public string GetCorrelationId()
        {
            var correlationId = string.Empty;
            var httpContext = _httpContextAccessor?.HttpContext;
            if (httpContext?.Session?.TryGetValue("UserCorrelationId", out var listBytes) ?? false)
            {
                if (listBytes != null)
                {
                    var userName = _activeUser.GetNameAsync().Result;
                    correlationId = Encoding.UTF8.GetString(listBytes) + "@" + userName ?? string.Empty;
                }
            }
            else
            {
                correlationId = Guid.NewGuid().ToString();
                var usersessionIdBytes = Encoding.UTF8.GetBytes(correlationId);
                httpContext?.Session?.Set("UserCorrelationId", usersessionIdBytes);
            }
            return correlationId;
        }

        public void SetCorrelationId(string correlationId)
        {
            var httpContext = _httpContextAccessor?.HttpContext;
            var usersessionIdBytes = Encoding.UTF8.GetBytes(correlationId);
            httpContext?.Session?.Set("UserCorrelationId", usersessionIdBytes);
        }

        public string GetLocalTimezoneName()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext.Session.TryGetValue("LocalTimezoneNameData", out var sessionData))
            {
                return Encoding.UTF8.GetString(sessionData);
            }
            return string.Empty;
        }

        public void SetLocalTimezoneName(string localtimezonename)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var localtimezoneBytes = Encoding.UTF8.GetBytes(localtimezonename);
            httpContext.Session.Set("LocalTimezoneNameData", localtimezoneBytes);
        }

        public string GetPlanType()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext.Session.TryGetValue("PlanTypeData", out var sessionData))
            {
                return Encoding.UTF8.GetString(sessionData);
            }
            return string.Empty;
        }

        public void SetPlanType(string planType)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var planTypeBytes = Encoding.UTF8.GetBytes(planType);
            httpContext.Session.Set("PlanTypeData", planTypeBytes);
        }
    }
}