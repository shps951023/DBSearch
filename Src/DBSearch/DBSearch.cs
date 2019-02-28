using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace DBSearch
{
    public static partial class DBSearch
    {
        public static IEnumerable<MatchColumnModel> Search(this IDbConnection cnn, object searchText, Action<MatchColumnModel> action = null)
        {
            return DBSearchFactory.CreateInstance<SQLServerSearch>(cnn, searchText, action).Search();
        }

        public static IEnumerable<MatchColumnModel> Search(this IDbConnection cnn, object searchText, ComparisonOperator comparisonOperator, Action<MatchColumnModel> action = null)
        {
            return DBSearchFactory.CreateInstance<SQLServerSearch>(cnn, searchText, action, comparisonOperator).Search();
        }
    }
}
