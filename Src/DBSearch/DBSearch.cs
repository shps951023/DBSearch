using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;

namespace DBSearch
{
    public static partial class DBSearch
    {
        public static IEnumerable<MatchColumnModel> Search(this DbConnection cnn
            , object searchText, Action<MatchColumnModel> action = null)
        {
            if (searchText == null) throw new ArgumentNullException(nameof(searchText));
            return DBSearchFactory.CreateInstance(cnn, searchText, action).Search();
        }

        public static IEnumerable<MatchColumnModel> Search(this DbConnection cnn, object searchText, ComparisonOperator comparisonOperator, Action<MatchColumnModel> action = null)
        {
            if (searchText == null) throw new ArgumentNullException(nameof(searchText));
            return DBSearchFactory.CreateInstance(cnn, searchText, action, comparisonOperator).Search();
        }

        public static IEnumerable<MatchColumnModel> Search(this DbConnection cnn, object searchText,DbType dbType, ComparisonOperator comparisonOperator, Action<MatchColumnModel> action = null)
        {
            if (searchText == null) throw new ArgumentNullException(nameof(searchText));
            return DBSearchFactory.CreateInstance(cnn, searchText, action, comparisonOperator).Search();
        }
    }
}
