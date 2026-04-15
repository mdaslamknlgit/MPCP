namespace MPC.PlanSched.UI.ViewModel
{
    public interface ISessionService
    {
        string GetCorrelationId();
        void SetCorrelationId(string correlationId);
        string GetLocalTimezoneName();
        void SetLocalTimezoneName(string localtimezone);
        string GetPlanType();
        void SetPlanType(string planType);
    }
}