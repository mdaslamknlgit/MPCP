using MPC.PlanSched.Model;
namespace MPC.PlanSched.UI.Shared
{
    public interface IExcelCommon
    {
        public Task<string?> GetExcelBase64ByRegion(RegionModel region, ApplicationArea area);

    }
}
