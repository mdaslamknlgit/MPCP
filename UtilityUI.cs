using System.ComponentModel;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MPC.PlanSched.Model;
using MPC.PlanSched.Service;
using MPC.PlanSched.Shared.Common;
using MPC.PlanSched.Shared.Common.Extensions;
using MPC.PlanSched.Shared.Notification.Model;
using MPC.PlanSched.Shared.Service.Schema;
using MPC.PlanSched.UI.Services;
using MPC.PlanSched.UI.ViewModel;
using Newtonsoft.Json;
using Telerik.DataSource.Extensions;
using Convert = System.Convert;
using RefineryPremiseConstraintType = MPC.PlanSched.Shared.Service.Schema.RefineryPremiseConstraintType;

namespace MPC.PlanSched.UI
{
    public static class UtilityUI
    {
        private static Dictionary<string, string> _refineryPremiseConstraintUrlMap = new Dictionary<string, string>
        {
            { RefineryPremiseConstraintType.Caps.Description(), ConfigurationUI.api_RefineryPremise_IsCapsAvailable },
            { RefineryPremiseConstraintType.Bounds.Description(), ConfigurationUI.api_RefineryPremise_IsBoundsAvailable },
            { RefineryPremiseConstraintType.Proclim.Description(), ConfigurationUI.api_RefineryPremise_IsProclimAvailable },
            { RefineryPremiseConstraintType.Pinv.Description(), ConfigurationUI.api_RefineryPremise_IsPinvAvailable }
        };

        private static Dictionary<string, string> _refineryPremiseConstraintGetUrlMap = new Dictionary<string, string>
        {
            { RefineryPremiseConstraintType.Caps.Description(), ConfigurationUI.api_RefineryPremise_GetCapsConstraint },
            { RefineryPremiseConstraintType.Bounds.Description(), ConfigurationUI.api_RefineryPremise_GetBoundsConstraint },
            { RefineryPremiseConstraintType.Proclim.Description(), ConfigurationUI.api_RefineryPremise_GetProclimConstraint },
            { RefineryPremiseConstraintType.Pinv.Description(), ConfigurationUI.api_RefineryPremise_GetPinvConstraint }
        };

        private static Dictionary<string, string> _regionalPremiseConstraintUrlMap = new Dictionary<string, string>
        {
            { RegionalPremiseConstraintType.RegionalCapsBounds.Description(), ConfigurationUI.api_IsRegionalCapsBoundsAvailable },
            { RegionalPremiseConstraintType.RegionalTransfer.Description(), ConfigurationUI.api_IsRegionalTransferAvailable },
        };

        private static Dictionary<string, string> _regionalPremiseConstraintGetUrlMap = new Dictionary<string, string>
        {
            {RegionalPremiseConstraintType.RegionalCapsBounds.Description(), ConfigurationUI.GetRegionalCapsBounds },
            {RegionalPremiseConstraintType.RegionalTransfer.Description(), ConfigurationUI.GetRegionalTransfer }
        };
        #region IsDataExist

        /// <summary>
        /// Checking the data available for these entities in DB
        /// </summary>
        /// <param name="period"></param>
        /// <param name="client"></param>
        /// <returns></returns>
        public static async Task<bool> IsDemandDataLoadedForPeriodAsync(RequestedPeriodModel period, HttpClient client)
        {
            var result = false;
            var responseResult = await client.PostAsJsonAsync(ConfigurationUI.IsDemandAvailable, period);
            if (responseResult != null)
            {
                var responseContent = await responseResult.Content.ReadAsStringAsync();
                result = responseContent == "true";
            }
            return result;
        }

        public static async Task<bool> IsPriceDataLoadedForPeriodAsync(RequestedPeriodModel period, HttpClient client)
        {
            var result = false;
            var responseResult = await client.PostAsJsonAsync(ConfigurationUI.IsPriceAvailable, period);
            if (responseResult != null)
            {
                var responseContent = await responseResult.Content.ReadAsStringAsync();
                result = responseContent == "true";
            }
            return result;
        }

        public static async Task<bool> IsSupplyDataLoadedForPeriodAsync(RequestedPeriodModel period, HttpClient client)
        {
            var result = false;

            var SupplyList = new List<ProductSupplyAndCost>();
            var responseResult = await client.PostAsJsonAsync(ConfigurationUI.IsSupplyAvailable, period);
            if (responseResult != null)
            {
                var responseContent = await responseResult.Content.ReadAsStringAsync();
                result = responseContent == "true";
            }
            return result;
        }

        public static async Task<bool> IsInventoryDataLoadedForPeriodByInvtSourceAsync(RequestedPeriodModel period, HttpClient client, string inventoryType)
            => await client.GetFromJsonAsync<bool>(string.Format(ConfigurationUI.api_IsInventoryAvailable, period.PeriodID, inventoryType));

        public static async Task<bool> IsTrasportationCostDataLoadedForPeriodAsync(RequestedPeriodModel period, HttpClient client)
        {
            var result = false;
            var responseResult = await client.PostAsJsonAsync(ConfigurationUI.IsPriceAvailable, period);
            if (responseResult != null)
            {
                var responseContent = await responseResult.Content.ReadAsStringAsync();
                if (responseContent == "true")
                {
                    result = true;
                }
            }

            return result;
        }

        public static async Task<Dictionary<string, bool>> IsRefineryPremiseDataLoadedForPeriodAsync(RequestedPeriodModel periodModel, string selectedConstraint, string refineryCode, HttpClient client)
        {
            var result = new Dictionary<string, bool>();
            foreach (var constraint in _refineryPremiseConstraintUrlMap)
            {
                var url = $"{constraint.Value}/{refineryCode}";
                var responseResult = await client.PostAsJsonAsync(url, periodModel);

                if (responseResult != null)
                {
                    var responseContent = await responseResult.Content.ReadAsStringAsync();
                    result.Add(constraint.Key, responseContent == "true");
                }
            }

            return result;
        }

        public static async Task<Dictionary<string, bool>> IsRegionalPremiseDataLoadedForPeriodAsync(RequestedPeriodModel periodModel, string selectedConstraint, HttpClient client)
        {
            var result = new Dictionary<string, bool>();
            foreach (var constraint in _regionalPremiseConstraintUrlMap)
            {
                var url = constraint.Value;
                var responseResult = await client.PostAsJsonAsync(url, periodModel);

                if (responseResult != null)
                {
                    var responseContent = await responseResult.Content.ReadAsStringAsync();
                    result.Add(constraint.Key, responseContent == "true");
                }
            }

            return result;
        }

        public static async Task<bool> IsTransferDataLoadedForPeriodAsync(RequestedPeriodModel periodModel, HttpClient client)
        {
            var result = false;
            var responseResult = await client.PostAsJsonAsync(ConfigurationUI.IsTransferAvailable, periodModel);
            if (responseResult != null)
            {
                var responseContent = await responseResult.Content.ReadAsStringAsync();
                result = responseContent == "true";
            }
            return result;
        }
        #endregion IsDataExsist

        #region SCRequest

        public static SCPriceRequest CreateSCPriceRequest(string sessionId, string senderAppName, string entityType, string refineryLocation, string regionName, string regionType, RequestedPeriodModel periodModel, string priceType, string priceIds = "")
        {
            var UserName = periodModel.CreatedBy;
            var priceRequestObj = new SCPriceRequest
            {
                SCPriceRequestHeader = new SCPriceRequestHeader
                {
                    DomainNamespace = new DomainNamespace
                    {
                        IdentifierType = "Name/Id",
                        SourceApplication = new SourceApplication
                        {
                            Name = senderAppName,
                            Description = senderAppName,
                            CreatedBy = UserName,
                            CreatedOn = DateTime.UtcNow,
                            UpdatedBy = UserName,
                            UpdatedOn = DateTime.UtcNow
                        },
                        DestinationApplication = new DestinationApplication
                        {
                            Name = periodModel.DomainNamespace.DestinationApplication.Name,
                            Description = periodModel.DomainNamespace.DestinationApplication.Name,
                            CreatedBy = UserName,
                            CreatedOn = DateTime.UtcNow,
                            UpdatedBy = UserName,
                            UpdatedOn = DateTime.UtcNow
                        }
                    },
                    BusinessContext = new BusinessContext { ApprovalState = "Approved", Location = "Garyville" },
                    Security = new Security
                    {
                        SenderContext = "PlanningScheduling UI Tool",
                        OriginalSourceContext = senderAppName
                    },
                    SenderInformation = new SenderInformation
                    {
                        SenderName = new SenderName
                        {
                            Name = UserName,
                            Description = UserName,
                            Id = 0
                        },
                        SenderDocumentID = Convert.ToString(sessionId),
                        SenderPublishDateTime = DateTime.UtcNow,
                        SentOn = periodModel.PriceEffectiveDate,
                        SessionID = Convert.ToString(sessionId)
                    }
                },
                SCPriceRequestBody = new SCPriceRequestBody
                {
                    RequestedPeriods = new RequestedPeriods
                    {
                        RequestedPeriod = new List<RequestedPeriod>
                        {
                            new RequestedPeriod
                            {
                                PeriodName = periodModel.PeriodName,
                                PeriodID = periodModel.PeriodID,
                                CreatedBy = UserName,
                                CreatedOn = DateTime.UtcNow,
                                ModifiedBy = UserName,
                                ModifiedOn = DateTime.UtcNow,
                                DateTimeRange = new DateTimeRange
                                {
                                    ToDateTime = periodModel.DateTimeRange.ToDateTime,
                                    FromDateTime = periodModel.DateTimeRange.FromDateTime
                                }
                            }
                        }
                    },
                    DataEntities = new DataEntities
                    {
                        DataEntity = new DataEntity
                        {
                            Name = priceType,
                            State = "SystemForecast",
                            Type = entityType,
                            Location = refineryLocation,
                            LocationType = regionName
                        }
                    },
                    BusinessCase = new BusinessCase
                    {
                        Name = periodModel.BusinessCase.Name,
                        Description = periodModel.BusinessCase.Name,
                        CreatedBy = UserName,
                        CreatedOn = DateTime.UtcNow,
                        UpdatedOn = DateTime.UtcNow,
                        UpdatedBy = UserName,
                        Id = periodModel.BusinessCase.Id
                    },
                    EffectiveDate = periodModel.PriceEffectiveDate,
                    PriceIds = priceIds,
                    State = periodModel.ApplicationState,
                    CreatedOrUpdatedOn = DateTime.UtcNow,
                    CreatedOrUpdatedBy = UserName
                }
            };
            return priceRequestObj;
        }

