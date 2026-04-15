using System.Diagnostics.CodeAnalysis;

namespace MPC.PlanSched.UI.ViewModel
{
    /// <summary>
    /// It contains policy of user roles 
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class UserRolePolicy
    {
        public string Policy { get; set; } = string.Empty;
        public List<string> Role { get; set; } = new List<string>();
    }
}