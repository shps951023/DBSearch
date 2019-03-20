using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace DBSearch
{



    internal abstract class DBSearchBase : IDbSearch
    {
        #region Props
        internal System.Data.IDbConnection _connection { get; set; }
        internal System.Data.IDbCommand _dbCommand { get; set; }
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
            using (this._dbCommand = this._connection.CreateCommand())
            {
                var parameter = this._dbCommand.CreateParameter();
                parameter.ParameterName = "@p";
                parameter.Value = this._searchText;
                this._dbCommand.Parameters.Add(parameter);

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
                            var matchCounts = QueryColumnGroupResultViewModel(tableName, col.COLUMN_NAME, searchTextValue)
                                .Where(w=>w != null)
                                .ToList();
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
        }

        #region private method
        private IEnumerable<ColumnsSchmaModel> GetTableSchmas()
        {
            var tableSchmasSQL = GetTableColumnsSchmasSQL(_searchText);

            this._dbCommand.CommandText = tableSchmasSQL;
            using (var reader = this._dbCommand.ExecuteReader())
            {
                while (reader.Read()) {
                    yield return new ColumnsSchmaModel()
                    {
                        TABLE_CATALOG = reader.GetString(0),
                        TABLE_SCHEMA = reader.GetString(1),
                        TABLE_NAME = reader.GetString(2),
                        TABLE_TYPE = reader.GetString(3),
                        COLUMN_NAME = reader.GetString(4),
                        IS_NULLABLE = reader.GetString(5),
                        DATA_TYPE = reader.GetString(6),
                    };
                }
            }
        }

        private IEnumerable<MatchColumnCountModel> QueryColumnGroupResultViewModel(string tableName, string column_name, string searchTextValue)
        {
            var matchCountSql = this.GetColumnMatchCountSQL(tableName, column_name, searchTextValue);

            this._dbCommand.CommandText = matchCountSql;
            using (var reader = this._dbCommand.ExecuteReader(CommandBehavior.SequentialAccess | CommandBehavior.SingleResult | CommandBehavior.SingleRow))
            {
                var data = (reader.Read() && reader.FieldCount != 0) ? new MatchColumnCountModel()
                    {
                        ColumnValue = reader.GetValue(0),
                        MatchCount = reader.GetInt32(1)
                    } : null;
                while (reader.Read()) { }
                while (reader.NextResult()) { }
                yield return data;
            }
        }

        private bool IsSearchTextInTable(string tableName, IGrouping<string, ColumnsSchmaModel> columns)
        {
            string exeistsCheckSql = GetIsSearchTextInTableSQL(tableName, columns);

            this._dbCommand.CommandText = exeistsCheckSql;

            var result = this._dbCommand.ExecuteScalar() != null  ;

            return result;
        }
        #endregion
    }

}
