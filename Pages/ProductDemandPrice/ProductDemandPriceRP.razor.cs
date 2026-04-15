using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.JSInterop;
using MPC.PlanSched.Model;
using MPC.PlanSched.Service;
using MPC.PlanSched.Shared.Common;
using MPC.PlanSched.Shared.Common.Extensions;
using MPC.PlanSched.UI.ViewModel;
using Telerik.Blazor.Components;
using Telerik.DataSource.Extensions;

namespace MPC.PlanSched.UI.Pages.ProductDemandPrice
{
    [Authorize(Policy = PlanNSchedConstant.ViewPims)]
    public partial class ProductDemandPriceRP
    {
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);
            if (firstRender)
            {
                LockLoading();
                StateHasChanged();
                IsAggregatedTier = false;
                Client = ClientFactory.CreateClient("WebAPI");
                Client.Timeout = TimeSpan.FromSeconds(600);
                OverrideTypes = await UtilityUI.GetOverrideTypes(Client);
                RegionModel = await UtilityUI.GetRegionByBusinessCaseIdAsync(BusinessCaseId, SessionService.GetCorrelationId(), Client);
                if (RegionModel != null)
                {
                    RegionTitle = RegionModel.BusinessCase.Name;
                    PlanUpdatedOn = "Last Saved: " + (RegionModel.BusinessCase.UpdatedOn?.ToString("MM/dd/yy") ?? "");
                    RegionName = RegionModel.RegionName;
                    PlanDescription = RegionModel.BusinessCase.Description;
                    ReadOnlyFlag = IsHistoricalData = IsHistoricalPlan;
                    if (!ConfigurationUI.IsMidtermEnabled)
                        PlanType = RegionModel.BusinessCase.PlanType ?? string.Empty;
                }

                await GetPeriodsAsync();

                CommonHelper.UpdateBaggage(SessionService.GetCorrelationId(),
                    RegionModel?.BusinessCase?.Id,
                    RequestedPeriods?.FirstOrDefault()?.PeriodID,
                    RegionModel?.DomainNamespace?.DestinationApplication.Name);

                await GetDemandAndPriceDataFromPublisherAsync();

                if (RegionName != null && !RegionName.Contains(Constant.SouthRegion))
                {
                    IsTierVisible = true;
                    StateHasChanged();
                }
                await GetLineChartDataAsync();
                UnlockLoading();
                StateHasChanged();
            }
        }

        public async Task ExcelDownloadAsync()
        {
            try
            {
                logger?.LogMethodStart();
                LockLoading();
                var data = await _excelCommon.GetExcelBase64ByRegion(RegionModel, ApplicationArea.regionalplanning);
                var fileName = RegionModel?.DomainNamespace?.DestinationApplication.Name + "_Planning.xlsx";
                await JsRuntime.InvokeVoidAsync("saveAsFile", data, fileName, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            }
            catch (Exception ex)
            {
                logger?.LogMethodError(ex, "Error occurred in ExcelDownload method for PIMS.");
            }
            finally
            {
                UnlockLoading();
                logger?.LogMethodEnd();
            }
        }

        #region Chart

        public async Task GetLineChartDataAsync()
        {
            logger.LogMethodStart();
            LockLoading();
            StateHasChanged();
            TotalMinDemandLineChartXAxisLabels = new object[Periods.Count];
            var periodData = new List<ProductDemandAndPrice>();

            var tasks = Periods.Select(async (period, i) =>
            {
                /// Get data for all periods in plan
                /// Added false as parameter value in below method, as aggregation by tier is not needed for XPIMS region.
                periodData = await UtilityUI.GetProductDemandAndPriceDataAsync(false, period.PeriodID, periodData, Periods, Client);

                if (periodData == null || !periodData.Any())
                {
                    UnlockLoading();
                    StateHasChanged();
                    logger.LogMethodWarning("Demand data is not loaded in GetLineChartData");
                    return;
                }

                var totalMinDemandPerProduct = periodData.GroupBy(x => x.ProductDescription)
                    .Select(x => new
                    {
                        Name = x.Key,
                        Total = x.Sum(y => y.MinDemandOverride).Value
                    })
                    .ToList();

                foreach (var product in totalMinDemandPerProduct)
                {
                    List<object> data;
                    var index = TotalMinDemandLineChartSeries.FindIndex(x => x.name == product.Name);

                    if (index != -1)
                    {
                        data = TotalMinDemandLineChartSeries[index].data;
                        data[i] = product.Total;
                        TotalMinDemandLineChartSeries[index].data = data;
                    }
                    else
                    {
                        data = new List<object>(Periods.Count);
                        for (var j = 0; j < data.Capacity; j++)
                        {
                            data.Add(0);
                        }

                        data[i] = product.Total;
                        TotalMinDemandLineChartSeries.Add(new LineChartSeries { name = product.Name, data = data });
                    }
                }
                TotalMinDemandLineChartXAxisLabels[i] = period.PeriodDisplayName;
            });

            await Task.WhenAll(tasks);

            TotalMinDemandLineChartSeriesVisible.AddRange(TotalMinDemandLineChartSeries);

            UnlockLoading();
            StateHasChanged();
            logger.LogMethodEnd();
        }

        /// <summary>
        /// Extract product description filter conditions from Grid State object and pass them to FilterLineChart
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private Task OnGridStateChanged(GridStateEventArgs<ProductDemandAndPrice> args)
        {
            if (args.PropertyName != "FilterDescriptors")
            {
                return Task.CompletedTask;
            }

            if (args.GridState.FilterDescriptors.Count == 0)
            {
                ResetLineChart();
                return Task.CompletedTask;
            }

            var filterDescriptors = args.GridState.FilterDescriptors
                .SelectMany(x => (x as Telerik.DataSource.CompositeFilterDescriptor).FilterDescriptors)
                .Select(x => new { (x as Telerik.DataSource.FilterDescriptor).ConvertedValue, (x as Telerik.DataSource.FilterDescriptor).Member });

            var lineChartFilters = new List<string>();
            foreach (var filterDescriptor in filterDescriptors)
            {
                if (filterDescriptor.Member == "ProductDescription")
                {
                    lineChartFilters.Add(filterDescriptor.ConvertedValue.ToString());
                }
            }

            FilterLineChart(lineChartFilters);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Reset line chart to include all products
        /// </summary>
        /// <returns></returns>
        private void ResetLineChart()
        {
            TotalMinDemandLineChartSeriesVisible.Clear();
            TotalMinDemandLineChartSeriesVisible.AddRange(TotalMinDemandLineChartSeries);
        }

        /// <summary>
        /// Display only products included in lineChartFilters
        /// </summary>
        /// <param name="lineChartFilters"></param>
        /// <returns></returns>
        private void FilterLineChart(List<string> lineChartFilters)
        {
            if (lineChartFilters.Count == 0)
            {
                ResetLineChart();
                return;
            }

            TotalMinDemandLineChartSeriesVisible.Clear();

            foreach (var filter in lineChartFilters)
            {
                var product = TotalMinDemandLineChartSeries.Find(x => x.name == filter);
                if (product != null)
                {
                    TotalMinDemandLineChartSeriesVisible.Add(product);
                }
            }
        }
        #endregion Chart       
    }
}