        public static SCPriceRequest CreateSCCostRequest(string sessionId, string senderAppName, string entityType, string refineryLocation, string regionName, string regionType, RequestedPeriodModel periodModel, string priceType, string priceIds)
        {
            var UserName = periodModel.CreatedBy;
            var priceRequestObj = new SCPriceRequest
            {
                SCPriceRequestHeader = new SCPriceRequestHeader
                {
                    DomainNamespace = new DomainNamespace
                    {
                        IdentifierType = "Name/Id",
                        SourceApplication = new SourceApplication
                        {
                            Name = senderAppName,
                            Description = senderAppName,
                            CreatedBy = UserName,
                            CreatedOn = DateTime.UtcNow,
                            UpdatedBy = UserName,
                            UpdatedOn = DateTime.UtcNow
                        },
                        DestinationApplication = new DestinationApplication
                        {
                            Name = periodModel.DomainNamespace.DestinationApplication.Name,
                            Description = periodModel.DomainNamespace.DestinationApplication.Name,
                            CreatedBy = UserName,
                            CreatedOn = DateTime.UtcNow,
                            UpdatedBy = UserName,
                            UpdatedOn = DateTime.UtcNow
                        }
                    },
                    BusinessContext = new BusinessContext { ApprovalState = "Approved", Location = "Garyville" },
                    Security = new Security
                    {
                        SenderContext = "PlanningScheduling UI Tool",
                        OriginalSourceContext = senderAppName
                    },
                    SenderInformation = new SenderInformation
                    {
                        SenderName = new SenderName
                        {
                            Name = UserName,
                            Description = UserName,
                            Id = 0
                        },
                        SenderDocumentID = Convert.ToString(sessionId),
                        SenderPublishDateTime = DateTime.UtcNow,
                        SentOn = periodModel.PriceEffectiveDate,
                        SessionID = Convert.ToString(sessionId)
                    }
                },
                SCPriceRequestBody = new SCPriceRequestBody
                {
                    RequestedPeriods = new RequestedPeriods
                    {
                        RequestedPeriod = new List<RequestedPeriod>
                        {
                            new RequestedPeriod
                            {
                                PeriodName = periodModel.PeriodName,
                                PeriodID = periodModel.PeriodID,
                                CreatedBy = UserName,
                                CreatedOn = DateTime.UtcNow,
                                ModifiedBy = UserName,
                                ModifiedOn = DateTime.UtcNow,
                                DateTimeRange = new DateTimeRange
                                {
                                    ToDateTime = periodModel.DateTimeRange.ToDateTime,
                                    FromDateTime = periodModel.DateTimeRange.FromDateTime
                                }
                            }
                        }
                    },
                    DataEntities = new DataEntities
                    {
                        DataEntity = new DataEntity
                        {
                            Name = priceType,
                            State = "SystemForecast",
                            Type = entityType,
                            Location = refineryLocation,
                            LocationType = regionName
                        }
                    },
                    BusinessCase = new BusinessCase
                    {
                        Name = periodModel.BusinessCase.Name,
                        Description = periodModel.BusinessCase.Name,
                        CreatedBy = UserName,
                        CreatedOn = DateTime.UtcNow,
                        UpdatedOn = DateTime.UtcNow,
                        UpdatedBy = UserName,
                        Id = periodModel.BusinessCase.Id
                    },
                    EffectiveDate = periodModel.PriceEffectiveDate,
                    State = periodModel.ApplicationState,
                    PriceIds = priceIds,
                    CreatedOrUpdatedOn = DateTime.UtcNow,
                    CreatedOrUpdatedBy = UserName
                }
            };
            return priceRequestObj;
        }

        public static SCDemandRequest CreateSCDemandRequest(string sessionId, string senderAppName, string entityType, string refineryLocation, string regionName, string regionType, RequestedPeriodModel periodModel)
        {
            var UserName = periodModel.CreatedBy;
            var demandRequestObj = new SCDemandRequest
            {
                SCDemandRequestHeader = new SCDemandRequestHeader
                {
                    DomainNamespace = new DomainNamespace
                    {
                        IdentifierType = "Name/Id",
                        SourceApplication = new SourceApplication
                        {
                            Name = senderAppName,
                            Description = senderAppName,
                            CreatedBy = UserName,
                            CreatedOn = DateTime.UtcNow,
                            UpdatedBy = UserName,
                            UpdatedOn = DateTime.UtcNow
                        },
                        DestinationApplication = new DestinationApplication
                        {
                            Name = periodModel.DomainNamespace.DestinationApplication.Name,
                            Description = periodModel.DomainNamespace.DestinationApplication.Name,
                            CreatedBy = UserName,
                            CreatedOn = DateTime.UtcNow,
                            UpdatedBy = UserName,
                            UpdatedOn = DateTime.UtcNow
                        }
                    },
                    BusinessContext = new BusinessContext { ApprovalState = "Approved", Location = "Garyville" },
                    Security = new Security
                    {
                        SenderContext = "PlanningScheduling UI Tool",
                        OriginalSourceContext = senderAppName
                    },
                    SenderInformation = new SenderInformation
                    {
                        SenderName = new SenderName
                        {
                            Name = UserName,
                            Description = UserName,
                            Id = 0
                        },
                        SenderDocumentID = Convert.ToString(sessionId),
                        SenderPublishDateTime = DateTime.UtcNow,
                        SentOn = DateTime.UtcNow,
                        SessionID = Convert.ToString(sessionId)
                    },
                },
                SCDemandRequestBody = new SCDemandRequestBody
                {
                    RequestedPeriods = new RequestedPeriods
                    {
                        RequestedPeriod = new List<RequestedPeriod>
            {
                new RequestedPeriod
                {
                    PeriodName = periodModel.PeriodName,
                    PeriodID = periodModel.PeriodID,
                    CreatedBy = UserName,
                    CreatedOn = DateTime.UtcNow,
                    ModifiedBy = UserName,
                    ModifiedOn = DateTime.UtcNow,
                    DateTimeRange = new DateTimeRange
                    {
                        ToDateTime = periodModel.DateTimeRange.ToDateTime,
                        FromDateTime = periodModel.DateTimeRange.FromDateTime
                    }
                }
            }
                    },
                    DataEntities = new DataEntities
                    {
                        DataEntity = new DataEntity
                        {
                            Name = "Demand",
                            State = "SystemForecast",
                            Type = entityType,
                            Location = refineryLocation, //CBG,with terminal
                            LocationType = regionName
                        }
                    },
                    BusinessCase = new BusinessCase
                    {
                        Name = periodModel.BusinessCase.Name,
                        Description = periodModel.BusinessCase.Name,
                        CreatedBy = UserName,
                        CreatedOn = DateTime.UtcNow,
                        UpdatedOn = DateTime.UtcNow,
                        UpdatedBy = UserName,
                        Id = periodModel.BusinessCase.Id,
                        Region = regionName
                    },
                    State = periodModel.ApplicationState,
                    CreatedOrUpdatedOn = DateTime.UtcNow,
                    CreatedOrUpdatedBy = UserName
                }
            };
            return demandRequestObj;
        }

        public static SCSupplyRequest CreateSCSupplyRequest(string sessionId, string senderAppName, string entityType, string refineryLocation, string regionName, string regionType, RequestedPeriodModel periodModel)
        {
            var UserName = periodModel.CreatedBy;
            var SupplyRequestObj = new SCSupplyRequest
            {
                SCSupplyRequestHeader = new SCSupplyRequestHeader
                {
                    DomainNamespace = new DomainNamespace
                    {
                        IdentifierType = "Name/Id",
                        SourceApplication = new SourceApplication
                        {
                            Name = senderAppName,
                            Description = senderAppName,
                            CreatedBy = UserName,
                            CreatedOn = DateTime.UtcNow,
                            UpdatedBy = UserName,
                            UpdatedOn = DateTime.UtcNow
                        },
                        DestinationApplication = new DestinationApplication
                        {
                            Name = periodModel.DomainNamespace.DestinationApplication.Name,
                            Description = periodModel.DomainNamespace.DestinationApplication.Name,
                            CreatedBy = UserName,
                            CreatedOn = DateTime.UtcNow,
                            UpdatedBy = UserName,
                            UpdatedOn = DateTime.UtcNow
                        }
                    },
                    BusinessContext = new BusinessContext { ApprovalState = "Approved", Location = "Garyville" },
                    Security = new Security
                    {
                        SenderContext = "PlanningScheduling UI Tool",
                        OriginalSourceContext = senderAppName
                    },
                    SenderInformation = new SenderInformation
                    {
                        SenderName = new SenderName
                        {
                            Name = UserName,
                            Description = UserName,
                            Id = 0
                        },
                        SenderDocumentID = Convert.ToString(sessionId),
                        SenderPublishDateTime = DateTime.UtcNow,
                        SentOn = DateTime.UtcNow,
                        SessionID = Convert.ToString(sessionId)

                    },
                },
                SCSupplyRequestBody = new SCSupplyRequestBody
                {
                    RequestedPeriods = new RequestedPeriods
                    {
                        RequestedPeriod = new List<RequestedPeriod>
                        {
                            new RequestedPeriod
                            {
                                PeriodName = periodModel.PeriodName,
                                PeriodID = periodModel.PeriodID,
                                CreatedBy = UserName,
                                CreatedOn = DateTime.UtcNow,
                                ModifiedBy = UserName,
                                ModifiedOn = DateTime.UtcNow,
                                DateTimeRange = new DateTimeRange
                                {
                                    ToDateTime = periodModel.DateTimeRange.ToDateTime,
                                    FromDateTime = periodModel.DateTimeRange.FromDateTime
                                }
                            }
                        }
                    },
                    DataEntities = new DataEntities
                    {
                        DataEntity = new DataEntity
                        {
                            Name = "Supply",
                            State = "SystemForecast",
                            Type = entityType,
                            Location = refineryLocation,
                            LocationType = regionName
                        }
                    },
                    BusinessCase = new BusinessCase
                    {
                        Name = periodModel.BusinessCase.Name,
                        Description = periodModel.BusinessCase.Name,
                        CreatedBy = UserName,
                        CreatedOn = DateTime.UtcNow,
                        UpdatedOn = DateTime.UtcNow,
                        UpdatedBy = UserName,
                        Id = periodModel.BusinessCase.Id,
                        Region = regionName
                    },
                    State = periodModel.ApplicationState,
                    EffectiveDate = periodModel.SupplyEffectiveDate,
                    CreatedOrUpdatedOn = DateTime.UtcNow,
                    CreatedOrUpdatedBy = UserName
                }
            };
            return SupplyRequestObj;
        }

        public static SCInventoryRequest CreateSCInventoryRequest(string sessionId, string SenderAppName, string entityType, string refineryLocation, string regionName, string regionType, RequestedPeriodModel periodModel)
        {
            var UserName = periodModel.CreatedBy;
            var inventoryRequestObj = new SCInventoryRequest
            {
                SCInventoryRequestHeader = new SCInventoryRequestHeader
                {
                    DomainNamespace = new DomainNamespace
                    {
                        IdentifierType = "Name/Id",
                        SourceApplication = new SourceApplication
                        {
                            Name = SenderAppName,
                            Description = SenderAppName,
                            CreatedBy = UserName,
                            CreatedOn = DateTime.UtcNow,
                            UpdatedBy = UserName,
                            UpdatedOn = DateTime.UtcNow
                        },
                        DestinationApplication = new DestinationApplication
                        {
                            Name = periodModel.DomainNamespace.DestinationApplication.Name,
                            Description = periodModel.DomainNamespace.DestinationApplication.Name,
                            CreatedBy = UserName,
                            CreatedOn = DateTime.UtcNow,
                            UpdatedBy = UserName,
                            UpdatedOn = DateTime.UtcNow
                        }
                    },
                    BusinessContext = new BusinessContext { ApprovalState = "Approved", Location = refineryLocation },
                    Security = new Security
                    {
                        SenderContext = "PlanningScheduling UI Tool",
                        OriginalSourceContext = SenderAppName
                    },
                    SenderInformation = new SenderInformation
                    {
                        SenderName = new SenderName
                        {
                            Name = UserName,
                            Description = UserName,
                            Id = 0
                        },
                        SenderDocumentID = Convert.ToString(sessionId),
                        SenderPublishDateTime = DateTime.UtcNow,
                        SentOn = DateTime.UtcNow,
                        SessionID = Convert.ToString(sessionId)
                    }
                },
                SCInventoryRequestBody = new SCInventoryRequestBody
                {
                    RequestedPeriods = new RequestedPeriods
                    {
                        RequestedPeriod = new List<RequestedPeriod>
                        {
                            new RequestedPeriod
                            {
                                PeriodName = periodModel.PeriodName,
                                PeriodID = periodModel.PeriodID,
                                CreatedBy = UserName,
                                CreatedOn = DateTime.UtcNow,
                                ModifiedBy = UserName,
                                ModifiedOn = DateTime.UtcNow,
                                DateTimeRange = new DateTimeRange
                                {
                                    ToDateTime = periodModel.DateTimeRange.ToDateTime,
                                    FromDateTime = periodModel.DateTimeRange.FromDateTime
                                }
                            }
                        }
                    },
                    DataEntities = new DataEntities
                    {
                        DataEntity = new DataEntity
                        {
                            Name = $"{refineryLocation} Product Opening Inventory",
                            State = "SystemForecast",
                            Type = entityType,
                            Location = refineryLocation,
                            LocationType = $"{regionName}_{regionType}"
                        }
                    },
                    BusinessCase = new BusinessCase
                    {
                        Name = periodModel.BusinessCase.Name,
                        Description = periodModel.BusinessCase.Name,
                        CreatedBy = UserName,
                        CreatedOn = DateTime.UtcNow,
                        UpdatedOn = DateTime.UtcNow,
                        UpdatedBy = UserName,
                        Id = periodModel.BusinessCase.Id
                    },
                    State = periodModel.ApplicationState,
                    CreatedOrUpdatedOn = DateTime.UtcNow,
                    CreatedOrUpdatedBy = UserName
                }
            };
            return inventoryRequestObj;
        }

