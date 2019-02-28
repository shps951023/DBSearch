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

    internal class DBSearchFactory
    {
        public static T CreateInstance<T>(IDbConnection cnn, object searchText, Action<MatchColumnModel> action, ComparisonOperator comparisonOperator = ComparisonOperator.Equal, DBSearchSetting dBSearchSetting = null)
            where T : DBSearchBase, new()
        {
            var dbsearch = new T();
            if (dbsearch._dbSearchSetting == null)
                dbsearch._dbSearchSetting = new DBSearchSetting();
            else
                dbsearch._dbSearchSetting = dBSearchSetting;

            dbsearch._connection = cnn;
            dbsearch._searchText = searchText;
            dbsearch._action = action;
            dbsearch._comparisonOperator = comparisonOperator;
            if (dbsearch._comparisonOperator == ComparisonOperator.Equal)
                dbsearch._comparisonOperatorString = "=";
            else if (dbsearch._comparisonOperator == ComparisonOperator.Like)
            {
                if (searchText is string)
                    dbsearch._comparisonOperatorString = "Like";
                else
                    dbsearch._comparisonOperatorString = "=";
            }
            return dbsearch;
        }
    }

    internal abstract class DBSearchBase:IDbSearch
    {
        #region Props
        internal DBSearchSetting _dbSearchSetting { get; set; }
        internal System.Data.IDbConnection _connection { get; set; }
        internal object _searchText { get; set; }
        internal Action<MatchColumnModel> _action { get; set; }
        internal ComparisonOperator _comparisonOperator { get; set; }
        internal string _comparisonOperatorString { get; set; } = "";
        #endregion

        #region abstract method
        internal abstract string GetTableSchmasSQLBySearchTextType(object searchText);
        internal abstract bool QueryTableExeistsSearchText(IGrouping<string, TableSchmaModel> table, object tableName);
        internal abstract string ConvertSearchTextToDBValue(string columnDataType, object searchText);
        internal abstract IEnumerable<ColumnGroupResultViewModel> QueryColumnGroupResultViewModel(string tableName, string column_name, string comparisonOperatorString, string searchTextValue);
        #endregion


        private IEnumerable<IGrouping<string, TableSchmaModel>> GetTableSchmas()
        {
            var tableSchmasSQL = GetTableSchmasSQLBySearchTextType(_searchText);
            var result = this._connection.SqlQuery<TableSchmaModel>(tableSchmasSQL, reader => new TableSchmaModel()
            {
                TABLE_CATALOG = reader.GetString(0),
                TABLE_NAME = reader.GetString(1),
                TABLE_SCHEMA = reader.GetString(2),
                TABLE_TYPE = reader.GetString(3),
                COLUMN_NAME = reader.GetString(4),
                ORDINAL_POSITION = reader.GetInt32(5),
                IS_NULLABLE = reader.GetString(6),
                DATA_TYPE = reader.GetString(7),
            });
            return result.GroupBy(g => g.TABLE_NAME);
        }
        public IEnumerable<MatchColumnModel> Search()
        {
            var tableSchmas = GetTableSchmas();
            var results = new List<MatchColumnModel>();
            foreach (var table in tableSchmas)
            {
                var tableName = table.Key;
                var columnsNames = table.Select(s => s.COLUMN_NAME).ToList();

                var exeist = QueryTableExeistsSearchText(table, tableName);
                if (exeist)
                {
                    var firstCol = table.First();

                    foreach (var col in table)
                    {
                        string searchTextValue = ConvertSearchTextToDBValue(col.DATA_TYPE, _searchText);
                        var matchCounts = QueryColumnGroupResultViewModel(tableName, col.COLUMN_NAME, this._comparisonOperatorString, searchTextValue).ToList();
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
        
    }

    internal class SQLServerSearch : DBSearchBase,IDbSearch
    {

        internal override  string GetTableSchmasSQLBySearchTextType(object searchText)
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

        internal override string ConvertSearchTextToDBValue(string columnDataType, object searchText)
        {
            /*
                Q.Why did i spend so much code on match type?
                R:It avoid implicit conversion in poor performance.
                    if column type is nvarchar then remove N'' , if column type is varchar
                    then use without N''. it avoid implicit conversion affect performanceit let indexes that will be triggered
             */
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
        private static void IsDBCloumnTypeMatchSearchTextType(TableSchmaModel column, Type type)
        {
            /*if dbtype and searchtype are not match then throw exception. 
             *   ex: column type is string but searchText type is int , it does not make sense.*/
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

        internal override IEnumerable<ColumnGroupResultViewModel> QueryColumnGroupResultViewModel( string tableName, string column_name, string comparisonOperatorString, string searchTextValue)
        {
            var matchCountSql = $@"
                select [{column_name}] [ColumnValue],count(1) [MatchCount] from [{tableName}] with (nolock) 
                where [{column_name}] {comparisonOperatorString} {searchTextValue} group by [{column_name}]; ";
            var result = this._connection.SqlQuery<ColumnGroupResultViewModel>(matchCountSql, reader => new ColumnGroupResultViewModel()
            {
                ColumnValue = reader.GetValue(0),
                MatchCount = reader.GetInt32(1)
            });
            return result;
        }

        internal override bool QueryTableExeistsSearchText(IGrouping<string, TableSchmaModel> table, object tableName)
        {
            var checkSqlCondition = string.Join("or", table.Select(column =>
            {
                string searchTextValue = ConvertSearchTextToDBValue(column.DATA_TYPE, this._searchText);
                return $" [{column.COLUMN_NAME}] {_comparisonOperatorString} {searchTextValue} ";
            }));

            //Use with (nolock) to avoid locking tables
            var exeistsCheckSql = $"select top 1 1 from [{tableName}] with (nolock) where {checkSqlCondition} ; ";
            var result = this._connection.SqlQuerySingleOrDefault<int>(exeistsCheckSql) == 1;
            return result;
        }
    }
}
