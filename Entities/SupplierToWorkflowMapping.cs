using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace cfFrame.ProjectCustoms.NeptuneLines.Entities
{
    public class SupplierToWorkflowMapping : ICustomEntity
    {
        public static readonly string TableName = "cf_SupplierToWorkflowMapping";
        public Guid cfEntityID => Guid.Parse("a326ac52-1ead-42ad-a9c9-08dadf39161d");
        public Guid Supplier { get; set; }
        public Guid Workflow { get; set; }
       
        public string RuleType { get; set; }
        public string RuleOperator { get; set; }
        private String RuleValues { get; set; }

        private List<string> _RuleValuesList; 
        public List<string> RuleValuesList
        {
            get
            {
                if (_RuleValuesList == null)
                {
                    _RuleValuesList = RuleValues?.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>();
                }
                return _RuleValuesList;
            }
        }
    }
}