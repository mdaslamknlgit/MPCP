using System.ComponentModel;
using MPC.PlanSched.Service;
using MPC.PlanSched.Shared.Common.Attributes;

namespace MPC.PlanSched.UI
{
    public enum ApplicationArea
    {
        [Description(PlanNSchedConstant.DistributionPlanning)]
        [AppDescription(PlanNSchedConstant.DPO)]
        distributionplanning,

        [Description(PlanNSchedConstant.RegionalPlanning)]
        [AppDescription(PlanNSchedConstant.Pims)]
        regionalplanning,

        [Description(PlanNSchedConstant.RegionalBackcasting)]
        [AppDescription(PlanNSchedConstant.Pims)]
        regionalbackcasting,

        [Description(PlanNSchedConstant.RefineryPlanning)]
        [AppDescription(PlanNSchedConstant.PPIMS)]
        refineryplanning
    }

    public enum NotificationType
    {
        Normal,
        Success,
        Error
    }
}
