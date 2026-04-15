using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace MPC.PlanSched.UI.Services
{
    public class ActiveUser : IActiveUser
    {
        private readonly ClaimsPrincipal _principal;
        private readonly IUserSettingsProvider _settingsProvider;
        private readonly IAuthorizationService _authorizationService;
        private UserSettings? _userSettings;

        public ActiveUser(ClaimsPrincipal principal, IUserSettingsProvider settingsProvider, IAuthorizationService authorizationService)
        {
            _principal = principal;
            _settingsProvider = settingsProvider;
            _authorizationService = authorizationService;
        }

        public Task<string?> GetNameAsync() => Task.FromResult(_principal.Claims.FirstOrDefault(x => x.Type == "name")?.Value);

        public Task<string?> GetEmailAddressAsync() => Task.FromResult(_principal.Claims.FirstOrDefault(x => x.Type == "verified_primary_email")?.Value);

        public bool IsInRole(string role) => _principal.IsInRole(role);

        public async Task<bool> IsAuthorizedAsync(string policy)
        {
            var authorizationResult = await _authorizationService.AuthorizeAsync(_principal, policy);

            return authorizationResult.Succeeded;
        }

        public UserSettings UserSettings => _userSettings ??= _settingsProvider.GetAsync().GetAwaiter().GetResult();
    }
}
