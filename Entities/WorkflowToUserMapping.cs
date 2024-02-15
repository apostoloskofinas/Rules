using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace cfFrame.ProjectCustoms.NeptuneLines.Entities
{
    public class WorkflowToUserMapping : ICustomEntity
    {
        public static readonly string TableName = "cf_WorkflowToUserMapping";
        public Guid cfEntityID => Guid.Parse("35085d21-2580-4681-a9d0-08dadf39161d");
        public Guid Workflow { get; set; }
        public Guid User { get; set; }

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