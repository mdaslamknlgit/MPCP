using System.Diagnostics.CodeAnalysis;

namespace MPC.PlanSched.UI.ViewModel
{
    [ExcludeFromCodeCoverage]
    public class CookieService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CookieService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public string GetThemePreference()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            return httpContext.Request.Cookies["userThemePreference"];
        }
    }
}