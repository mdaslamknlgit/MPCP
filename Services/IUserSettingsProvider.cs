namespace MPC.PlanSched.UI.Services
{
    public interface IUserSettingsProvider
    {
        event EventHandler Changed;
        ValueTask<UserSettings> GetAsync();
        Task SaveAsync();
    }
}