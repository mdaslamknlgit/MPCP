using Microsoft.JSInterop;
using System.ComponentModel;

namespace MPC.PlanSched.UI.Services
{
    public sealed class UserSettingsProvider : IUserSettingsProvider
    {
        private readonly IJSRuntime _runtime;
        private UserSettings _settings;
        public event EventHandler? Changed;

        public UserSettingsProvider(IHttpContextAccessor httpContextAccessor, IJSRuntime runtime)
        {
            _settings = ParseSettings(httpContextAccessor);
            _runtime = runtime;
        }

        private UserSettings ParseSettings(IHttpContextAccessor httpContextAccessor)
        {
            var cookies = httpContextAccessor.HttpContext?.Request.Cookies ?? throw new ArgumentException("There is no HttpContext");
            var settings = new UserSettings
            {
                PlanType = cookies["RegionalOptimization.PlanningAndScheduling.Settings.PlanType"] ?? string.Empty,
            };

            settings.PropertyChanged += OnPropertyChangedAsync;

            return settings;
        }

        public async ValueTask<UserSettings> GetAsync() => _settings;

        public async Task SaveAsync()
        {
            await _runtime.InvokeVoidAsync("setCookie", "RegionalOptimization.PlanningAndScheduling.Settings.PlanType", _settings.PlanType);
        }

        /// <summary>
        /// Automatically persist the settings when a property changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void OnPropertyChangedAsync(object? sender, PropertyChangedEventArgs e)
        {
            await SaveAsync();
        }
    }
}