        public static SCRefineryPremiseRequest CreateSCRefineryPremiseRequest(string sessionID, string SenderAppName, string refineryCode, List<RefineryPremiseConstraintType> requestedConstraint, RequestedPeriodModel periodModel)
        {
            var UserName = periodModel.CreatedBy;
            var refineryPremiseRequestObj = new SCRefineryPremiseRequest
            {
                SCRefineryPremiseRequestHeader = new SCRefineryPremiseRequestHeader
                {
                    DomainNamespace = new DomainNamespace
                    {
                        IdentifierType = "Name/Id",
                        SourceApplication = new SourceApplication
                        {
                            Name = SenderAppName,
                            Description = SenderAppName,
                            CreatedBy = UserName,
                            CreatedOn = DateTime.UtcNow,
                            UpdatedBy = UserName,
                            UpdatedOn = DateTime.UtcNow
                        },
                        DestinationApplication = new DestinationApplication
                        {
                            Name = periodModel.DomainNamespace.DestinationApplication.Name,
                            Description = periodModel.DomainNamespace.DestinationApplication.Name,
                            CreatedBy = UserName,
                            CreatedOn = DateTime.UtcNow,
                            UpdatedBy = UserName,
                            UpdatedOn = DateTime.UtcNow
                        }
                    },
                    BusinessContext = new BusinessContext { ApprovalState = "Approved", Location = "Garyville" },
                    Security = new Security
                    {
                        SenderContext = "PlanningScheduling UI Tool",
                        OriginalSourceContext = SenderAppName
                    },
                    SenderInformation = new SenderInformation
                    {
                        SenderName = new SenderName
                        {
                            Name = UserName,
                            Description = UserName,
                            Id = 0
                        },
                        SenderDocumentID = Convert.ToString(sessionID),
                        SenderPublishDateTime = DateTime.UtcNow,
                        SentOn = DateTime.UtcNow,
                        SessionID = Convert.ToString(sessionID)
                    },
                },
                SCRefineryPremiseRequestBody = new SCRefineryPremiseRequestBody
                {
                    RequestedPeriods = new RequestedPeriods
                    {
                        RequestedPeriod = new List<RequestedPeriod>
                        {
                            new RequestedPeriod
                            {
                                PeriodName = periodModel.PeriodName,
                                PeriodID = periodModel.PeriodID,
                                CreatedBy = UserName,
                                CreatedOn = DateTime.UtcNow,
                                ModifiedBy = UserName,
                                ModifiedOn = DateTime.UtcNow,
                                DateTimeRange = new DateTimeRange
                                {
                                    ToDateTime = periodModel.DateTimeRange.ToDateTime,
                                    FromDateTime = periodModel.DateTimeRange.FromDateTime
                                }
                            }
                        }
                    },
                    DataEntities = new DataEntities
                    {
                        DataEntity = new DataEntity
                        {
                            Name = "RefineryPremise",
                            State = "SystemForecast",
                            Type = string.Empty,
                            Location = "Location",
                            LocationType = string.Empty
                        }
                    },
                    BusinessCase = new BusinessCase
                    {
                        Name = periodModel.BusinessCase.Name,
                        Description = periodModel.BusinessCase.Name,
                        CreatedBy = UserName,
                        CreatedOn = DateTime.UtcNow,
                        UpdatedOn = DateTime.UtcNow,
                        UpdatedBy = UserName,
                        Id = periodModel.BusinessCase.Id
                    },
                    State = periodModel.ApplicationState,
                    RefineryCode = refineryCode,
                    RefineryPremiseConstraints = requestedConstraint,
                    CreatedOrUpdatedOn = DateTime.UtcNow,
                    CreatedOrUpdatedBy = UserName
                }
            };
            return refineryPremiseRequestObj;
        }

        public static SCRegionalPremiseRequest CreateSCRegionalPremiseRequest(string sessionId, string senderAppName, RequestedPeriodModel periodModel, List<RegionalPremiseConstraintType> requestedConstraint)
        {
            var UserName = periodModel.CreatedBy;
            var regionalPremiseRequest = new SCRegionalPremiseRequest
            {
                SCRegionalPremiseRequestHeader = new SCRegionalPremiseRequestHeader
                {
                    DomainNamespace = new DomainNamespace
                    {
                        IdentifierType = "Name/Id",
                        SourceApplication = new SourceApplication
                        {
                            Name = senderAppName,
                            Description = senderAppName,
                            CreatedBy = UserName,
                            CreatedOn = DateTime.UtcNow,
                            UpdatedBy = UserName,
                            UpdatedOn = DateTime.UtcNow
                        },
                        DestinationApplication = new DestinationApplication
                        {
                            Name = periodModel.DomainNamespace.DestinationApplication.Name,
                            Description = periodModel.DomainNamespace.DestinationApplication.Name,
                            CreatedBy = UserName,
                            CreatedOn = DateTime.UtcNow,
                            UpdatedBy = UserName,
                            UpdatedOn = DateTime.UtcNow
                        }
                    },
                    BusinessContext = new BusinessContext { ApprovalState = "Approved", Location = "Garyville" },
                    Security = new Security
                    {
                        SenderContext = "PlanningScheduling UI Tool",
                        OriginalSourceContext = senderAppName
                    },
                    SenderInformation = new SenderInformation
                    {
                        SenderName = new SenderName
                        {
                            Name = UserName,
                            Description = UserName,
                            Id = 0
                        },
                        SenderDocumentID = Convert.ToString(sessionId),
                        SenderPublishDateTime = DateTime.UtcNow,
                        SentOn = DateTime.UtcNow,
                        SessionID = Convert.ToString(sessionId)
                    },
                },
                SCRegionalPremiseRequestBody = new SCRegionalPremiseRequestBody
                {
                    RequestedPeriods = new RequestedPeriods
                    {
                        RequestedPeriod = new List<RequestedPeriod>
                        {
                            new RequestedPeriod
                            {
                                PeriodName = periodModel.PeriodName,
                                PeriodID = periodModel.PeriodID,
                                CreatedBy = UserName,
                                CreatedOn = DateTime.UtcNow,
                                ModifiedBy = UserName,
                                ModifiedOn = DateTime.UtcNow,
                                DateTimeRange = new DateTimeRange
                                {
                                    ToDateTime = periodModel.DateTimeRange.ToDateTime,
                                    FromDateTime = periodModel.DateTimeRange.FromDateTime
                                }
                            }
                        }
                    },
                    DataEntities = new DataEntities
                    {
                        DataEntity = new DataEntity
                        {
                            Name = "RegionalPremise",
                            State = "SystemForecast",
                            Type = string.Empty,
                            Location = "Location",
                            LocationType = string.Empty
                        }
                    },
                    BusinessCase = new BusinessCase
                    {
                        Name = periodModel.BusinessCase.Name,
                        Description = periodModel.BusinessCase.Name,
                        CreatedBy = UserName,
                        CreatedOn = DateTime.UtcNow,
                        UpdatedOn = DateTime.UtcNow,
                        UpdatedBy = UserName,
                        Id = periodModel.BusinessCase.Id
                    },
                    RegionalPremiseConstraints = requestedConstraint,
                    RegionCode = periodModel.RegionName,
                    State = periodModel.ApplicationState,
                    CreatedOrUpdatedOn = DateTime.UtcNow,
                    CreatedOrUpdatedBy = UserName
                }
            };
            return regionalPremiseRequest;
        }

        public static SCTransferRequest CreateSCTransferRequest(string sessionId, string senderAppName, string entityType, string refineryLocation, string regionName, string regionType, RequestedPeriodModel periodModel)
        {
            var userName = periodModel.CreatedBy;
            var transferRequestObj = new SCTransferRequest
            {
                SCTransferRequestHeader = new SCTransferRequestHeader
                {
                    DomainNamespace = new DomainNamespace
                    {
                        IdentifierType = "Name/Id",
                        SourceApplication = new SourceApplication
                        {
                            Name = senderAppName,
                            Description = senderAppName,
                            CreatedBy = userName,
                            CreatedOn = DateTime.UtcNow,
                            UpdatedBy = userName,
                            UpdatedOn = DateTime.UtcNow
                        },
                        DestinationApplication = new DestinationApplication
                        {
                            Name = periodModel.DomainNamespace.DestinationApplication.Name,
                            Description = periodModel.DomainNamespace.DestinationApplication.Name,
                            CreatedBy = userName,
                            CreatedOn = DateTime.UtcNow,
                            UpdatedBy = userName,
                            UpdatedOn = DateTime.UtcNow
                        }
                    },
                    BusinessContext = new BusinessContext(),
                    Security = new Security
                    {
                        SenderContext = "PlanningScheduling UI Tool",
                        OriginalSourceContext = senderAppName
                    },
                    SenderInformation = new SenderInformation
                    {
                        SenderName = new SenderName
                        {
                            Name = userName,
                            Description = userName,
                            Id = 0
                        },
                        SenderDocumentID = Convert.ToString(sessionId),
                        SenderPublishDateTime = DateTime.UtcNow,
                        SentOn = DateTime.UtcNow,
                        SessionID = Convert.ToString(sessionId)
                    },
                },
                SCTransferRequestBody = new SCTransferRequestBody
                {
                    RequestedPeriods = new RequestedPeriods
                    {
                        RequestedPeriod = new List<RequestedPeriod>
                        {
                            new RequestedPeriod
                            {
                                PeriodName = periodModel.PeriodName,
                                PeriodID = periodModel.PeriodID,
                                CreatedBy = userName,
                                CreatedOn = DateTime.UtcNow,
                                ModifiedBy = userName,
                                ModifiedOn = DateTime.UtcNow,
                                DateTimeRange = new DateTimeRange
                                {
                                    ToDateTime = periodModel.DateTimeRange.ToDateTime,
                                    FromDateTime = periodModel.DateTimeRange.FromDateTime
                                }
                            }
                        }
                    },
                    DataEntities = new DataEntities
                    {
                        DataEntity = new DataEntity
                        {
                            Name = "Transfer",
                            State = "SystemActual",
                            Type = entityType,
                            Location = refineryLocation,
                            LocationType = regionName
                        }
                    },
                    BusinessCase = new BusinessCase
                    {
                        Name = periodModel.BusinessCase.Name,
                        Description = periodModel.BusinessCase.Name,
                        CreatedBy = userName,
                        CreatedOn = DateTime.UtcNow,
                        UpdatedOn = DateTime.UtcNow,
                        UpdatedBy = userName,
                        Id = periodModel.BusinessCase.Id,
                        Region = regionName
                    },
                    State = periodModel.ApplicationState,
                    CreatedOrUpdatedOn = DateTime.UtcNow,
                    CreatedOrUpdatedBy = userName
                }
            };
            return transferRequestObj;
        }
        #endregion SCRequest

        #region AZResponse

