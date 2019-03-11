using System;
using System.Collections.Generic;
using System.Text;

namespace DBSearch
{
    public class DBSearchSetting
    {
        public bool ContainView { get; set; } = false;
        internal ComparisonOperator comparisonOperator { get; set; }
    }
}
