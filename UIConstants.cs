namespace MPC.PlanSched.UI
{
    public static class UIConstants
    {
        public const string QuantityDigitPrecisionFormat = "0.000";
        public const string InventoryQuantityDigitPrecisionFormat = "0.000";
        public const string PriceDigitPrecisionFormat = "0.0000";
        public const string TransportationCostDigitPrecisionFormat = "0.0000";
        public const string LeadDaysDigitPrecisionFormat = "0";
        public const string PlanPremiseConstraintDigitalFormat = "0.000";

        public const string AbsoluteEntityValue = "Absolute";
        public const string OffsetEntityValue = "Offset";
        public const string PercentageEntityValue = "Percentage";
        public const string FactorEntityValue = "Factor";
        public const string PricingEntityType = "Pricing";
        public const string ROPricingEntityType = "ROPricing";
        public const string CrudeAvailExcel = "CrudeAvailExcel";
        public const string Terminal = "TERMINAL";
        public const string PeriodStartDate = "Period Start Date:";
        public const string LastPeriodEndDate = "Last Period End Date:";
        public const string Completed = "Completed";
        public const string Failed = "Failed";
        public const string Terminated = "Terminated";
        public const string Canceled = "Canceled";
        public const string Target = "Target";
        public const string Safety = "Safety";
        public const string TCost = "TCost";
        #region Interface
        public const string EditPlan = "Edit Plan:";
        public const string Plan = "Plan:";
        public const string PlanType = "Plan Type:";
        public const string PriceEffectiveDate = "Price Effective Date:";
        public const string SupplyEffectiveDate = "Crude Effective Date:";
        public const string PlanningPeriod = "Planning Period";
        public const string SortBy = "Sort By";
        public const string SelectPeriod = "Select Period:";
        public const string NoItemsAvailable = "No items available";
        public const string NoDataAvailable = "No Data Available.";
        public const int DurableFunctionInMintues = 5;
        public const int DelayMilliseconds = 3000;

        #endregion Interface

        #region Message
        public const string No = "No";
        public const string FailedToSaveData = "Fail to save the data.";
        public const string SuccessfullySavedData = "Data saved successfully.";
        public const string NoOverwriteRecord = "No overwrite record was found.";
        public const string FinalizedDataWarningMessage = "Regional data not finalized.";
        #endregion Message

        public const string RefineryPremiseConstraintDropdown = "Refinery Premise Tables:";
        public const string SelectRegionalPremiseConstraint = "Select Regional Premise Table";
        public const string RegionalPremiseConstraint = "Regional Premise Tables:";
        public const string KittyHawk = "KittyHawk";
        public const string PlannerManualExcel = "PlannerManualExcel";
        public const string Regional = "Regional";
        public const string Refinery = "Refinery";
    }
}