        public static async Task<List<AZFunctionResponse>> LoadDemandAndPriceDataFromPublisherByPeriodAsync(RequestedPeriodModel requestedPeriodModel, bool isDemandExist, bool isPriceExist, HttpClient client, ILogger logger)
        {
            var demandSessionId = requestedPeriodModel.CorrelationId;
            var priceSessionId = requestedPeriodModel.CorrelationId;
            var senderAppDemand = requestedPeriodModel.ApplicationState == Service.Model.State.Actual.Description()
                ? PlanNSchedConstant.Application_AspenCDM
                : PlanNSchedConstant.Application_CIP;
            var senderAppPrice = requestedPeriodModel.ApplicationState == Service.Model.State.Actual.Description()
                ? PlanNSchedConstant.Application_BackcastDemandPricing
                : PlanNSchedConstant.Application_ForecastDemandPricing; //PlanNSchedConstant.Application_MarathonPR;
            var entityTypeDemand = requestedPeriodModel.QuantityEntityType;
            var entityTypePrice = requestedPeriodModel.PriceEntityType;
            var refineryLocation = requestedPeriodModel.RegionRefineryName;
            var regionName = requestedPeriodModel.RegionName;
            var regionType = requestedPeriodModel.RegionType;
            var destinationAppPrice = requestedPeriodModel.DomainNamespace.DestinationApplication.Name;
            var pricingEntityType = UIConstants.ROPricingEntityType;
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var demandAZFunctionResponseStatusList = new List<AZFunctionResponse>();
            try
            {
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var priceIds = await GetSupplyPriceIdsAsync(senderAppPrice, destinationAppPrice, pricingEntityType, client);

                if (!isPriceExist && !string.IsNullOrEmpty(priceIds))
                {
                    var scPriceRequest = CreateSCPriceRequest(priceSessionId, senderAppPrice, entityTypePrice, refineryLocation, regionName, regionType, requestedPeriodModel, entityTypePrice, priceIds);
                    HttpContent priceContent = new StringContent(JsonConvert.SerializeObject(scPriceRequest), Encoding.UTF8, "application/json");
                    var httpPrice = new HttpRequestMessage
                    {
                        Method = HttpMethod.Post,
                        RequestUri = new Uri(ConfigurationUI.Get_AzPriceFunction),
                        Content = priceContent,
                    };
                    var azPriceFunctionResponseStatus = new AZFunctionResponse
                    {
                        SessionID = priceSessionId,
                        EntityName = entityTypePrice,
                        PeriodID = requestedPeriodModel.PeriodID,
                        Status = Constant.DefaultStatus,
                        Description = string.Empty
                    };
                    try
                    {
                        var functionResponseForPrice = await client.SendAsync(httpPrice);
                        if (functionResponseForPrice.IsSuccessStatusCode)
                            azPriceFunctionResponseStatus = await HandleDurableFunctionResponseAsync(requestedPeriodModel, client, priceSessionId, entityTypePrice, functionResponseForPrice, logger);

                        demandAZFunctionResponseStatusList.Add(azPriceFunctionResponseStatus);
                    }
                    catch (Exception ex)
                    {
                        logger.LogMethodError(ex);
                    }
                }
                if (!isDemandExist && requestedPeriodModel.PeriodName != PlanNSchedConstant.LastPeriodOCostName)
                {
                    var scDemandRequest = CreateSCDemandRequest(demandSessionId, senderAppDemand, entityTypeDemand, refineryLocation, regionName, regionType, requestedPeriodModel);
                    HttpContent demandContent = new StringContent(JsonConvert.SerializeObject(scDemandRequest), Encoding.UTF8, "application/json");

                    var httpDemand = new HttpRequestMessage
                    {
                        Method = HttpMethod.Post,
                        RequestUri = new Uri(ConfigurationUI.Get_AzDemandFunction),
                        Content = demandContent,
                    };

                    var azDemandFunctionResponseStatus = new AZFunctionResponse
                    {
                        SessionID = demandSessionId,
                        EntityName = entityTypeDemand,
                        PeriodID = requestedPeriodModel.PeriodID,
                        Status = false,
                        Description = string.Empty
                    };

                    try
                    {
                        var functionResponseForDemand = await client.SendAsync(httpDemand);
                        if (functionResponseForDemand.IsSuccessStatusCode)
                            azDemandFunctionResponseStatus = await HandleDurableFunctionResponseAsync(requestedPeriodModel, client, demandSessionId, entityTypeDemand, functionResponseForDemand, logger);

                        demandAZFunctionResponseStatusList.Add(azDemandFunctionResponseStatus);
                    }
                    catch (Exception ex)
                    {
                        logger.LogMethodError(ex);
                    }
                }

                return demandAZFunctionResponseStatusList;
            }
            catch (Exception)
            {
                return new List<AZFunctionResponse>();
            }
        }

        public static async Task<List<AZFunctionResponse>> LoadSupplyAndCostDataFromPublisherByPeriodAsync(RequestedPeriodModel requestedPeriodModel, bool isSupplyExist, bool isCostExist, HttpClient client, ILogger logger)
        {
            var supplySessionId = requestedPeriodModel.CorrelationId;
            var costSessionId = requestedPeriodModel.CorrelationId;
            var isCrudeAvails = requestedPeriodModel.DomainNamespace.SourceApplication.Name == UIConstants.CrudeAvailExcel;
            var senderAppSupply = isCrudeAvails
                ? UIConstants.CrudeAvailExcel
                : requestedPeriodModel.ApplicationState == Service.Model.State.Actual.Description()
                ? PlanNSchedConstant.Application_AspenCDM
                : PlanNSchedConstant.Application_CIP;
            var senderAppCost = string.IsNullOrEmpty(requestedPeriodModel.ApplicationState)
                ? PlanNSchedConstant.Application_MarathonCR
                : requestedPeriodModel.ApplicationState == Service.Model.State.Actual.Description()
                ? isCrudeAvails ? PlanNSchedConstant.Application_BackcastingMarathonCR
                : PlanNSchedConstant.Application_BackcastSupplyPricing
                : PlanNSchedConstant.Application_ForecastSupplyPricing;
            var actualSenderAppCost = requestedPeriodModel.ApplicationState == Service.Model.State.Actual.Description()
                ? isCrudeAvails ? PlanNSchedConstant.Application_BackcastSupplyPricing
                : senderAppCost : senderAppCost;

            var destinationAppCost = requestedPeriodModel.DomainNamespace.DestinationApplication.Name;
            var entityTypeSupply = requestedPeriodModel.QuantityEntityType;
            var entityTypeCost = requestedPeriodModel.PriceEntityType;
            var pricingEntityType = string.IsNullOrEmpty(requestedPeriodModel.ApplicationState) ? UIConstants.PricingEntityType :
                requestedPeriodModel.ApplicationState == Service.Model.State.Actual.Description()
                ? UIConstants.ROPricingEntityType
                : UIConstants.ROPricingEntityType; // UIConstants.PricingEntityType;
            var refineryLocation = requestedPeriodModel.RegionRefineryName;
            var regionName = requestedPeriodModel.RegionName;
            var regionType = requestedPeriodModel.RegionType;
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(PlanNSchedConstant.MediaType_Json));
            var supplyAZFunctionResponseStatusList = new List<AZFunctionResponse>();

            try
            {
                if (!isCostExist)
                {
                    var priceIds = await GetSupplyPriceIdsAsync(actualSenderAppCost, destinationAppCost, pricingEntityType, client);
                    if (!string.IsNullOrEmpty(priceIds))
                    {
                        var scCostRequest = CreateSCCostRequest(costSessionId, senderAppCost, entityTypeCost, refineryLocation, regionName, regionType, requestedPeriodModel, entityTypeCost, priceIds);
                        HttpContent costContent = new StringContent(JsonConvert.SerializeObject(scCostRequest), Encoding.UTF8, "application/json");
                        var httpCost = new HttpRequestMessage
                        {
                            Method = HttpMethod.Post,
                            RequestUri = new Uri(ConfigurationUI.Get_AzCostFunction),
                            Content = costContent,
                        };
                        var azCostFunctionResponseStatus = new AZFunctionResponse
                        {
                            SessionID = costSessionId,
                            EntityName = entityTypeCost,
                            PeriodID = requestedPeriodModel.PeriodID,
                            Status = Constant.DefaultStatus,
                            Description = string.Empty
                        };
                        try
                        {
                            var functionResponseForCost = await client.SendAsync(httpCost);
                            if (functionResponseForCost.IsSuccessStatusCode)
                                azCostFunctionResponseStatus = await HandleDurableFunctionResponseAsync(requestedPeriodModel, client, costSessionId, entityTypeCost, functionResponseForCost, logger);

                            supplyAZFunctionResponseStatusList.Add(azCostFunctionResponseStatus);
                        }
                        catch (Exception ex)
                        {
                            logger.LogMethodError(ex);
                        }
                    }
                }

                if (!isSupplyExist)
                {
                    var scSupplyRequest = CreateSCSupplyRequest(supplySessionId, senderAppSupply, entityTypeSupply, refineryLocation, regionName, regionType, requestedPeriodModel);
                    HttpContent supplyContent = new StringContent(JsonConvert.SerializeObject(scSupplyRequest), Encoding.UTF8, "application/json");
                    var httpSupply = new HttpRequestMessage
                    {
                        Method = HttpMethod.Post,
                        RequestUri = new Uri(ConfigurationUI.Get_AzSupplyFunction),
                        Content = supplyContent,
                    };

                    var azSupplyFunctionResponseStatus = new AZFunctionResponse
                    {
                        SessionID = scSupplyRequest?.SCSupplyRequestHeader?.SenderInformation?.SessionID,
                        EntityName = entityTypeSupply,
                        PeriodID = requestedPeriodModel.PeriodID,
                        Status = Constant.DefaultStatus,
                        Description = string.Empty
                    };

                    try
                    {
                        var functionResponseForSupply = await client.SendAsync(httpSupply);
                        if (functionResponseForSupply.IsSuccessStatusCode)
                            azSupplyFunctionResponseStatus = await HandleDurableFunctionResponseAsync(requestedPeriodModel, client, supplySessionId, entityTypeSupply, functionResponseForSupply, logger);

                        supplyAZFunctionResponseStatusList.Add(azSupplyFunctionResponseStatus);
                    }
                    catch (Exception ex)
                    {
                        logger.LogMethodError(ex);
                    }
                }

                return supplyAZFunctionResponseStatusList;
            }
            catch (Exception)
            {
                return new List<AZFunctionResponse>();
            }
        }

        public static async Task<AZFunctionResponse> RequestInventoriesDataAsync(Refinery refinery, HttpClient client, RequestedPeriodModel firstRequestedPeriodModel, ILogger logger)
        {
            var sessionId = firstRequestedPeriodModel.CorrelationId;

            var senderAppInventory = firstRequestedPeriodModel.ApplicationState == Service.Model.State.Actual.Description() ? PlanNSchedConstant.Application_CIP : refinery.DomainNamespace.SourceApplication.Name;
            var entityTypeInventory = "Product";
            if (refinery.Name.Contains("Terminal")) { entityTypeInventory = ConfigurationUI.OnlyShowBasedOnCurrentInventory; }
            var refineryLocation = refinery.Name;
            var regionName = refinery.RegionName;
            var regionType = refinery.Name.Contains("Terminal") ? refinery.Name : "Refinery";

            try
            {
                var scInventoryRequest = CreateSCInventoryRequest(sessionId, senderAppInventory, entityTypeInventory, refineryLocation,
                    regionName, regionType, firstRequestedPeriodModel);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                HttpContent inventoryContent = new StringContent(JsonConvert.SerializeObject(scInventoryRequest), Encoding.UTF8, "application/json");
                var httpInventory = new HttpRequestMessage();
                httpInventory.Method = HttpMethod.Post;
                httpInventory.Content = inventoryContent;
                var azInventoryFunctionResponseStatus = new AZFunctionResponse
                {
                    SessionID = sessionId,
                    EntityName = entityTypeInventory,
                    PeriodID = firstRequestedPeriodModel.PeriodID,
                    Status = false,
                    Description = string.Empty
                };

                httpInventory.RequestUri = refinery.Name == "Terminal" ? new Uri(ConfigurationUI.Get_AzTelInventoryFunction) : new Uri(ConfigurationUI.Get_AzRefInventoryFunction);

                var functionResponseForInventory = await client.SendAsync(httpInventory);
                if (functionResponseForInventory.IsSuccessStatusCode)
                {
                    azInventoryFunctionResponseStatus = await HandleDurableFunctionResponseAsync(firstRequestedPeriodModel, client, sessionId, entityTypeInventory, functionResponseForInventory, logger);
                    return azInventoryFunctionResponseStatus;
                }
                else
                    return azInventoryFunctionResponseStatus;
            }
            catch (Exception ex)
            {
                logger.LogMethodError(ex);
                var azInvtFunctionResponseStatus = new AZFunctionResponse
                {
                    SessionID = sessionId,
                    EntityName = entityTypeInventory,
                    PeriodID = firstRequestedPeriodModel.PeriodID,
                    Status = false,
                    Description = string.Empty
                };
                return azInvtFunctionResponseStatus;
            }
        }

