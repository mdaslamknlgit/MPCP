using Microsoft.FeatureManagement;
using MPC.PlanSched.Common;
using MPC.PlanSched.Model;
using MPC.PlanSched.Shared.Common;
using MPC.PlanSched.Shared.Common.Extensions;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Data;
using Telerik.DataSource.Extensions;
namespace MPC.PlanSched.UI.Shared
{
    public class ExcelCommon : IExcelCommon
    {
        private readonly IFeatureManager _featureManager;
        private readonly IHttpClientFactory _clientFactory;
        private readonly IExcelUtility _excelUtilityModel;

        public ExcelCommon(IFeatureManager featureManager, IHttpClientFactory clientFactory, IExcelUtility excelUtilityModel)
        {
            _featureManager = featureManager;
            _clientFactory = clientFactory;
            _excelUtilityModel = excelUtilityModel;
        }

        public List<string> GetEntityNameListByRegion(RegionModel region)
        {
            var entityNameList = new List<string>();
            entityNameList.Add(Constant.Plan);
            entityNameList.Add(Constant.Demand);
            if (_featureManager.IsEnabledAsync(Constant.ManualData).GetAwaiter().GetResult())
            {
                if (region.ApplicationState != Service.Model.State.Actual.Description() && region.Type == Constant.RegionType_PIMS)
                    entityNameList.Add(Constant.DemandManualRefinery);
            }
            entityNameList.Add(Constant.Supply);
            entityNameList.Add(Constant.CrudeAvailSupply);
            entityNameList.Add(Constant.CrudeAvailGSupply);
            if (_featureManager.IsEnabledAsync(Constant.PlanPremise).GetAwaiter().GetResult())
            {
                if (region.ApplicationState != Service.Model.State.Actual.Description() && region.Type == Constant.RegionType_PIMS)
                {
                    entityNameList.Add(Constant.CapsBonuds);
                    entityNameList.Add(Constant.Transfer);
                    entityNameList.Add(Constant.Caps);
                    entityNameList.Add(Constant.Bounds);
                    entityNameList.Add(Constant.Proclim);
                    entityNameList.Add(Constant.Pinv);
                }
            }
            entityNameList.Add(Constant.Inventory);
            entityNameList.Add(Constant.InTransitInventory);
            entityNameList.Add(Constant.TranspCost);
            if (region.ApplicationState == Service.Model.State.Actual.Description())
            {
                entityNameList.Add(Constant.DInventory);
                entityNameList.Add(Constant.ActualTransfer);
            }
            return entityNameList;
        }

        public async Task<string?> GetExcelBase64ByRegion(RegionModel region, ApplicationArea area)
        {
            var base64 = string.Empty;

            var entityNameList = GetEntityNameListByRegion(region);
            var serializedTables = new ConcurrentDictionary<string, string>();

            var tasks = entityNameList.Select(async name =>
            {
                var entityName = name == Constant.Plan ? name : name.ToLower();
                serializedTables.AddRange(await GetSerializedTablesAsync(region, [entityName]));
            });

            await Task.WhenAll(tasks);

            if (area == ApplicationArea.regionalplanning && entityNameList.Contains(Constant.Caps))
            {
                var refineries = await GetRefineriesAsync();
                var refineryEntities = new[] { Constant.Caps, Constant.Bounds, Constant.Proclim, Constant.Pinv };

                if (!refineries.IsCollectionNullOrEmpty())
                {
                    var regionSpecificRefineryCodes = refineries.Where(x => x.RegionName == region.RegionName.Split(" ")[0]).Select(r => r.Name).ToList();
                    foreach (var entity in refineryEntities)
                    {
                        entityNameList.AddRange(regionSpecificRefineryCodes.Select(refineryCode => $"{refineryCode}_{entity}"));
                    }
                }

                entityNameList.RemoveAll(e => refineryEntities.Contains(e));
            }

            var result = entityNameList.ToDictionary(name => name, name =>
            serializedTables.TryGetValue(name, out var value) ? JsonConvert.DeserializeObject<DataTable?>(value) : new());

            if (result.IsCollectionNullOrEmpty()) return base64;

            var excelDataArray =
             area == ApplicationArea.distributionplanning ?
             _excelUtilityModel.ExcelDownloadModelForDPO(result) :
             _excelUtilityModel.ExcelDownloadModelForPIMS(region.RegionName, result);

            return Convert.ToBase64String(excelDataArray);
        }

        public async Task<Dictionary<string, string>?> GetSerializedTablesAsync(RegionModel region, List<string> entityNames)
        {
            var response = await _clientFactory.CreateClient("ExcelAPI").PostAsJsonAsync(ConfigurationUI.GetExcelPlanByInterface, new ExcelRequest { Region = region, EntityNames = entityNames });
            if (!response.IsSuccessStatusCode) return null;

            var responseContent = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<Dictionary<string, string>>(responseContent);
        }

        public async Task<List<RefineryModel>> GetRefineriesAsync() => await _clientFactory.CreateClient("ExcelAPI").GetFromJsonAsync<List<RefineryModel>>(string.Format(ConfigurationUI.GetRefineries));
    }
}
