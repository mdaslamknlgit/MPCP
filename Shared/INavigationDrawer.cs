namespace MPC.PlanSched.UI.Shared
{
    public interface INavigationDrawer
    {
        public void SetExpanded(bool isExpanded);
        public void SetActiveMenuItemByUrl(string url);
    }
}