        public static async Task<List<AZFunctionResponse>> LoadTransportationCostFromPublisherByPeriodAsync(RequestedPeriodModel requestedPeriodModel, bool isTransportationCostExist, string senderAppPrice, Uri requestUri, HttpClient client, ILogger logger, bool isTransferExist = true)
        {
            try
            {
                var transportationCostAZFunctionResponseStatuses = new List<AZFunctionResponse>();
                var sessionID = requestedPeriodModel.CorrelationId;
                var entityTypePrice = requestedPeriodModel.PriceEntityType;
                var refineryLocation = requestedPeriodModel.RegionRefineryName;
                var regionName = requestedPeriodModel.RegionName;
                var regionType = requestedPeriodModel.RegionType;
                if (!isTransportationCostExist)
                {
                    var scPriceRequest = CreateSCPriceRequest(sessionID, senderAppPrice, entityTypePrice, refineryLocation, regionName,
                    regionType, requestedPeriodModel, entityTypePrice);
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    HttpContent priceContent = new StringContent(JsonConvert.SerializeObject(scPriceRequest), Encoding.UTF8, "application/json");

                    var httpPrice = new HttpRequestMessage
                    {
                        Method = HttpMethod.Post,
                        RequestUri = requestUri,
                        Content = priceContent,
                    };
                    var azPriceFunctionResponseStatus = new AZFunctionResponse
                    {
                        SessionID = sessionID,
                        EntityName = entityTypePrice,
                        PeriodID = requestedPeriodModel.PeriodID,
                        Status = Constant.DefaultStatus,
                        Description = string.Empty
                    };
                    try
                    {
                        var functionResponseForPrice = await client.SendAsync(httpPrice);
                        if (functionResponseForPrice.IsSuccessStatusCode)
                            azPriceFunctionResponseStatus = await HandleDurableFunctionResponseAsync(requestedPeriodModel, client, sessionID, entityTypePrice, functionResponseForPrice, logger);

                        transportationCostAZFunctionResponseStatuses.Add(azPriceFunctionResponseStatus);
                    }
                    catch (Exception ex)
                    {
                        logger.LogMethodError(ex);
                    }
                }
                if (!isTransferExist)
                {

                    var scTransferRequest = CreateSCTransferRequest(sessionID, senderAppPrice, entityTypePrice, refineryLocation, regionName, regionType, requestedPeriodModel);
                    HttpContent transferContent = new StringContent(JsonConvert.SerializeObject(scTransferRequest), Encoding.UTF8, "application/json");

                    var httpTransfer = new HttpRequestMessage
                    {
                        Method = HttpMethod.Post,
                        RequestUri = new Uri(ConfigurationUI.Get_AzTransferFunction),
                        Content = transferContent,
                    };

                    var azTransferFunctionResponseStatus = new AZFunctionResponse
                    {
                        SessionID = sessionID,
                        EntityName = entityTypePrice,
                        PeriodID = requestedPeriodModel.PeriodID,
                        Status = false,
                        Description = string.Empty
                    };

                    try
                    {
                        var functionResponseForDemand = await client.SendAsync(httpTransfer);
                        if (functionResponseForDemand.IsSuccessStatusCode)
                            azTransferFunctionResponseStatus = await HandleDurableFunctionResponseAsync(requestedPeriodModel, client, sessionID, entityTypePrice, functionResponseForDemand, logger);

                        transportationCostAZFunctionResponseStatuses.Add(azTransferFunctionResponseStatus);
                    }
                    catch (Exception ex)
                    {
                        logger.LogMethodError(ex);
                    }
                }
                return transportationCostAZFunctionResponseStatuses;
            }
            catch (Exception)
            {
                return new List<AZFunctionResponse>();
            }
        }

        public static async Task<AZFunctionResponse> RequestInTransitInventoriesAsync(RequestedPeriodModel firstRequestedPeriodModel, HttpClient client, string inventoryType, ILogger logger)
        {
            var sessionId = firstRequestedPeriodModel.CorrelationId;
            var senderAppInventory = PlanNSchedConstant.Application_AspenCDM;
            var entityTypeInventory = PlanNSchedConstant.Product;
            var regionName = firstRequestedPeriodModel.RegionName;

            var azInventoryFunctionResponse = new AZFunctionResponse
            {
                SessionID = sessionId,
                EntityName = entityTypeInventory,
                PeriodID = firstRequestedPeriodModel.PeriodID,
                Status = false,
                Description = string.Empty
            };
            try
            {
                var scInventoryRequest = CreateSCInventoryRequest(sessionId, senderAppInventory, entityTypeInventory, inventoryType, regionName, inventoryType, firstRequestedPeriodModel);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(PlanNSchedConstant.MediaType_Json));
                var httpInventory = new HttpRequestMessage()
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri(ConfigurationUI.Get_AzInTransitInventoryFunction),
                    Content = new StringContent(JsonConvert.SerializeObject(scInventoryRequest), Encoding.UTF8, PlanNSchedConstant.MediaType_Json)
                };

                var functionResponseForInventory = await client.SendAsync(httpInventory);
                if (functionResponseForInventory.IsSuccessStatusCode)
                    azInventoryFunctionResponse = await HandleDurableFunctionResponseAsync(firstRequestedPeriodModel, client, sessionId, entityTypeInventory, functionResponseForInventory, logger);

