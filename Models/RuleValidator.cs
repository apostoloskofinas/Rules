using cfFrame.Models;
using CloudFin.Model.Engine;
using Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace cfFrame.ProjectCustoms.NeptuneLines.Models
{
    public class RuleValidator
    {

        private cfEntitiesV2 db;
        private Guid _vesselId { get; set; } = Guid.Empty;
        private cfCompany _vessel { get; set; }
        private DocumentInfo docdata;

        public RuleValidator(cfEntitiesV2 db, Guid vesselId, DocumentInfo docdata)
        {
            this.docdata = docdata;
            this.db = db;
            SetVessel(vesselId); 
        }

        public bool ValidateRule(Rule rule)
        {
            if (rule.Type == RuleType.None || rule.Operator == RuleOperator.None || !rule.Values.Any()) return false;

            switch (rule.Type)
            {
                case RuleType.Vessel:
                    return RunVesselRule(rule);
                case RuleType.TotalAmount:
                    return RunTotalAmountRule(rule);
                case RuleType.Text:
                    return RunTextRule(rule);
                default:
                    break;
            }
            
            return false;
        }

        private bool RunVesselRule(Rule rule)
        {
            if (_vessel == null) return false;

            var vesselFound = false;
            var wordFound = false;
            switch (rule.Operator)
            {
                case RuleOperator.Equals:
                    foreach (var value in rule.Values)
                    {
                        vesselFound = _vessel.Description.Equals(value,StringComparison.InvariantCultureIgnoreCase);
                        if (vesselFound) break;
                    }
                    break;
                case RuleOperator.Contains:
                    foreach (var value in rule.Values)
                    {
                        vesselFound = _vessel.Description.ToLower().Contains(value.ToLower());
                        if (vesselFound) break;
                    }
                    break;
                case RuleOperator.ItsNotEqual:
                    foreach (var value in rule.Values)
                    {
                        wordFound = _vessel.Description.Equals(value, StringComparison.InvariantCultureIgnoreCase);
                        if (wordFound) break;
                    }
                    vesselFound = !wordFound;
                    break;
                case RuleOperator.DoesNotContain:
                    foreach (var value in rule.Values)
                    {
                        wordFound = _vessel.Description.ToLower().Contains(value.ToLower());
                        if (wordFound) break;
                    }
                    vesselFound = !wordFound;
                    break;
            }
            return vesselFound;
        }

        private bool RunTotalAmountRule(Rule rule)
        {
            if (docdata.TotalAmount == null) return false;
            
            var totalFound = false;
            double _rulesParsedValue = 0;
            switch (rule.Operator)
            {
                case RuleOperator.Equals:
                    foreach (var value in rule.Values)
                    {
                        
                        var valueToParse = value.Replace('.', ',');
                        double.TryParse(valueToParse, out _rulesParsedValue); 
                        if (docdata.TotalAmount == _rulesParsedValue)
                        {
                            totalFound = true;
                            break;
                        }                            
                    }
                    break;
                case RuleOperator.Greaterthan:
                    foreach (var value in rule.Values)
                    {
                        var valueToParse = value.Replace('.', ',');
                        double.TryParse(valueToParse, out _rulesParsedValue);
                        if (docdata.TotalAmount > _rulesParsedValue)
                        {
                            totalFound = true;
                            break;
                        }
                    }
                    break;
                case RuleOperator.GreaterOrEqual:
                    foreach (var value in rule.Values)
                    {
                        var valueToParse = value.Replace('.', ',');
                        double.TryParse(valueToParse, out _rulesParsedValue);
                        if (docdata.TotalAmount >= _rulesParsedValue)
                        {
                            totalFound = true;
                            break;
                        }
                    }
                    break;
                case RuleOperator.LessThan:
                    foreach (var value in rule.Values)
                    {
                        var valueToParse = value.Replace('.', ',');
                        double.TryParse(valueToParse, out _rulesParsedValue);
                        if (docdata.TotalAmount < _rulesParsedValue)
                        {
                            totalFound = true;
                            break;
                        }
                    }
                    break;
                case RuleOperator.LessOrEqual:
                    foreach (var value in rule.Values)
                    {
                        var valueToParse = value.Replace('.', ',');
                        double.TryParse(valueToParse, out _rulesParsedValue);
                        if (docdata.TotalAmount <= _rulesParsedValue)
                        {
                            totalFound = true;
                            break;
                        }
                    }
                    break;
            }
            return totalFound;
        }
        private bool RunTextRule(Rule rule)
        {
            var ocrs = docdata.RawOcrs()?.Select(ocr=>ocr.ToLower()) ?? new List<string>();
            if (!ocrs.Any()) return false;

            var phraseFound = false;
            var rulePassed = false;

            switch (rule.Operator)
            {
                case RuleOperator.Contains:
                    foreach (var value in rule.Values.Select(v=>v.ToLower()))
                    {
                        foreach(var ocr in ocrs)
                        {
                            if (ocr.Contains(value))
                            {
                                rulePassed = true;
                                break;
                            }
                        }
                    }
                    break;
       
                case RuleOperator.DoesNotContain:
                    foreach (var value in rule.Values.Select(v => v.ToLower()))
                    {
                        foreach (var ocr in ocrs)
                        {
                            phraseFound = ocr.Contains(value);
                            if (phraseFound) break;
                        }
                        if (phraseFound) break;
                        
                    }
                    rulePassed = !phraseFound;
                    break;              
            }
            return rulePassed;
        } 

        private void SetVessel(Guid vesselId)
        {
            _vesselId = vesselId;
            _vessel = db.cfCompany.FirstOrDefault(c => c.ID == _vesselId && (c.isActive ?? true) && (!c.isDeleted ?? true));
        }
    }
}