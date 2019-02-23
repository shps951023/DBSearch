using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace DBSearch
{
    public class DBSearchSetting
    {
        public bool ContainView { get; set; } = false;
    }

    internal class SQLServerSearch : IDbSearch
    {
        #region Props
        private DBSearchSetting _dbSearchSetting { get; set; }
        private System.Data.IDbConnection _connection { get; set; }
        private object _searchText { get; set; }
        private Action<MatchColumnModel> _action { get; set; }
        private ComparisonOperator _comparisonOperator { get; set; }
        private string _comparisonOperatorString { get; set; } = "";
        #endregion
        public SQLServerSearch(IDbConnection cnn, object searchText, Action<MatchColumnModel> action, ComparisonOperator comparisonOperator = ComparisonOperator.Equal, DBSearchSetting dBSearchSetting = null)
        {
            if (_dbSearchSetting == null)
            {
                _dbSearchSetting = new DBSearchSetting();
            }
            else
            {
                _dbSearchSetting = dBSearchSetting;
            }

            _connection = cnn;
            _searchText = searchText;
            _action = action;
            _comparisonOperator = comparisonOperator;
            if (_comparisonOperator == ComparisonOperator.Equal)
            {
                _comparisonOperatorString = "=";
            }
            else if (_comparisonOperator == ComparisonOperator.Like)
            {
                //if null value only support string type
                if (searchText is string)
                {
                    _comparisonOperatorString = "Like";
                }
                else
                {
                    _comparisonOperatorString = "=";
                }
            }
        }
        public IEnumerable<MatchColumnModel> Search()
        {
            var tableSchmas = GetTableSchmas();
            var results = new List<MatchColumnModel>();
            foreach (var table in tableSchmas)
            {
                var tableName = table.Key;
                var columnsNames = table.Select(s => s.COLUMN_NAME).ToList();

                var exeist = QueryTableExeistsSearchText(_connection, table, tableName);
                if (exeist)
                {
                    var firstCol = table.First();

                    foreach (var col in table)
                    {
                        string searchTextValue = ConvertSearchTextToDBValue(col.DATA_TYPE, _searchText);
                        var matchCounts = QueryColumnGroupResultViewModel(_connection, tableName, col.COLUMN_NAME, this._comparisonOperatorString, searchTextValue).ToList();
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
                                ColumnQuerySQL = $"select * from [{tableName}] with (nolock) where [{col.COLUMN_NAME}] {_comparisonOperatorString} {searchTextValue} ; ",
                                ColumnValue = match.ColumnValue
                            };
                            if (_action != null)
                            {
                                this._action(result);
                            }

                            results.Add(result);
                        }
                    }
                }
            }
            return results;
        }

        private IEnumerable<IGrouping<string, TableSchmaModel>> GetTableSchmas()
        {
            var tableSchmasSQL = GetTableSchmasSQLBySearchTextType(_searchText);
            return QueryTableSchma(_connection,tableSchmasSQL).GroupBy(g => g.TABLE_NAME);
        }

        private IEnumerable<TableSchmaModel> QueryTableSchma(IDbConnection connection, string tableSchmasSQL)
        {
            using (IDbCommand command = connection.CreateCommand())
            {
                command.CommandText = tableSchmasSQL;
                command.CommandType = CommandType.Text;
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var tableSchma = new TableSchmaModel()
                        {
                            TABLE_CATALOG = reader.GetString(0),
                            TABLE_NAME = reader.GetString(1),
                            TABLE_SCHEMA = reader.GetString(2),
                            TABLE_TYPE = reader.GetString(3),
                            COLUMN_NAME = reader.GetString(4),
                            ORDINAL_POSITION = reader.GetInt32(5),
                            IS_NULLABLE = reader.GetString(6),
                            DATA_TYPE = reader.GetString(7),
                        };
                        yield return tableSchma;
                    }
                }
            }
        }

        private static readonly string SqlServerNumberType = " 'bigint','numeric','smallint','decimal','smallmoney','int','tinyint','money','float','real' ";
        private static readonly Dictionary<Type, string> DBTypes = new Dictionary<Type, string> {
               { typeof(int), SqlServerNumberType },
               { typeof(long), SqlServerNumberType },
               { typeof(double), SqlServerNumberType },
               { typeof(decimal), SqlServerNumberType },
               { typeof(float), SqlServerNumberType },
               { typeof(string), " 'varchar' , 'nvarchar' " },
               { typeof(DateTime), " 'datetime' " },
               { typeof(bool), " 'bit' " }
          };
        /// <remarks>
        ///Q.Why did i spend so much code on match type?
        ///R:It avoid implicit conversion in poor performance.
        ///  if column type is nvarchar then remove N'' , if column type is varchar
        ///  then use without N''. it avoid implicit conversion affect performanceit let indexes that will be triggered
        /// </remarks>
        internal string ConvertSearchTextToDBValue(string columnDataType, object searchText)
        {
            //TODO:the method has SQL injection
            if (searchText == null)
            {
                return "null";
            }

            string value = string.Empty;
            if (searchText is string)
            {
                if (_comparisonOperator == ComparisonOperator.Like)/*only string type need like check*/
                {
                    searchText = $"%{searchText}%";
                }

                if (columnDataType == "nvarchar")
                {
                    value = $"N'{searchText}'";
                }
                else if (columnDataType == "varchar")
                {
                    value = $"'{searchText}'";
                }
            }
            else if (searchText is int || searchText is float || searchText is decimal || searchText is double)
            {
                value = $"{searchText}";
            }
            else if (searchText is DateTime)
            {
                value = $"'{((DateTime)searchText).ToString("s")}'";
            }
            else if (searchText is bool)
            {
                var isTrue = (bool)searchText;
                value = isTrue ? "1" : "0";
            }

            return value;
        }
        /// <remarks>
        ///if dbtype and searchtype are not match then throw exception. 
        ///ex: column type is string but searchText type is int , it does not make sense.
        /// </remarks>
        private static void IsDBCloumnTypeMatchSearchTextType(TableSchmaModel column, Type type)
        {
            var dbType = string.Empty;
            DBTypes.TryGetValue(type, out dbType);
            if (dbType == null)
            {
                throw new Exception($"DBSearch not support {type.Name} type");
            }

            if (dbType.IndexOf(column.DATA_TYPE) == -1)
            {
                throw new Exception($"searchText and column DATA_TYPE are not match.");
            }
        }
        private string GetTableSchmasSQLBySearchTextType(object searchText)
        {
            System.Type type = searchText.GetType();

            //when use dictionary[key] if key not contatins in dictionary , it'll throw System.Collections.Generic.KeyNotFoundException: 'The given key 'System.Object' was not present in the dictionary.'
            //so i use TryGetValue
            var condition = string.Empty;
            DBTypes.TryGetValue(type, out condition);
            if (condition == null)
            {
                throw new Exception($"DBSearch not support {type.Name} type");
            }

            var sql = new StringBuilder($@"
			select 
				T2.TABLE_CATALOG,T2.TABLE_NAME,T2.TABLE_SCHEMA,T2.TABLE_TYPE
				,T1.COLUMN_NAME,T1.ORDINAL_POSITION,T1.IS_NULLABLE,T1.DATA_TYPE
			from INFORMATION_SCHEMA.COLUMNS T1 with (nolock)
			left join INFORMATION_SCHEMA.TABLES T2 on T1.TABLE_NAME = T2.TABLE_NAME
               where 1 =1 
		  ");

            if (this._dbSearchSetting.ContainView == false)
            {
                sql.Append(" and Table_Type = 'BASE TABLE' ");
            }

            sql.Append($" and T1.DATA_TYPE in ({condition}) ");
            if (searchText == null)
            {
                sql.Append(" and IS_NULLABLE = 'YES' ");
            }

            return sql.ToString();
        }
        private IEnumerable<ColumnGroupResultViewModel> QueryColumnGroupResultViewModel(IDbConnection connection, string tableName, string column_name, string comparisonOperatorString, string searchTextValue)
        {
            var matchCountSql = $@"
                select [{column_name}] [ColumnValue],count(1) [MatchCount] from [{tableName}] with (nolock) 
                where [{column_name}] {comparisonOperatorString} {searchTextValue} group by [{column_name}]; ";

            using (IDbCommand command = connection.CreateCommand())
            {
                command.CommandText = matchCountSql;
                command.CommandType = CommandType.Text;
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var column = new ColumnGroupResultViewModel()
                        {
                            ColumnValue = reader.GetValue(0),
                            MatchCount = reader.GetInt32(1)
                        };
                        yield return column;
                    }
                }
            }
        }
        private bool QueryTableExeistsSearchText(IDbConnection connection, IGrouping<string, TableSchmaModel> table, object tableName)
        {
            var checkSqlCondition = string.Join("or", table.Select(column =>
            {
                string searchTextValue = ConvertSearchTextToDBValue(column.DATA_TYPE, this._searchText);
                return $" [{column.COLUMN_NAME}] {_comparisonOperatorString} {searchTextValue} ";
            })); ;

            //Use with (nolock) to avoid locking tables
            var exeistsCheckSql = $"select top 1 1 from [{tableName}] with (nolock) where {checkSqlCondition} ; ";

            using (IDbCommand command = connection.CreateCommand())
            {
                command.CommandText = exeistsCheckSql;
                command.CommandType = CommandType.Text;
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return reader.GetInt32(0) == 1;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
        }
    }
}
