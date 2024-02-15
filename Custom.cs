using cfFrame.Controllers.Onboarding;
using cfFrame.Helpers;
using cfFrame.Models;
using CloudFin.Common;
using CloudFin.Model.Engine;
using CloudFinConnector;
using System;
using System.Collections.Generic;
using System.Linq;
using static cfFrame.Helpers.Utils;
using cfFrame.ProjectCustoms.NeptuneLines.Entities;
using cfFrame.ProjectCustoms.NeptuneLines.Models;
using cfFrame.Controllers;

namespace cfFrame.ProjectCustoms
{
    public static class NeptuneLinesCustom
    {
        public static readonly List<Guid> ProjectIDs = new List<Guid>() {
            Guid.Parse("1979595E-7863-4706-802B-3E8BCEADE34B"), 
            Guid.Parse("6562B068-4CDC-4649-AD9C-E9CCBBE3D608"), 
        };
        public static void InvoiceHeaderAndLines(DocumentInfo docdata, ref bool userReviewRequired, ref Guid tradeCurrID, bool isSalesDocument, ref Guid tradeTypeID,Guid? scanlogTradeTypeId, ILogger logger = null)
        {
            try
            {
                docdata.DocItems = docdata.DocItems == null ? new List<DocumentItems>() : docdata.DocItems;
                docdata.DocItems.Clear();
                var itemAmount = (docdata.TotalAmount ?? 0) == 0 ? docdata.NetAmountAfterDisc ?? 0 : docdata.TotalAmount ?? 0;

                #region header data
                docdata.NetAmount = docdata.NetAmountAfterDisc = docdata.LinesNetAmount = itemAmount;
                docdata.LinesSurchAmount = docdata.VatAmount = docdata.LinesVatAmount = docdata.LinesDiscAmount = docdata.TotalDiscAmount = docdata.TotalSurchAmount = docdata.VatPerc = 0;
                #endregion

                docdata.DocItems.Add(new DocumentItems()
                {
                    ProductCode = "ΠΑΡΥΠ",
                    ProductDescr = "ΠΑΡΥΠ ΔΑΠΑΝΗ - ΠΑΡΟΧΗ ΥΠΗΡΕΣΙΑΣ",
                    ProductID = "e24ad293-07ee-4f26-a247-0bbec1427c8e",
                    TotalAmount = itemAmount,
                    NetAmount = itemAmount,
                    NetValueAfterDisc = itemAmount,
                    VatAmount = 0,
                    Qty = 1,
                    Price = itemAmount
                });
                docdata.RawData = docdata?.RawData ?? String.Empty;

                if ((docdata.TotalAmount ?? 0) == 0)
                {
                    userReviewRequired = true;
                    logger?.I($"TotalAmount = 0. UserReviewRequired = true");
                }

                //get currency
                cfCurrency cfCurrency = null;
                try
                {
                    cfCurrency = NeptuneLinesCurrency(docdata);
                }
                catch (Exception ex)
                {
                    logger?.E($"Failed to determine currency. {ex.FlattenNoStack()}");
                }

                if (cfCurrency != null) tradeCurrID = cfCurrency.ID;
                else
                {
                    userReviewRequired = true;
                    logger?.I($"Currency Not Found. UserReviewRequired = true");
                }

                docdata.DocType = "Invoice";
                tradeTypeID = GetTradeDataType(docdata, isSalesDocument, scanlogTradeTypeId ?? Guid.Empty);
            }
            catch (Exception ex)
            {
                logger?.E($"Error: {ex}");
            }
        }
        private static cfCurrency NeptuneLinesCurrency(DocumentInfo docdata)
        {
            var ocrs = ExtractUtils.Minify(docdata.RawOcrs());
            var totalAmount = (docdata.TotalAmount ?? 0);
            List<cfCurrency> currencies;

            using (cfEntitiesV2 db = cfEntitiesV2.CreateEntitiesForSpecificDatabaseName())
            {
                currencies = db.cfCurrency.AsNoTracking().Where(c => !(c.isDeleted ?? false)).ToList();
            }

            var titleTokenPat = @"(?<=\W)[A-Z\u0374-\u03FF]{3}(?=\W)";
            var symbolTokenPat = @"[^\w\s.,\-|+():\/\@'#%\u0374-\u03FF]";
            cfCurrency cfCurrency = null;
            if (!docdata.Currency.IsEmpty())
            {
                var potentialCurrencies = new List<string>() { docdata.Currency };
                cfCurrency = SearchForEntity(currencies, c => c.Title, potentialCurrencies, titleTokenPat, String.Empty, false);
                if (cfCurrency == null) cfCurrency = SearchForEntity(currencies, c => c.CurrencySymbol, potentialCurrencies, symbolTokenPat, String.Empty, false);
            }
            if (cfCurrency == null && totalAmount != 0)
            {
                var anchor = totalAmount.Regexify();
                var texts = ocrs.Select(t => ExtractUtils.GetTextCloseToAnchor(t, anchor)).ToList();

                cfCurrency = SearchForEntity(currencies, c => c.Title, texts, titleTokenPat, String.Empty, false);
                if (cfCurrency == null) cfCurrency = SearchForEntity(currencies, c => c.CurrencySymbol, texts, symbolTokenPat, String.Empty, false);
            }

            if (cfCurrency == null) cfCurrency = SearchForEntity(currencies, c => c.Title, ocrs, titleTokenPat, String.Empty, false);
            if (cfCurrency == null) cfCurrency = SearchForEntity(currencies, c => c.CurrencySymbol, ocrs, symbolTokenPat, String.Empty, false);
            return cfCurrency;
        }

