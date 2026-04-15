namespace MPC.PlanSched.UI
{
    /// <summary>
    /// This page is using to get constants from configuration UI
    /// </summary>
    public static class ConfigurationUI
    {
        private static IConfiguration _config;
        public static void Build_ConfigurationUI(IConfiguration configuration)
        {
            _config = configuration;
        }
        public static string Get_AzSupplyFunction => _config["AppSettings:api_azGetSupplyfunction"] ?? string.Empty;
        public static string Get_AzCostFunction => _config["AppSettings:api_azGetPricefunction"] ?? string.Empty;
        public static string domainNamespaceTypeDP => _config["AppSettings:domainNamespaceTypeDP"] ?? string.Empty;
        public static string domainNamespaceTypeRP => _config["AppSettings:domainNamespaceTypeRP"] ?? string.Empty;
        public static string PriceTypeName => _config["AppSettings:PriceTypeName"] ?? string.Empty;
        public static string TranspCostTypeName => _config["AppSettings:TranspCostTypeName"] ?? string.Empty;
        public static IConfiguration TranspCostType => _config.GetSection("AppSettings:TranspCostType");
        public static string CostTypeName => _config["AppSettings:CostTypeName"] ?? string.Empty;
        public static string DemandTypeName => _config["AppSettings:DemandTypeName"] ?? string.Empty;
        public static string SupplyTypeName => _config["AppSettings:SupplyTypeName"] ?? string.Empty;
        public static string CrudeSupplyTypeName => _config["AppSettings:CrudeSupplyTypeName"] ?? string.Empty;
        public static string OnlyShowBasedOnCurrentInventory => _config["AppSettings:OnlyShowBasedOnCurrentInventory"] ?? string.Empty;
        public static string RefreshDataDialogMessage => _config["AppSettings:RefreshDataDialogMessage"] ?? string.Empty;
        public static string RefineryPremiseRefreshDataDialogMessage => _config["AppSettings:RefineryPremiseRefreshDataDialogMessage"] ?? string.Empty;
        public static string SouthBackgroundImages => _config["AppSettings:SouthBackgroundImages"] ?? string.Empty;
        public static int CommentsMaxLength => int.TryParse(_config["AppSettings:CommentsMaxLength"], out var value) ? value : 0;
        public static int PlanDescriptionMaxLength => int.TryParse(_config["AppSettings:PlanDescriptionMaxLength"], out var value) ? value : 0;
        public static bool IsMidtermEnabled => bool.TryParse(_config["AppSettings:IsMidtermEnabled"], out var value) && value;
        public static string MidtermRegion => _config["AppSettings:MidtermRegion"] ?? string.Empty;



        #region Common
        public static string msg_successfull => _config["AppSettings:msg_successfull"] ?? string.Empty;
        public static string msg_Failure => _config["AppSettings:msg_Failure"] ?? string.Empty;
        public static string MultiPeriodDataLoadingInProgressMessage => _config["AppSettings:MultiPeriodDataLoadingInProgressMessage"] ?? string.Empty;
        public static string MultiPeriodDataLoadingCompletedMessage => _config["AppSettings:MultiPeriodDataLoadingCompletedMessage"] ?? string.Empty;
        public static string DataLoadingInProgressMessage => _config["AppSettings:DataLoadingInProgressMessage"] ?? string.Empty;
        public static string DataLoadingCompletedMessage => _config["AppSettings:DataLoadingCompletedMessage"] ?? string.Empty;
        public static string FooterAppDetails => _config["AppSettings:FooterAppDetails"] ?? string.Empty;
        public static string msg_RefreshDataFailure => _config["AppSettings:msg_RefreshDataFailure"] ?? string.Empty;
        #endregion Common
        #region Api

        public static string api_GetOverrideValueTypes => _config["AppSettings:api_GetOverrideValueTypes"] ?? string.Empty;
        public static string api_GetRefinery => _config["AppSettings:api_GetRefinery"] ?? string.Empty;
        public static string api_IsInventoryAvailable => _config["AppSettings:api_IsInventoryAvailable"] ?? string.Empty;
        public static string api_IsRegionalCapsBoundsAvailable => _config["AppSettings:api_IsRegionalCapsBoundsAvailable"] ?? string.Empty;
        public static string api_IsRegionalTransferAvailable => _config["AppSettings:api_IsRegionalTransferAvailable"] ?? string.Empty;
        public static string api_Plan_GetAllBusinessProcess => _config["AppSettings:api_Plan_GetAllBusinessProcess"] ?? string.Empty;
        public static string api_Plan_GetAllRefineryPremiseActivePlans => _config["AppSettings:api_GetAllRefineryPremiseLatestActivePlans"] ?? string.Empty;
        public static string api_Plan_GetAllRefineryPremiseHistoricalPlans => _config["AppSettings:api_GetAllRefineryPremiseHistoricalPlans"] ?? string.Empty;
        public static string api_Plan_GetDomainNamespaceId => _config["AppSettings:api_GetDomainNamespaceId"] ?? string.Empty;
        public static string api_Plan_GetRegions => _config["AppSettings:api_Plan_GetRegions"] ?? string.Empty;
        public static string api_Plan_GetRegion => _config["AppSettings:api_Plan_GetRegion"] ?? string.Empty;
        public static string api_Plan_GetRegionByBusinessCaseId => _config["AppSettings:api_Plan_GetRegionByBusinessCaseId"] ?? string.Empty;
        public static string api_RefineryPremise_IsCapsAvailable => _config["AppSettings:api_RefineryPremise_IsCapsAvailable"] ?? string.Empty;
        public static string api_RefineryPremise_IsBoundsAvailable => _config["AppSettings:api_RefineryPremise_IsBoundsAvailable"] ?? string.Empty;
        public static string api_RefineryPremise_IsProclimAvailable => _config["AppSettings:api_RefineryPremise_IsProclimAvailable"] ?? string.Empty;
        public static string api_RefineryPremise_IsPinvAvailable => _config["AppSettings:api_RefineryPremise_IsPinvAvailable"] ?? string.Empty;
        public static string api_RefineryPremise_GetCapsConstraint => _config["AppSettings:api_RefineryPremise_GetCapsConstraint"] ?? string.Empty;
        public static string api_RefineryPremise_GetBoundsConstraint => _config["AppSettings:api_RefineryPremise_GetBoundsConstraint"] ?? string.Empty;
        public static string api_RefineryPremise_GetProclimConstraint => _config["AppSettings:api_RefineryPremise_GetProclimConstraint"] ?? string.Empty;
        public static string api_RefineryPremise_GetPinvConstraint => _config["AppSettings:api_RefineryPremise_GetPinvConstraint"] ?? string.Empty;
        public static string api_Plan_GetBusinessCaseByBusinessCaseId => _config["AppSettings:api_Plan_GetBusinessCaseByBusinessCaseId"] ?? string.Empty;
        public static string api_Plan_GetRefineryModelByBusinessCaseId => _config["AppSettings:api_Plan_GetRefineryModelByBusinessCaseId"] ?? string.Empty;
        public static string api_Plan_GetLatestBusinessCase => _config["AppSettings:api_Plan_GetLatestBusinessCase"] ?? string.Empty;
        public static string api_Plan_GetAllActiveBusinessCases => _config["AppSettings:api_Plan_GetAllActiveBusinessCases"] ?? string.Empty;
        public static string api_Plan_ArchivePlan => _config["AppSettings:api_Plan_ArchivePlan"] ?? string.Empty;
        public static string api_Plan_DeletePlan => _config["AppSettings:api_Plan_DeletePlan"] ?? string.Empty;
        public static string api_Plan_GetAllActiveActualBusinessCasesAsync => _config["AppSettings:api_Plan_GetAllActiveActualBusinessCasesAsync"] ?? string.Empty;
        public static string api_Plan_GetAllInactiveActualBusinessCasesAsync => _config["AppSettings:api_Plan_GetAllInactiveActualBusinessCasesAsync"] ?? string.Empty;
        public static string api_Plan_GetAllInActiveBusinessCasesAsync => _config["AppSettings:api_Plan_GetAllInActiveBusinessCasesAsync"] ?? string.Empty;
        public static string api_Plan_UpdateBusinessCaseFlagAsync => _config["AppSettings:api_Plan_UpdateBusinessCaseFlagAsync"] ?? string.Empty;
        public static string Api_Plan_SharePlan => _config["AppSettings:api_SharePlan"] ?? string.Empty;
        public static string Api_Plan_CreateFromShared => _config["AppSettings:api_CreateFromSharedPlan"] ?? string.Empty;
        public static string Api_FlagPlanType => _config["AppSettings:api_FlagPlanType"] ?? string.Empty;
        public static string GetPeriods => _config["AppSettings:api_GetPeriods"] ?? string.Empty;
        public static string GetPeriodsByBusinessCaseId => _config["AppSettings:api_GetPeriodsByBusinessCaseId"] ?? string.Empty;
        public static string SavePeriods => _config["AppSettings:api_SavePeriods"] ?? string.Empty;
        public static string GetDemandAndPrice => _config["AppSettings:api_GetDemandAndPrice"] ?? string.Empty;
        public static string GetSupplyAndCost => _config["AppSettings:api_GetSupplyAndCost"] ?? string.Empty;
        public static string GetSupplyPriceIds => _config["Appsettings:api_GetSupplyPriceIds"] ?? string.Empty;
        public static string GetPublishedDemandAndPrice => _config["AppSettings:api_GetPublishedDemandAndPrice"] ?? string.Empty;
        public static string GetRegionalCapsBounds => _config["AppSettings:api_GetRegionalCapsBounds"] ?? string.Empty;
        public static string GetRegionalTransfer => _config["AppSettings:api_GetRegionalTransfer"] ?? string.Empty;
        public static string SaveDemandAndPrice => _config["AppSettings:api_SaveDemandAndPrice"] ?? string.Empty;
        public static string SaveSupplyAndCost => _config["AppSettings:api_SaveSupplyAndCost"] ?? string.Empty;
        public static string SaveRegionalCapsBounds => _config["AppSettings:api_SaveRegionalCapsBounds"] ?? string.Empty;
        public static string SaveRegionalTransfer => _config["AppSettings:api_SaveRegionalTransfer"] ?? string.Empty;
        public static string Get_AzPriceFunction => _config["AppSettings:api_azGetPricefunction"] ?? string.Empty;
        public static string Get_AzDemandFunction => _config["AppSettings:api_azGetDemandfunction"] ?? string.Empty;
        public static string Get_AzRefineryPremiseFunction => _config["AppSettings:api_azGetRefineryPremiseFunction"] ?? string.Empty;
        public static string Get_AzRegionalPremiseFunction => _config["AppSettings:api_azGetRegionalPremiseFunction"] ?? string.Empty;
        public static string Get_AzRefInventoryFunction => _config["AppSettings:api_azGetRefInventoryfunction"] ?? string.Empty;
        public static string Get_AzTelInventoryFunction => _config["AppSettings:api_azGetTelInventoryfunction"] ?? string.Empty;
        public static string Get_AzTransferFunction => _config["AppSettings:api_azGetTransferfunction"] ?? string.Empty;
        public static string GetInventory => _config["AppSettings:api_GetInventory"] ?? string.Empty;
        public static string SaveInventory => _config["AppSettings:api_SaveInventory"] ?? string.Empty;
        public static string IsDemandAvailable => _config["AppSettings:api_IsDemandAvailable"] ?? string.Empty;
        public static string IsPriceAvailable => _config["AppSettings:api_IsPriceAvailable"] ?? string.Empty;
        public static string IsSupplyAvailable => _config["AppSettings:api_IsSupplyAvailable"] ?? string.Empty;
        public static string GetTransportationCost => _config["AppSettings:api_GetTransportationCost"] ?? string.Empty;
        public static string Get_AzTransportationCostFunction => _config["AppSettings:api_azGetTransportationCostfunction"] ?? string.Empty;
        public static string SaveTransportationCost => _config["AppSettings:api_SaveTransportationCost"] ?? string.Empty;
        public static string SaveTransferAndCost => _config["AppSettings:api_SaveTransferAndCost"] ?? string.Empty;
        public static string GetInTransitInventory => _config["AppSettings:api_GetInTransitInventory"] ?? string.Empty;
        public static string SaveInTransitInventory => _config["AppSettings:api_SaveInTransitInventory"] ?? string.Empty;
        public static string Get_AzInTransitInventoryFunction => _config["AppSettings:api_azGetInTransitInventoryfunction"] ?? string.Empty;
        public static string RefreshInterfaceData => _config["AppSettings:api_RefreshInterfaceData"] ?? string.Empty;
        public static string PriceEffectiveDate => _config["AppSettings:api_PriceEffectiveDate"] ?? string.Empty;
        public static string UpdatedBy => _config["AppSettings:api_UpdatedBy"] ?? string.Empty;
        public static string GetExcelPlanByInterface => _config["AppSettings:api_GetExcelPlanByInterface"] ?? string.Empty;

        public static string GetExcelPlanByRefinery => _config["AppSettings:api_GetExcelPlanByRefinery"] ?? string.Empty;
        public static string GetTransferAndCost => _config["AppSettings:api_GetTransferAndCost"] ?? string.Empty;
        public static string IsTransferAvailable => _config["AppSettings:api_IsTransferAvailable"] ?? string.Empty;
        public static string GetRefineries => _config["AppSettings:api_GetRefineries"] ?? string.Empty;
        public static string IsLatestPlan => _config["AppSettings:api_IsLatestPlan"] ?? string.Empty;
        #endregion Api
        #region Planning Dashboard
        public static string GetCOEReportUrl => _config["AppSettings:COEReportUrl"] ?? string.Empty;
        public static string GetPIMSAnalysisToolUrl => _config["AppSettings:PIMSAnalysisToolUrl"] ?? string.Empty;
        public static string GetDPOAnalysisToolUrl => _config["AppSettings:DPOAnalysisToolUrl"] ?? string.Empty;
        public static string GetMultiPlantMonthlyAveragesUrl => _config["AppSettings:MultiPlantMonthlyAveragesComparisonUrl"] ?? string.Empty;
        #endregion Planning Dashboard
        public static string getValue(string description)
        {
            var value = _config[$"AppSettings:{description}"];
            var message = string.IsNullOrEmpty(value) ? string.Empty : value;
            return message;
        }

        #region Notification UI
        public static string GetNotifications => _config["AppSettings:api_GetNotifications"] ?? string.Empty;
        public static string NotificationServiceScope => _config["AppSettings:NotificationServiceScope"] ?? string.Empty;
        #endregion Notification UI

        public static bool IsRefineryInventoryEnabled => !bool.TryParse(_config["AppSettings:IsRefineryInventoryEnabled"], out var value) || value;
    }
}
