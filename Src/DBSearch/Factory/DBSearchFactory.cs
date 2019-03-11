using System;
using System.Data;

namespace DBSearch
{
    internal class DBSearchFactory
    {
        public static IDbSearch CreateInstance(IDbConnection cnn, object searchText, Action<MatchColumnModel> action,
            ComparisonOperator comparisonOperator = ComparisonOperator.Equal, DBSearchSetting dBSearchSetting = null)
        {
            var connectionType = CheckDBConnectionTypeHelper.GetMatchDBType(cnn);
            if (connectionType == DBConnectionType.Oracle)
                return CreateInstance<OracleSearch>(cnn, searchText, action, comparisonOperator, dBSearchSetting);
            else if (connectionType == DBConnectionType.SqlServer)
                return CreateInstance<SQLServerSearch>(cnn, searchText, action, comparisonOperator, dBSearchSetting);
            else
                throw new Exception("Not Support DB Connection");
        }

        public static T CreateInstance<T>(IDbConnection cnn, object searchText, Action<MatchColumnModel> action,
            ComparisonOperator comparisonOperator = ComparisonOperator.Equal, DBSearchSetting dBSearchSetting = null)
            where T : DBSearchBase, new()
        {
            var dbsearch = new T
            {
                _connection = cnn,
                _searchText = searchText,
                _action = action
            };

            dbsearch._dbSearchSetting = dBSearchSetting ?? new DBSearchSetting();
            dbsearch._dbSearchSetting.comparisonOperator = comparisonOperator;

            if (dbsearch._dbSearchSetting.comparisonOperator == ComparisonOperator.Like && searchText is string)
                dbsearch._comparisonOperatorString = "Like";

            return dbsearch;
        }
    }

}
