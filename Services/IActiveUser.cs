namespace MPC.PlanSched.UI.Services
{
    public interface IActiveUser
    {
        UserSettings UserSettings { get; }
        Task<string?> GetEmailAddressAsync();
        Task<string?> GetNameAsync();
        bool IsInRole(string role);
        Task<bool> IsAuthorizedAsync(string policy);
    }
}