        public static void DetermineWorkflowAndUser(cfEntitiesV2 db, Guid cfSupplierID, Guid cfCompanyID, ref Guid TradeTreeID, ref Guid TradeCategoryID, DocumentInfo docdata, ILogger logger = null)
        {
            var supplierToWorkflowMappings = new List<SupplierToWorkflowMapping>();
            var workflowToUserMappings = new List<WorkflowToUserMapping>();
            try
            {

                var sqlForWorkflows = $"Select * from [{SupplierToWorkflowMapping.TableName}] WHERE [Supplier] = '{cfSupplierID}';";
                supplierToWorkflowMappings = db.Database.SqlQuery<SupplierToWorkflowMapping>(sqlForWorkflows).ToList();
                var distinctWorkflowCount = supplierToWorkflowMappings.GroupBy(wfm => wfm.Workflow).Count();

                RuleValidator ruleValidator = new RuleValidator(db, cfCompanyID,docdata);


                if (distinctWorkflowCount == 1)
                {
                    var workflowMapping = supplierToWorkflowMappings.FirstOrDefault();
                    TradeTreeID = workflowMapping.Workflow;
                }
                else
                {
                    var passed = new List<SupplierToWorkflowMapping>();

                    //Run Rules
                    foreach (var supplierToWorkflowMapping in supplierToWorkflowMappings)
                    {
                        var rule = new Rule(supplierToWorkflowMapping.RuleType, supplierToWorkflowMapping.RuleOperator, supplierToWorkflowMapping.RuleValuesList);
                        bool ruleIsValid = ruleValidator.ValidateRule(rule);
                        if (ruleIsValid)
                            passed.Add(supplierToWorkflowMapping);
                    }


                    //1 Supplier with 1 PASSED workflow
                    distinctWorkflowCount = passed.GroupBy(wfm => wfm.Workflow).Count();
                    if (distinctWorkflowCount == 1)
                    {
                        var workflowMapping = supplierToWorkflowMappings.FirstOrDefault();
                        TradeTreeID = workflowMapping.Workflow;
                    }
                }

                if (TradeTreeID == Guid.Empty) return;

                //find user
                var sqlForUsers = $"Select * from [{WorkflowToUserMapping.TableName}] WHERE [Workflow] = '{TradeTreeID}';";
                workflowToUserMappings = db.Database.SqlQuery<WorkflowToUserMapping>(sqlForUsers).ToList();
                var distinctUserCount = workflowToUserMappings.GroupBy(wfm => wfm.User).Count();

                if (distinctUserCount == 1)
                {
                    var workflowMapping = workflowToUserMappings.FirstOrDefault();
                    TradeCategoryID = workflowMapping.User;
                }
                else
                {
                    var passed = new List<WorkflowToUserMapping>();
                    foreach (var workflowToUserMapping in workflowToUserMappings)
                    {
                        var rule = new Rule(workflowToUserMapping.RuleType, workflowToUserMapping.RuleOperator, workflowToUserMapping.RuleValuesList);
                        bool ruleIsValid = ruleValidator.ValidateRule(rule);
                        if (ruleIsValid)
                            passed.Add(workflowToUserMapping);
                    }

                    distinctUserCount = passed.GroupBy(wfm => wfm.User).Count();
                    if (distinctUserCount == 1)
                    {
                        var workflowMapping = passed.FirstOrDefault();
                        TradeCategoryID = workflowMapping.User;
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.E($"{ex.FlattenNoStack()}");
            }
        }
        public static Guid GetSetCompany(DocumentInfo docdata, ILogger logger = null)
        {
            List<string> ocrs = new List<string>();
            cfCompany cfCompany = null;
            try
            {
                if (!docdata.RawData.IsEmpty()) ocrs.Add(docdata.RawData);
                if (docdata.TemplateResult != null) ocrs.AddRange(docdata.TemplateResult.RawOcrs());

                List<cfCompany> companies;
                var scores = new Dictionary<Guid, double>();
                using (cfEntitiesV2 db = cfEntitiesV2.CreateEntitiesForSpecificDatabaseName())
                {
                    companies = db.cfCompany.AsNoTracking().Where(c => (c.isActive ?? true)).ToList();
                }
                var rejectPat = @"(^|[\r\n])([nν][εe][pρ][τt][υuv][νn][eε]( {1,5}[iιl|][iιl|][νn][eεc][sσ5$])?|[εe][υuv][rn][oοό0]?|[rn][iιl|][sσ5$]|company)([\r\n]|$)";
                var tokenPat = @"[nν][εe][pρ][τt][υuv][νn][eε] {1,5}[A-Z\u0374-\u03FF]{3,}|[A-Z\u0374-\u03FF]{4,}";

                var prioPat = @"([nν][εe][pρ][τt][υuv][νn][eε]|[vn][iιl|][kκ][iιl|][nν].|[g][rb][aαλδ][νn]([dδ][εe])?) {1,5}[A-Z\u0374-\u03FF]{3,}";

                //give priority to company from template
                if (!docdata.Company.IsEmpty())
                {
                    var cOcrs = new List<string> { docdata.Company };
                    cfCompany = SearchForEntity(companies, c => c.Description, cOcrs, tokenPat, rejectPat);
                }
                if (cfCompany == null) cfCompany = SearchForEntity(companies, c => c.Description, ocrs, prioPat, rejectPat);
                if (cfCompany == null) cfCompany = SearchForEntity(companies, c => c.Description, ocrs, tokenPat, rejectPat);

                if (cfCompany != null) return cfCompany.ID;
                var defaultCompanyID = companies.Where(s => s.isDefault == true && s.isActive ==true && s.isDeleted==false).Select(s => s.ID).FirstOrDefault();
                return defaultCompanyID;
            }
            catch (Exception ex)
            {
                logger?.E($"{ex.FlattenNoStack()}");
            }
            return Guid.Empty;
        }

        public static void UpdatecfCategory(cfEntitiesV2 db, ILogger logger = null)
        {
            try
            {
                var workFlowToUserMappingsQuery = $"Select * from [{WorkflowToUserMapping.TableName}];";
                var workflowToUserMappings = db.Database.SqlQuery<WorkflowToUserMapping>(workFlowToUserMappingsQuery).ToList();

                var updatedCategoriesInMapping = UpdateCategoriesInMapping(db, workflowToUserMappings);
                var updatedCategoriesNotInMapping = UpdateCategoriesNotInMapping(db, workflowToUserMappings);

                var saveChanges = updatedCategoriesInMapping || updatedCategoriesNotInMapping;
                if (saveChanges)
                {
                    db.SaveChanges();
                    CachedController.InvalidateCache(db.TokenInfo.Token, "NeptuneLinesCustom.UpdatecfCategory");
                } 
            }
            catch (Exception ex)
            {
                logger?.E($"Error: {ex.Flatten()}");
            }
        }

        private static bool UpdateCategoriesInMapping(cfEntitiesV2 db, List<WorkflowToUserMapping> workflowToUserMappings)
        {
            bool saveChanges = false;
            foreach (var group in workflowToUserMappings.GroupBy(wf => wf.User))
            {
                var workFlowIDs = group.ToList().Select(m => m.Workflow);

                var cfTrees = db.cfTree.Where(t => workFlowIDs.Contains(t.ID) && (t.isActive ?? true) && !(t.isDeleted ?? false));
                if (!cfTrees.Any()) continue;

                var workflowList = cfTrees.Select(t => t.Title).Distinct();

                var cfCategory = db.cfCategory.FirstOrDefault(c => c.ID == group.Key && (c.isActive ?? true) && !(c.isDeleted ?? false));
                if (cfCategory == null) continue;

                var dbWorkflowList = cfCategory.Type?.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Distinct() ?? new List<string>();

                var deletedWorkflows = dbWorkflowList.Except(workflowList);
                var addedWorkFlows = workflowList.Except(dbWorkflowList);

                if (!(deletedWorkflows.Any() || addedWorkFlows.Any())) continue;
                saveChanges = true;
                cfCategory.Type = String.Join(";", workflowList);
                db.Entry(cfCategory).State = System.Data.Entity.EntityState.Modified;
            }
            return saveChanges;
        }

        private static bool UpdateCategoriesNotInMapping(cfEntitiesV2 db, List<WorkflowToUserMapping> workflowToUserMappings)
        {
            var userIds = workflowToUserMappings.Select(wf => wf.User).Distinct();
            var categoriesNotInMapping = db.cfCategory.Where(c =>
                                                        !userIds.Contains(c.ID) && (c.Type ?? String.Empty) != String.Empty
                                                        && (c.isActive ?? true) && !(c.isDeleted ?? false)
                                                     );
            if (categoriesNotInMapping.Any())
            {
                foreach (var category in categoriesNotInMapping)
                {
                    category.Type = String.Empty;
                    db.Entry(category).State = System.Data.Entity.EntityState.Modified;
                }
                db.SaveChanges();
                return true;
            }
            return false;
        }
    }
}