                return azInventoryFunctionResponse;
            }
            catch (Exception ex)
            {
                logger.LogMethodError(ex);
                return azInventoryFunctionResponse;
            }
        }

        public static async Task<List<AZFunctionResponse>> LoadRefineryPremiseConstraintDataFromPublisherByPeriodAsync(RequestedPeriodModel requestedPeriodModel, Dictionary<string, bool> isConstraintExistResult, string refineryCode, HttpClient client, ILogger logger)
        {
            var refineryPremiseSessionId = requestedPeriodModel.CorrelationId;
            var sourceApplication = PlanNSchedConstant.RefineryPremiseExcel;
            var refineryPremiseAZFunctionResponseStatusList = new List<AZFunctionResponse>();

            var constraintToRequest = new List<RefineryPremiseConstraintType>();
            foreach (var constraint in isConstraintExistResult)
            {
                if (!constraint.Value)
                {
                    foreach (var field in typeof(RefineryPremiseConstraintType).GetFields())
                    {
                        if (Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) is DescriptionAttribute attribute && attribute.Description.Equals(constraint.Key, StringComparison.OrdinalIgnoreCase))
                        {
                            constraintToRequest.Add((RefineryPremiseConstraintType)field.GetValue(null));
                        }
                    }
                }
            }
            var sCRefineryPremiseRequest = CreateSCRefineryPremiseRequest(refineryPremiseSessionId, sourceApplication, refineryCode, constraintToRequest, requestedPeriodModel);
            HttpContent refineryPremiseContent = new StringContent(JsonConvert.SerializeObject(sCRefineryPremiseRequest), Encoding.UTF8, "application/json");
            var httpRefineryPremise = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(ConfigurationUI.Get_AzRefineryPremiseFunction),
                Content = refineryPremiseContent,
            };
            try
            {
                var functionResponseForRefineryPremise = await client.SendAsync(httpRefineryPremise);
                var azRefineryPremiseFunctionResponseStatus = await HandleDurableFunctionResponseAsync(requestedPeriodModel, client, refineryPremiseSessionId, PlanNSchedConstant.RefineryPremise, functionResponseForRefineryPremise, logger);

                refineryPremiseAZFunctionResponseStatusList.Add(azRefineryPremiseFunctionResponseStatus);
            }
            catch (Exception)
            {
                return new List<AZFunctionResponse>();
            }
            return refineryPremiseAZFunctionResponseStatusList;
        }

        public static async Task<List<AZFunctionResponse>> LoadRegionalPremiseConstraintDataFromPublisherByPeriodAsync(RequestedPeriodModel requestedPeriodModel, Dictionary<string, bool> isConstraintExistResult, HttpClient client, ILogger logger)
        {
            var regionalPremiseSessionId = requestedPeriodModel.CorrelationId;
            var senderApp = PlanNSchedConstant.Cip;
            var regionName = requestedPeriodModel.RegionName;
            var regionType = requestedPeriodModel.RegionType;
            var regionalPremiseAZFunctionResponseStatusList = new List<AZFunctionResponse>();

            var constraintToRequest = new List<RegionalPremiseConstraintType>();
            foreach (var constraint in isConstraintExistResult)
            {
                if (!constraint.Value)
                {
                    foreach (var field in typeof(RegionalPremiseConstraintType).GetFields())
                    {
                        if (Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) is DescriptionAttribute attribute && attribute.Description.Equals(constraint.Key, StringComparison.OrdinalIgnoreCase))
                        {
                            constraintToRequest.Add((RegionalPremiseConstraintType)field.GetValue(null));
                        }
                    }
                }
            }

            var sCRegionalPremiseRequest = CreateSCRegionalPremiseRequest(regionalPremiseSessionId, senderApp, requestedPeriodModel, constraintToRequest);
            HttpContent regionalPremiseContent = new StringContent(JsonConvert.SerializeObject(sCRegionalPremiseRequest), Encoding.UTF8, "application/json");
            var httpRegionalPremise = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(ConfigurationUI.Get_AzRegionalPremiseFunction),
                Content = regionalPremiseContent,
            };
            try
            {
                var functionResponseForRegionalPremise = await client.SendAsync(httpRegionalPremise);
                var azRegionalPremiseFunctionResponseStatus = await HandleDurableFunctionResponseAsync(requestedPeriodModel, client, regionalPremiseSessionId, PlanNSchedConstant.RegionalPremise, functionResponseForRegionalPremise, logger);
                regionalPremiseAZFunctionResponseStatusList.Add(azRegionalPremiseFunctionResponseStatus);
            }
            catch (Exception)
            {
                return new List<AZFunctionResponse>();
            }

            return regionalPremiseAZFunctionResponseStatusList;
        }
        #endregion AZResponse

        #region GetCallFromUI

        public static NotificationEventsResponseUI GetNotifications(NotificationRequestUI notificationRequest, HttpClient client)
        {

            var notificationEventsResponseUI = new NotificationEventsResponseUI();

            var response = client.PostAsJsonAsync(ConfigurationUI.GetNotifications, notificationRequest).Result;
            var responseContent = response.Content.ReadAsStringAsync().Result;
            var notificationEventsServiceResponse = JsonConvert.DeserializeObject<NotificationEventsServiceResponse>(responseContent);
            if (response.IsSuccessStatusCode)
            {
                var uniqueEventIds = notificationEventsServiceResponse?.notificationEventList.Select(s => s.id).Distinct().ToList();
                if (uniqueEventIds?.Count != 0)
                {
                    foreach (var uniqueEventId in uniqueEventIds)
                    {
                        var notificationObject = new NotificationEventsModelUI();
                        var notifications = notificationEventsServiceResponse?.notificationEventList.Where(n => n.id == uniqueEventId).ToList();
                        var notif = notifications.FirstOrDefault();
                        foreach (var notification in notifications)
                        {
                            if (notification.eventDataKey.ToLower().Contains("metadata"))
                            {
                                var metaData = new NotificationMetaData();
                                metaData = !string.IsNullOrEmpty(notification.eventDataValue) ? JsonConvert.DeserializeObject<NotificationMetaData>(notification.eventDataValue) : null;
                                if (metaData != null)
                                {
                                    notificationObject.CorrelationId = metaData.CorrelationId;
                                    notificationObject.PlanName = metaData.PlanName;
                                    notificationObject.RegionName = metaData.RegionName;
                                    notificationObject.PeriodName = metaData.PeriodName;
                                    notificationObject.SourceApplicationName = metaData.SourceApplicationName;
                                    notificationObject.DestinationApplicationName = metaData.DestinationApplicationName;
                                    notificationObject.EventSource = metaData.EventSource;
                                    notificationObject.EntityName = metaData.EntityName;
                                }
                                else
                                {
                                    notificationObject.CorrelationId = string.Empty;
                                    notificationObject.PlanName = string.Empty;
                                    notificationObject.RegionName = string.Empty;
                                    notificationObject.PeriodName = string.Empty;
                                    notificationObject.SourceApplicationName = string.Empty;
                                    notificationObject.DestinationApplicationName = string.Empty;
                                    notificationObject.EventSource = string.Empty;
                                    notificationObject.EntityName = string.Empty;
                                }
                            }
                            else
                            {
                                notificationObject.eventData.Add(notification.eventDataKey, notification.eventDataValue);
                            }
                        }
                        notificationObject.id = uniqueEventId;
                        notificationObject.severityLevel = notif.severityLevel;
                        notificationObject.impact = notif.impact;
                        notificationObject.eventSourceName = notif.eventSourceName;
                        notificationObject.eventTypeName = notif.eventTypeName;
                        notificationObject.occurrenceTime = notif.occurrenceTime;
                        notificationObject.eventSourceType = notif.eventSourceType;
                        notificationObject.statusName = notif.statusName;
                        notificationObject.createdByUserId = notif.createdByUserId;

                        notificationEventsResponseUI.notificationEventList.Add(notificationObject);
                    }
                }
                return notificationEventsResponseUI;
            }
            else
            {
                notificationEventsResponseUI.status = notificationEventsServiceResponse.status;
                notificationEventsResponseUI.error = notificationEventsServiceResponse.error;
            }
            return notificationEventsResponseUI;
        }

        private static async Task<bool> IsUserAuthorizedForDpoAsync(string domainNamespace, IActiveUser activeUser) =>
            domainNamespace == ConfigurationUI.domainNamespaceTypeDP && await activeUser.IsAuthorizedAsync(PlanNSchedConstant.ViewDpo);

        private static async Task<bool> IsUserAuthorizedForPimsAsync(string domainNamespace, IActiveUser activeUser) =>
        domainNamespace == ConfigurationUI.domainNamespaceTypeRP &&
            (await activeUser.IsAuthorizedAsync(PlanNSchedConstant.ViewPims) || await activeUser.IsAuthorizedAsync(PlanNSchedConstant.ViewPimsBackcasting));

        private static async Task<List<string>> GetUnauthorizedRegionsAsync(string domainType, IActiveUser activeUser)
        {
            var regions = new[] { PlanNSchedConstant.NorthRegion, PlanNSchedConstant.SouthRegion, PlanNSchedConstant.WestRegion };
            var unauthorizedRegions = new List<string>();
            foreach (var region in regions)
            {
                var authorized = await activeUser.IsAuthorizedAsync($"View{region}{domainType}");
                if (!authorized)
                    unauthorizedRegions.Add($"{region}");
            }

            return unauthorizedRegions;
        }

        private static async Task<List<RefineryModel>> GetAuthorizedRefineriesAsync(List<RefineryModel> refineries, IActiveUser activeUser)
        {
            var authorizedRefineries = new List<RefineryModel>();
            foreach (var refinery in refineries)
            {
                var authorized = await activeUser.IsAuthorizedAsync($"View{refinery.RefineryCode}PPIMS");
                if (authorized)
                    authorizedRefineries.Add(refinery);
            }

            return authorizedRefineries;
        }

        public static async Task<List<RegionModel>> GetRegionListAsync(IActiveUser activeUser, string domainNamespaceType, HttpClient client,
            string localTimeZoneName, string correlationId, bool includeHistoricalPlanList = false, bool isActualData = false)
        {
            var businessProcesses = await client.GetFromJsonAsync<List<BusinessProcess>>(string.Format(ConfigurationUI.api_Plan_GetAllBusinessProcess,
                correlationId));
            var businessProcess = businessProcesses?.FirstOrDefault(x => x.BusinessProcessCode == domainNamespaceType);
            if (businessProcess == null)
                return [];

            var applicationState = isActualData ? Service.Model.State.Actual.Description() : Service.Model.State.Forecast.Description();
            List<RegionModel> regions = [];
            if (await IsUserAuthorizedForDpoAsync(domainNamespaceType, activeUser) || await IsUserAuthorizedForPimsAsync(domainNamespaceType, activeUser))
                regions = await client.GetFromJsonAsync<List<RegionModel>>(string.Format(ConfigurationUI.api_Plan_GetRegions, correlationId, businessProcess.Id));

            if (regions.Count == 0) return regions;

            var domainType = domainNamespaceType == ConfigurationUI.domainNamespaceTypeDP ? PlanNSchedConstant.DPO : PlanNSchedConstant.Pims;
            if (isActualData) domainType += PlanSched.Shared.Common.Constant.Backcasting;
            var unauthorizedRegions = await GetUnauthorizedRegionsAsync(domainType, activeUser);
            regions.RemoveAll(r => unauthorizedRegions.Contains(r.RegionName));

            foreach (var region in regions)
            {
                region.ApplicationState = applicationState;

                var (businessCase, activeBusinessCases, backcastingBusinessCases) = await GetBusinessCasesForRegionAsync(
                    region, client, isActualData, includeHistoricalPlanList, applicationState);

                region.BusinessCase = businessCase;
                region.ActiveBusinessCases = activeBusinessCases;
                region.BackcastingBusinessCases = backcastingBusinessCases;

                PopulateRegionMetadata(region, businessCase, localTimeZoneName);
            }
            return regions;
        }

        private static async Task<(BusinessCase businessCase, List<BusinessCase>? activeBusinessCases, List<BusinessCase> backcastingBusinessCases)> GetBusinessCasesForRegionAsync(RegionModel region, HttpClient client, bool isActualData, bool includeHistoricalPlanList, string applicationState)
        {
            var activeBackcastingBusinessCases = new List<BusinessCase>();
            var businessCase = new BusinessCase();
            List<BusinessCase>? activeBusinessCases = null;

            if (isActualData)
            {
                activeBackcastingBusinessCases = await client.GetFromJsonAsync<List<BusinessCase>>(
                    ConfigurationUI.api_Plan_GetAllActiveActualBusinessCasesAsync + region.DomainNamespace?.DestinationApplication.Id) ?? [];
                SetBusinessCaseRegionInfo(activeBackcastingBusinessCases, region.Refinery, region.RegionName);
                businessCase = activeBackcastingBusinessCases.FirstOrDefault() ?? new BusinessCase();
            }
            else if (ConfigurationUI.IsMidtermEnabled)
            {
                activeBusinessCases = await client.GetFromJsonAsync<List<BusinessCase>>(
                    ConfigurationUI.api_Plan_GetAllActiveBusinessCases + region.DomainNamespace?.DestinationApplication.Id);
            }
            else
            {
                businessCase = await client.GetFromJsonAsync<BusinessCase>(
                    string.Format(ConfigurationUI.api_Plan_GetLatestBusinessCase, region.DomainNamespace?.DestinationApplication.Id, applicationState)) ?? new BusinessCase();
            }
            if (includeHistoricalPlanList)
            {
                if (isActualData)
                    activeBusinessCases = await client.GetFromJsonAsync<List<BusinessCase>>(ConfigurationUI.api_Plan_GetAllInactiveActualBusinessCasesAsync + region.DomainNamespace.DestinationApplication.Id) ?? [];
                else if (ConfigurationUI.IsMidtermEnabled)
                    activeBusinessCases = await client.GetFromJsonAsync<List<BusinessCase>>(ConfigurationUI.api_Plan_GetAllInActiveBusinessCasesAsync + region.DomainNamespace.DestinationApplication.Id) ?? [];
                else
                    activeBusinessCases = await client.GetFromJsonAsync<List<BusinessCase>>(ConfigurationUI.api_Plan_GetAllActiveBusinessCases + region.DomainNamespace.DestinationApplication.Id) ?? [];
                activeBusinessCases.ForEach(businessCase => businessCase.RefineryName = region.Refinery);
                activeBusinessCases.ForEach(businessCase => businessCase.Region = region.RegionName);
            }

            SetBusinessCaseRegionInfo(activeBusinessCases, region.Refinery, region.RegionName);

            return (businessCase, activeBusinessCases, activeBackcastingBusinessCases);
        }
        private static void SetBusinessCaseRegionInfo(List<BusinessCase>? businessCases, string? refineryName, string regionName)
        {
            if (businessCases == null) return;

            foreach (var bc in businessCases)
            {
                bc.RefineryName = refineryName;
                bc.Region = regionName;
            }
        }
        private static void PopulateRegionMetadata(RegionModel region, BusinessCase businessCase, string localTimeZoneName)
        {
            if (businessCase?.Id > 0)
            {
                var utcCreatedOn = DateTime.Parse(Convert.ToString(businessCase.CreatedOn));
                var utcUpdatedOn = DateTime.Parse(Convert.ToString(businessCase.UpdatedOn));

                region.CreatedOn = localTimeZoneName == PlanNSchedConstant.DefaultTimeZone
                    ? utcCreatedOn.ToLocalTime()
                    : ConvertUTCToLocal(utcCreatedOn, localTimeZoneName);

                region.UpdatedOn = localTimeZoneName == PlanNSchedConstant.DefaultTimeZone
                    ? utcUpdatedOn.ToLocalTime()
                    : ConvertUTCToLocal(utcUpdatedOn, localTimeZoneName);

                region.CreatedBy = businessCase.CreatedBy;
                region.UpdatedBy = businessCase.UpdatedBy;
                region.ConcatUpdatedBy = "by " + businessCase.UpdatedBy;
                region.BusinessCase.Id = businessCase.Id;
            }
            else
            {
                region.CreatedOn = null;
                region.CreatedBy = string.Empty;
                region.UpdatedOn = null;
                region.UpdatedBy = string.Empty;
                region.ConcatUpdatedBy = string.Empty;
            }
        }
        public static async Task<List<RefineryModel>> GetAllRefineryPremiseActivePlansAsync(HttpClient client, string localTimeZoneName, string correlationId, IActiveUser? activeUser = null)
        {
            var refineries = await client.GetFromJsonAsync<List<RefineryModel>>(string.Format(ConfigurationUI.api_Plan_GetAllRefineryPremiseActivePlans,
                correlationId));
            if (activeUser != null)
                refineries = await GetAuthorizedRefineriesAsync(refineries, activeUser);

            foreach (var refinery in refineries)
            {
                if (refinery.BusinessCase != null && refinery.BusinessCase.Id > 0)
                {
                    var UTC_CreatedOn = DateTime.Parse(Convert.ToString(refinery.BusinessCase.CreatedOn));
                    var UTC_UpdatedOn = DateTime.Parse(Convert.ToString(refinery.BusinessCase.UpdatedOn));

                    refinery.CreatedOn = localTimeZoneName == PlanNSchedConstant.DefaultTimeZone
                        ? UTC_CreatedOn.ToLocalTime()
                        : ConvertUTCToLocal(UTC_CreatedOn, localTimeZoneName);

                    refinery.UpdatedOn = localTimeZoneName == PlanNSchedConstant.DefaultTimeZone
                        ? UTC_UpdatedOn.ToLocalTime()
                        : ConvertUTCToLocal(UTC_UpdatedOn, localTimeZoneName);

                    refinery.CreatedBy = refinery.BusinessCase.CreatedBy;
                    refinery.UpdatedBy = refinery.BusinessCase.UpdatedBy;
                    refinery.ConcatUpdatedBy = "by " + refinery.BusinessCase.UpdatedBy;
                }
                else
                {
                    refinery.CreatedOn = null;
                    refinery.CreatedBy = string.Empty;
                    refinery.UpdatedOn = null;
                    refinery.UpdatedBy = string.Empty;
                    refinery.ConcatUpdatedBy = string.Empty;
                }
            }
            return refineries;
        }

        public static async Task<List<RefineryModel>> GetAllRefineryPremiseHistoricalPlansAsync(HttpClient client, string localTimeZoneName, string correlationId, IActiveUser activeUser)
        {
            var refineries = await client.GetFromJsonAsync<List<RefineryModel>>(string.Format(ConfigurationUI.api_Plan_GetAllRefineryPremiseHistoricalPlans,
                correlationId));

            refineries = await GetAuthorizedRefineriesAsync(refineries, activeUser);

            foreach (var refinery in refineries)
            {
                if (refinery.BusinessCase != null && refinery.BusinessCase.Id > 0)
                {
                    var UTC_CreatedOn = DateTime.Parse(Convert.ToString(refinery.BusinessCase.CreatedOn));
                    var UTC_UpdatedOn = DateTime.Parse(Convert.ToString(refinery.BusinessCase.UpdatedOn));

                    refinery.CreatedOn = localTimeZoneName == PlanNSchedConstant.DefaultTimeZone
                        ? UTC_CreatedOn.ToLocalTime()
                        : ConvertUTCToLocal(UTC_CreatedOn, localTimeZoneName);

                    refinery.UpdatedOn = localTimeZoneName == PlanNSchedConstant.DefaultTimeZone
                        ? UTC_UpdatedOn.ToLocalTime()
                        : ConvertUTCToLocal(UTC_UpdatedOn, localTimeZoneName);

                    refinery.CreatedBy = refinery.BusinessCase.CreatedBy;
                    refinery.UpdatedBy = refinery.BusinessCase.UpdatedBy;
                    refinery.ConcatUpdatedBy = "by " + refinery.BusinessCase.UpdatedBy;
                }
                else
                {
                    refinery.CreatedOn = null;
                    refinery.CreatedBy = string.Empty;
                    refinery.UpdatedOn = null;
                    refinery.UpdatedBy = string.Empty;
                    refinery.ConcatUpdatedBy = string.Empty;
                }
            }
            return refineries;
        }

        public static async Task<DomainNamespace> GetDomainNamespaceIdAsync(int domainNamespaceId, string correlationId, HttpClient client) =>
            await client.GetFromJsonAsync<DomainNamespace>(string.Format(ConfigurationUI.api_Plan_GetDomainNamespaceId, correlationId, domainNamespaceId));

        public static async Task<RegionModel> GetRegionAsync(int domainNamespaceId, string correlationId, HttpClient client, Service.Model.State applicationState) =>
            await client.GetFromJsonAsync<RegionModel>(string.Format(ConfigurationUI.api_Plan_GetRegion, correlationId, domainNamespaceId, applicationState));

        public static async Task<RegionModel> GetRegionByBusinessCaseIdAsync(int businessCaseId, string correlationId, HttpClient client)
            => await client.GetFromJsonAsync<RegionModel>(string.Format(ConfigurationUI.api_Plan_GetRegionByBusinessCaseId, correlationId, businessCaseId));

        public static async Task<BusinessCase> GetBusinessCaseByBusinessCaseIdAsync(int businessCaseId, string correlationId, HttpClient client) =>
           await client.GetFromJsonAsync<BusinessCase>(string.Format(ConfigurationUI.api_Plan_GetBusinessCaseByBusinessCaseId, correlationId, businessCaseId));

        public static async Task<RefineryModel> GetRefineryModelByBusinessCaseIdAsync(int businessCaseId, string correlationId, HttpClient client) =>
           await client.GetFromJsonAsync<RefineryModel>(string.Format(ConfigurationUI.api_Plan_GetRefineryModelByBusinessCaseId, correlationId, businessCaseId));

        public static async Task<List<RequestedPeriodModel>> GetInventoryPeriodsAsync(List<RequestedPeriodModel> periodModels, RegionModel regionModel, ISessionService sessionService, HttpClient client, string userName)
        {
            var requestedPeriodModel = new RequestedPeriodModel
            {
                CreatedBy = userName,
                CreatedOn = DateTime.UtcNow,
                ModifiedBy = userName,
                ModifiedOn = DateTime.UtcNow,
                PeriodID = 1,
                PeriodName = PlanNSchedConstant.DefaultEntityValue,
                DateTimeRange = new DateTimeRange { FromDateTime = DateTime.UtcNow, ToDateTime = DateTime.UtcNow },
                DomainNamespace = regionModel.DomainNamespace,
                BusinessCase = regionModel.BusinessCase,
                RegionMarket = regionModel.Market,
                RegionRefineryName = regionModel.Refinery,
                RegionName = regionModel.RegionName,
                RegionType = regionModel.Type,
                RegionTerminal = regionModel.Terminal,
                CorrelationId = sessionService.GetCorrelationId(),
                IsHierarchy = regionModel.IsHierarchy,
            };
            requestedPeriodModel.DomainNamespace.SourceApplication = new SourceApplication { Name = Constant.DefaultEntityValue, Description = Constant.DefaultEntityValue };
            var response = await client.PostAsJsonAsync(ConfigurationUI.GetPeriods, requestedPeriodModel);

            if (response == null || response?.IsSuccessStatusCode != true) return null;
            var responseContent = response.Content.ReadAsStringAsync().Result;

            return JsonConvert.DeserializeObject<List<RequestedPeriodModel>>(responseContent).ToList();
        }

        public static RequestedPeriodModel GetInventoryFirstRequestedPeriod(List<RequestedPeriodModel> periodModels, RegionModel regionModel, RequestedPeriodModel finalRequestedPeriodModelObj, string currentUser)
        {
            var isActual = regionModel.ApplicationState == Service.Model.State.Actual.Description();
            var lastPeriod = periodModels.LastOrDefault();
            finalRequestedPeriodModelObj = periodModels.FirstOrDefault();
            finalRequestedPeriodModelObj.DateTimeRange = new DateTimeRange
            {
                FromDateTime = finalRequestedPeriodModelObj.DateTimeRange.FromDateTime,
                ToDateTime = isActual ? lastPeriod.DateTimeRange.ToDateTime : finalRequestedPeriodModelObj.DateTimeRange.FromDateTime
            };
            finalRequestedPeriodModelObj.CreatedBy = currentUser;
            finalRequestedPeriodModelObj.CreatedOn = DateTime.UtcNow;
            finalRequestedPeriodModelObj.ModifiedBy = currentUser;
            finalRequestedPeriodModelObj.ModifiedOn = DateTime.UtcNow;
            finalRequestedPeriodModelObj.RegionMarket = regionModel.Market;
            finalRequestedPeriodModelObj.RegionRefineryName = regionModel.Refinery;
            finalRequestedPeriodModelObj.RegionName = regionModel.RegionName;
            finalRequestedPeriodModelObj.RegionType = regionModel.Type;
            finalRequestedPeriodModelObj.RegionTerminal = regionModel.Terminal;
            return finalRequestedPeriodModelObj;
        }

        public static async Task<string> GetRefineryPremiseConstraintDataAsync(RequestedPeriodModel periodModel, string selectedConstraint, HttpClient client)
        {
            if (_refineryPremiseConstraintGetUrlMap.TryGetValue(selectedConstraint, out var url))
            {
                var response = await client.PostAsJsonAsync(url, periodModel);
                if (response != null && response.IsSuccessStatusCode)
                {
                    var responseContent = response.Content.ReadAsStringAsync();

                    return responseContent.Result;
                }
            }
            return null;
        }

        public static async Task<string> GetRegionalPremiseConstraintDataAsync(RequestedPeriodModel periodModel, string selectedConstraint, HttpClient client)
        {
            if (_regionalPremiseConstraintGetUrlMap.TryGetValue(selectedConstraint, out var url))
            {
                var response = await client.PostAsJsonAsync(url, periodModel);
                if (response != null && response.IsSuccessStatusCode)
                {
                    var responseContent = response.Content.ReadAsStringAsync();

                    return responseContent.Result;
                }
            }
            return null;
        }

        public static async Task<List<RequestedPeriodModel>> GetPeriodDataByBusinessCaseId(int businessCaseId, string correlationId, HttpClient client, string userName)
        {
            List<RequestedPeriodModel> periodModelList = [];
            var currentTime = DateTime.UtcNow;
            var currentUser = userName;

            periodModelList = await client.GetFromJsonAsync<List<RequestedPeriodModel>>(string.Format(ConfigurationUI.GetPeriodsByBusinessCaseId, correlationId, businessCaseId));

            if (periodModelList.Count == 0) return periodModelList;

            foreach (var item in periodModelList)
            {
                item.PeriodDisplayName = item.PeriodName + ": " + item.DateTimeRange.FromDateTime.Value.ToString("MM/dd/yy") + " To " + item.DateTimeRange.ToDateTime.Value.ToString("MM/dd/yy");
                item.CreatedBy = currentUser;
                item.CreatedOn = currentTime;
                item.ModifiedBy = currentUser;
                item.ModifiedOn = currentTime;
            }
            return periodModelList;
        }

        public static List<RequestedPeriodModel> GetPeriodData(List<RequestedPeriodModel> periodModelList, RegionModel regionModel,
            ISessionService sessionService, HttpClient client, string userName)
        {
            var businessCase = regionModel.BusinessCase;
            var currentTime = DateTime.UtcNow;
            var currentUser = userName;

            var requestedPeriodModel = new RequestedPeriodModel
            {
                CreatedBy = currentUser,
                CreatedOn = currentTime,
                ModifiedBy = currentUser,
                ModifiedOn = currentTime,
                PeriodID = 1,
                PeriodName = "DEFAULT",
                DateTimeRange = new DateTimeRange { FromDateTime = DateTime.UtcNow, ToDateTime = DateTime.UtcNow },
                DomainNamespace = regionModel.DomainNamespace,
                BusinessCase = businessCase,
                RegionMarket = regionModel.Market,
                RegionRefineryName = regionModel.Refinery,
                RegionName = regionModel.RegionName,
                RegionType = regionModel.Type,
                RegionTerminal = regionModel.Terminal,
                CorrelationId = sessionService.GetCorrelationId(),
                IsHierarchy = regionModel.IsHierarchy
            };
            requestedPeriodModel.DomainNamespace.SourceApplication = new SourceApplication { Name = "string", Description = "string" };

            var response = client.PostAsJsonAsync(ConfigurationUI.GetPeriods, requestedPeriodModel).Result;
            if (response != null)
            {
                var responseContent = response.Content.ReadAsStringAsync();
                periodModelList = JsonConvert.DeserializeObject<List<RequestedPeriodModel>>(responseContent.Result).ToList();
            }

            foreach (var item in periodModelList)
            {
                item.PeriodDisplayName = item.PeriodName + ": " + item.DateTimeRange.FromDateTime.Value.ToString("MM/dd/yy") + " To " + item.DateTimeRange.ToDateTime.Value.ToString("MM/dd/yy");
                item.CreatedBy = currentUser;
                item.CreatedOn = currentTime;
                item.ModifiedBy = currentUser;
                item.ModifiedOn = currentTime;
                item.RegionMarket = regionModel.Market;
                item.RegionRefineryName = regionModel.Refinery;
                item.RegionName = regionModel.RegionName;
                item.RegionType = regionModel.Type;
                item.RegionTerminal = regionModel.Terminal;

            }
            return periodModelList;
        }

        public static async Task<List<ProductDemandAndPrice>?> GetProductDemandAndPriceDataAsync(bool isTierAggregated, int selectedPeriodId, List<ProductDemandAndPrice>? productDemandAndPriceList, List<RequestedPeriodModel> periodModelList, HttpClient client)
        {
            var requestedPeriodModel = periodModelList?.FirstOrDefault(d => d.PeriodID == selectedPeriodId);
            if (requestedPeriodModel != null)
            {
                var response = await client.PostAsJsonAsync(ConfigurationUI.GetDemandAndPrice + PlanNSchedConstant.ForwardSlash + isTierAggregated, requestedPeriodModel);
                if (response != null && response.IsSuccessStatusCode)
                {
                    var responseContent = response.Content.ReadAsStringAsync();
                    productDemandAndPriceList = JsonConvert.DeserializeObject<List<ProductDemandAndPrice>>(responseContent.Result)?.ToList();
                    return productDemandAndPriceList;
                }
            }
            return null;
        }

        public static List<ProductSupplyAndCost>? GetSupplyAndCostData(int selectedPeriodId, List<ProductSupplyAndCost>? productSupplyAndCostList, List<RequestedPeriodModel> periodModelList, HttpClient client)
        {
            var requestedPeriodModel = periodModelList.FirstOrDefault(d => d.PeriodID == selectedPeriodId);
            if (requestedPeriodModel != null)
            {
                var response = client.PostAsJsonAsync(ConfigurationUI.GetSupplyAndCost, requestedPeriodModel).Result;
                if (response != null)
                {
                    var responseContent = response.Content.ReadAsStringAsync();
                    productSupplyAndCostList = JsonConvert.DeserializeObject<List<ProductSupplyAndCost>>(responseContent.Result)?.ToList();
                    return productSupplyAndCostList;
                }
            }
            return null;
        }

        public async static Task<List<Model.OpeningInventory>> GetInventoryDataAsync(RequestedPeriodModel firstRequestedPeriodModel, List<Model.OpeningInventory> openingInventories, HttpClient client)
        {
            var response = await client.PostAsJsonAsync(ConfigurationUI.GetInventory, firstRequestedPeriodModel);
            if (response == null) return null;

            var responseContent = await response.Content.ReadAsStringAsync();
            openingInventories = JsonConvert.DeserializeObject<List<Model.OpeningInventory>>(responseContent)?.ToList() ?? new List<Model.OpeningInventory>();
            return openingInventories;
        }

        public static List<CommodityType> GetInventoryCommodityData(List<Model.OpeningInventory> openingInventories, List<CommodityType> commodityTypes)
        {
            if (openingInventories == null) return null;
            var inventoryTypes = openingInventories.Select(x => x.Type).ToList();
            var commodityTypeId = 1;
            foreach (var inventoryType in inventoryTypes.Distinct())
            {
                var commodityTypeArray = inventoryType?.Split(Constant.EmptySpace);
                if (commodityTypeArray.Length >= 2)
                {
                    var commodityTypeName = commodityTypeArray[1];
                    if (commodityTypes.Select(o => o.Name == commodityTypeName).Count() == 0)
                    {
                        commodityTypes.Add(new CommodityType { Id = commodityTypeId, Name = commodityTypeName });
                        commodityTypeId++;
                    }
                }
            }
            return commodityTypes;
        }

        public static List<Refinery> GetInventoryRefineryData(List<Model.OpeningInventory> openingInventories, List<Refinery> refineriesListDD)
        {
            if (openingInventories == null) return null;
            var locations = openingInventories.Select(x => x.Location).Distinct().ToList();
            var locationId = 1;
            foreach (var location in locations)
            {
                refineriesListDD.Add(new Refinery { Id = locationId, Name = location });
                locationId++;
            }
            return refineriesListDD;
        }

        public static async Task<List<RequestedPeriodModel>> GetTransportationCostPeriodDataAsync(List<RequestedPeriodModel> periodModels,
            RegionModel regionModel, ISessionService sessionService, HttpClient client, string userName)
        {
            var currentTime = DateTime.UtcNow;
            var requestedPeriodModel = new RequestedPeriodModel
            {
                CreatedBy = userName,
                CreatedOn = currentTime,
                ModifiedBy = userName,
                ModifiedOn = currentTime,
                PeriodID = 1,
                PeriodName = "DEFAULT",
                DateTimeRange = new DateTimeRange { FromDateTime = DateTime.UtcNow, ToDateTime = DateTime.UtcNow },
                DomainNamespace = regionModel.DomainNamespace,
                BusinessCase = regionModel.BusinessCase,
                RegionMarket = regionModel.Market,
                RegionRefineryName = regionModel.Refinery,
                RegionName = regionModel.RegionName,
                RegionType = regionModel.Type,
                RegionTerminal = regionModel.Terminal,
                CorrelationId = sessionService.GetCorrelationId()
            };
            requestedPeriodModel.DomainNamespace.SourceApplication = new SourceApplication { Name = "string", Description = "string" };

            var response = await client.PostAsJsonAsync(ConfigurationUI.GetPeriods, requestedPeriodModel);
            if (response == null) return null;

            var responseContent = await response.Content.ReadAsStringAsync();
            periodModels = JsonConvert.DeserializeObject<List<RequestedPeriodModel>>(responseContent).ToList();

            foreach (var periodModel in periodModels)
            {

                periodModel.PeriodDisplayName = string.Format("{0}: {1} To {2}", periodModel.PeriodName,
                    periodModel.DateTimeRange.FromDateTime.Value.ToString(Constant.DateFormat),
                    periodModel.DateTimeRange.ToDateTime.Value.ToString(Constant.DateFormat));
                periodModel.CreatedBy = userName;
                periodModel.CreatedOn = currentTime;
                periodModel.ModifiedBy = userName;
                periodModel.ModifiedOn = currentTime;
                periodModel.RegionMarket = regionModel.Market;
                periodModel.RegionRefineryName = regionModel.Refinery;
                periodModel.RegionName = regionModel.RegionName;
                periodModel.RegionType = regionModel.Type;
                periodModel.RegionTerminal = regionModel.Terminal;
            }
            return periodModels;
        }

        public static async Task<List<TransportationCost>?> GetTransportationCostDataAsync(int selectedPeriodId, List<TransportationCost>? transportationCosts,
            List<RequestedPeriodModel> periodModels, HttpClient client)
        {
            var requestedPeriodModel = periodModels?.FirstOrDefault(d => d.PeriodID == selectedPeriodId);
            if (requestedPeriodModel == null) return null;

            var response = await client.PostAsJsonAsync(ConfigurationUI.GetTransportationCost, requestedPeriodModel);
            if (response == null) return null;

            var responseContent = await response.Content.ReadAsStringAsync();
            transportationCosts = JsonConvert.DeserializeObject<List<TransportationCost>>(responseContent)?.ToList();
            return transportationCosts;
        }

        public static async Task<List<TransferAndCost>?> GetTransferCostDataAsync(int selectedPeriodId, List<TransferAndCost>? transferCosts,
           List<RequestedPeriodModel> periodModels, HttpClient client)
        {
            var requestedPeriodModel = periodModels?.FirstOrDefault(d => d.PeriodID == selectedPeriodId);
            if (requestedPeriodModel == null) return null;

            var response = await client.PostAsJsonAsync(ConfigurationUI.GetTransferAndCost, requestedPeriodModel);
            if (response == null) return null;

            var responseContent = await response.Content.ReadAsStringAsync();
            transferCosts = JsonConvert.DeserializeObject<List<TransferAndCost>>(responseContent)?.ToList();
            return transferCosts;
        }

        public static List<InTransitInventory>? GetInTransitInventoriesAsync(RequestedPeriodModel firstRequestedPeriodModel, List<InTransitInventory>? inTransitInventories, HttpClient client)
        {
            var response = client.PostAsJsonAsync(ConfigurationUI.GetInTransitInventory, firstRequestedPeriodModel).Result;
            if (response != null)
            {
                var responseContent = response.Content.ReadAsStringAsync();
                inTransitInventories = JsonConvert.DeserializeObject<List<InTransitInventory>>(responseContent.Result).ToList();
                inTransitInventories = inTransitInventories.Where(x => x.TypeName.ToUpper().Contains("INTRANSIT")).ToList();
                return inTransitInventories;
            }
            return null;
        }

        public static async Task<bool> IsLatestPlanAsync(int businessCaseId, HttpClient client) =>
           await client.GetFromJsonAsync<bool>(string.Format(ConfigurationUI.IsLatestPlan, businessCaseId));
        #endregion GetCallFromUI

        #region CommonMethod

        /// <summary>
        /// Method to convert UTC Datetime to Local Datetime
        /// </summary>
        /// <param name="utcDate"></param>
        /// <param name="localTimeZone"></param>
        /// <returns name="cstTime"></returns>
        public static DateTime ConvertUTCToLocal(DateTime utcDate, string localTimeZone)
        {
            var cstZone = TimeZoneInfo.FindSystemTimeZoneById(localTimeZone);
            var cstTime = TimeZoneInfo.ConvertTimeFromUtc(utcDate, cstZone);
            return cstTime;
        }

        public static async Task<List<ValueTypes>> GetOverrideTypes(HttpClient client)
            => await client.GetFromJsonAsync<List<ValueTypes>>(ConfigurationUI.api_GetOverrideValueTypes);

        /// <summary>
        /// Get SelectedUserRole from URI
        /// </summary>
        /// <param name="pageUri"></param>
        /// <returns></returns>
        public static string GetUserRoleFromURI(string pageUri)
        {
            var splitUri = pageUri.Split(PlanNSchedConstant.UriDelimiter);
            var userRole = pageUri.Contains(PlanNSchedConstant.PlanPageAttribute) ? splitUri[splitUri.Count() - 4] : splitUri[splitUri.Count() - 3];

            return userRole;
        }

        public static string GetPlanTypeName(RegionModel regionModel)
        {
            if (regionModel?.BusinessCase?.PlanTypeId != null && regionModel.PlanTypes != null)
            {
                return regionModel.PlanTypes
                    .FirstOrDefault(pt => pt.Id == regionModel.BusinessCase.PlanTypeId)?.Name ?? string.Empty;
            }
            return string.Empty;
        }
        #endregion CommonMethod

        #region Other

        private static async Task<string?> GetSupplyPriceIdsAsync(string senderAppCost, string destinationAppCost, string pricingEntityType, HttpClient client) =>
            await client.GetFromJsonAsync<string>(string.Format(ConfigurationUI.GetSupplyPriceIds, senderAppCost, destinationAppCost, pricingEntityType));
        public static async Task<bool> ArchivePlanAsync(int businessCaseId, HttpClient client) =>
            await client.GetFromJsonAsync<bool>(string.Format(ConfigurationUI.api_Plan_ArchivePlan, businessCaseId));

        public static async Task<bool> DeletePlanAsync(int businessCaseId, HttpClient client) =>
            await client.GetFromJsonAsync<bool>(string.Format(ConfigurationUI.api_Plan_DeletePlan, businessCaseId));

        public static async Task<bool> UpdateBusinessCaseFlagAsync(int businessCaseId, string correlationId, HttpClient client, bool isFlagged)
        {
            try
            {
                var url = string.Format(ConfigurationUI.api_Plan_UpdateBusinessCaseFlagAsync, correlationId, businessCaseId, isFlagged);
                var response = await client.PutAsync(url, null);

                return response.IsSuccessStatusCode && await response.Content.ReadFromJsonAsync<bool>();
            }
            catch
            {
                return false;
            }
        }

        public static async Task<bool> SharePlanAsync(int businessCaseId, bool? isShared, HttpClient client) =>
           await client.GetFromJsonAsync<bool>(string.Format(ConfigurationUI.Api_Plan_SharePlan, businessCaseId, isShared ?? false));
        public static async Task<bool> CreateFromSharedAsync(int sourceBusinessCaseId, string correlationId, string createdBy, HttpClient client)
        {
            var url = string.Format(ConfigurationUI.Api_Plan_CreateFromShared, correlationId, sourceBusinessCaseId, createdBy);
            var response = await client.PostAsync(url, null);
            return response.IsSuccessStatusCode;
        }

        public static async Task<bool> FlagPlanTypeAsync(BusinessCase businessCase, HttpClient client)
        {
            var result = false;
            var responseResult = await client.PostAsJsonAsync(ConfigurationUI.Api_FlagPlanType, businessCase);
            if (responseResult != null)
            {
                var responseContent = await responseResult.Content.ReadAsStringAsync();
                result = responseContent == "true";
            }
            return result;
        }

        #endregion Other

        private static async Task<AZFunctionResponse> HandleDurableFunctionResponseAsync(RequestedPeriodModel requestedPeriodModel, HttpClient client, string sessionId, string entityType, HttpResponseMessage functionResponse, ILogger logger)
        {
            var azResponseStatus = new AZFunctionResponse
            {
                SessionID = sessionId,
                EntityName = entityType,
                PeriodID = requestedPeriodModel.PeriodID,
                Status = false,
                Description = string.Empty
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            try
            {
                var responseContent = await functionResponse.Content.ReadAsStringAsync();
                var durableFunctionResponse = System.Text.Json.JsonSerializer.Deserialize<DurableFunctionTriggerResponse>(responseContent, _caseInSensitiveOptions);
                var statusQueryUri = durableFunctionResponse?.StatusQueryGetUri;

                if (string.IsNullOrWhiteSpace(statusQueryUri))
                    return azResponseStatus;

                while (!cts.Token.IsCancellationRequested)
                {
                    var statusResponse = await client.GetAsync(statusQueryUri, cts.Token);
                    var statusResponseContent = await statusResponse.Content.ReadAsStringAsync(cts.Token);
                    var orchestrationResponse = System.Text.Json.JsonSerializer.Deserialize<DurableFunctionOrchestrationResponse>(statusResponseContent, _caseInSensitiveOptions);
                    var runtimeStatus = orchestrationResponse?.RuntimeStatus;

                    if (runtimeStatus == UIConstants.Completed)
                    {
                        azResponseStatus.Status = true;
                        break;
                    }
                    else if (runtimeStatus is UIConstants.Failed or UIConstants.Terminated or UIConstants.Canceled)
                    {
                        azResponseStatus.Status = false;
                        break;
                    }

                    await Task.Delay(UIConstants.DelayMilliseconds, cts.Token);
                }
            }
            catch (OperationCanceledException ex)
            {
                logger.LogMethodError(ex);
            }
            catch (Exception ex)
            {
                logger.LogMethodError(ex);
            }

            return azResponseStatus;
        }

        private static readonly JsonSerializerOptions _caseInSensitiveOptions = new() { PropertyNameCaseInsensitive = true };

    }
}
