using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace DBSearch
{
    public class DBSearchSetting
    {
        public bool ContainView { get; set; } = false;
        internal ComparisonOperator comparisonOperator { get; set; }
    }

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

    internal abstract class DBSearchBase : IDbSearch
    {
        #region Props
        internal System.Data.IDbConnection _connection { get; set; }
        internal object _searchText { get; set; }
        internal Action<MatchColumnModel> _action { get; set; }
        internal string _comparisonOperatorString { get; set; } = "=";
        internal DBSearchSetting _dbSearchSetting { get; set; }
        #endregion

        #region abstract method
        internal abstract string GetTableColumnsSchmasSQL(object searchText);
        internal abstract string GetColumnMatchCountSQL(string tableName, string column_name, string searchTextValue);
        internal abstract string GetIsSearchTextInTableSQL(string tableName, IGrouping<string, ColumnsSchmaModel> columns);
        internal abstract string ConvertSearchTextToDBValue(string columnDataType, object searchText);
        #endregion

        public IEnumerable<MatchColumnModel> Search()
        {
            var tableSchmas = GetTableSchmas().GroupBy(g => g.TABLE_NAME);
            var results = new List<MatchColumnModel>();
            foreach (var columns in tableSchmas)
            {
                var tableName = columns.Key;
                var exeist = IsSearchTextInTable(tableName, columns);
                if (exeist)
                {
                    var firstCol = columns.First();
                    foreach (var col in columns)
                    {
                        string searchTextValue = ConvertSearchTextToDBValue(col.DATA_TYPE, _searchText);
                        var matchCounts = QueryColumnGroupResultViewModel(tableName, col.COLUMN_NAME, searchTextValue).ToList();
                        foreach (var match in matchCounts)
                        {
                            var result = new MatchColumnModel()
                            {
                                Database = firstCol.TABLE_CATALOG,
                                Schema = firstCol.TABLE_SCHEMA,
                                TableName = tableName,
                                SearchValue = _searchText,
                                MatchCount = match.MatchCount,
                                ColumnName = col.COLUMN_NAME,
                                ColumnValue = match.ColumnValue
                            };

                            if (_action != null)
                                this._action(result);

                            results.Add(result);
                        }
                    }
                }
            }
            return results;
        }

        #region private method
        private IEnumerable<ColumnsSchmaModel> GetTableSchmas()
        {
            var tableSchmasSQL = GetTableColumnsSchmasSQL(_searchText);
            var result = this._connection.SqlQuery<ColumnsSchmaModel>(tableSchmasSQL, reader => new ColumnsSchmaModel()
            {
                TABLE_CATALOG = reader.GetString(0),
                TABLE_SCHEMA = reader.GetString(1),
                TABLE_NAME = reader.GetString(2),
                TABLE_TYPE = reader.GetString(3),
                COLUMN_NAME = reader.GetString(4),
                IS_NULLABLE = reader.GetString(5),
                DATA_TYPE = reader.GetString(6),
            });
            return result;
        }

        private IEnumerable<MatchColumnCountModel> QueryColumnGroupResultViewModel(string tableName, string column_name, string searchTextValue)
        {
            var matchCountSql = this.GetColumnMatchCountSQL(tableName, column_name, searchTextValue);
            var result = this._connection.SqlQuery<MatchColumnCountModel>(matchCountSql, reader => new MatchColumnCountModel()
            {
                ColumnValue = reader.GetValue(0),
                MatchCount = reader.GetInt32(1)
            });
            return result;
        }

        private bool IsSearchTextInTable(string tableName, IGrouping<string, ColumnsSchmaModel> columns)
        {
            string exeistsCheckSql = GetIsSearchTextInTableSQL(tableName, columns);
            var result = Convert.ToInt32(this._connection.SqlQuerySingleOrDefault(exeistsCheckSql));
            return result == 1;
        }
        #endregion
    }

}
