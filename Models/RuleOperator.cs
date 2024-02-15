using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace cfFrame.ProjectCustoms.NeptuneLines.Models
{
    public enum RuleOperator
    {
        None,
        Equals,
        Contains,
        Greaterthan,
        GreaterOrEqual,
        LessThan,
        LessOrEqual,
        DoesNotContain,
        ItsNotEqual
    }
}