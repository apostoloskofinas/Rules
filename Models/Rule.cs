using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace cfFrame.ProjectCustoms.NeptuneLines.Models
{
    public class Rule
    {
        public Rule(string type, string @operator, List<string> values)
        {
            Enum.TryParse(type,true,out RuleType ruleType);
            Type = ruleType;
            Enum.TryParse(@operator, true, out RuleOperator ruleOperator);
            Operator = ruleOperator;
            Values = values;
        }

        public RuleType Type { get; set; }
        public RuleOperator Operator { get; set; }
        public List<string> Values { get; set; }
    }
}