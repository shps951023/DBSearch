using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;

namespace DBSearch
{
    internal  class DBSearchBase : IDbSearch
    {
        #region Props
        internal object _searchText { get; set; }
        internal DbConnection _connection { get; set; }
        internal DbCommand _dbCommand { get; set; }
        
        internal Action<MatchColumnModel> _action { get; set; }
        internal string _comparisonOperatorString { get; set; } = "=";
        internal DBSearchSetting _dbSearchSetting { get; set; }
        #endregion

        #region abstract method
        public virtual string GetColumnMatchCountSQL(string tableName, string column_name)
        {
            var matchCountSql = $@"
                        select [{column_name}] [ColumnValue],count(1) [MatchCount] from [{tableName}] with (nolock) 
                        where [{column_name}] {this._comparisonOperatorString} @p group by [{column_name}]; ";
            return matchCountSql;
        }

        public virtual string GetIsSearchTextInTableSQL(string tableName, IGrouping<string, ColumnsSchmaModel> columns)
        {
            var checkConditionSql = string.Join("or", columns.Select(
                //TODO:Add different type parameters according to the field type
                (column) => $" [{column.COLUMN_NAME}] {_comparisonOperatorString} @p ").ToArray()
            );

            var exeistsCheckSql = $"select top 1 1 from [{tableName}] with (nolock) where {checkConditionSql} ; "; /*Use with (nolock) to avoid locking tables*/
            return exeistsCheckSql;
        }

        public virtual object AddParameter(string name,object value)
        {              
            if (value == null)
                value = DBNull.Value;
            else if (value is string)
                if (this._dbSearchSetting.comparisonOperator == ComparisonOperator.Like)/*only string type need like check*/
                    value = $"%{value.ToString()}%";

            var parameter = (this._dbCommand).CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            this._dbCommand.Parameters.Add(parameter);

            return value;
        }
        #endregion

        public readonly static string TableColumnsSchmasSQL = @"
            select 
            	 T2.TABLE_CATALOG
                ,T2.TABLE_SCHEMA
                ,T2.TABLE_NAME
                ,T2.TABLE_TYPE
            	 ,T1.COLUMN_NAME,T1.IS_NULLABLE,T1.DATA_TYPE
            from INFORMATION_SCHEMA.COLUMNS T1 with (nolock)
            left join INFORMATION_SCHEMA.TABLES T2 on T1.TABLE_NAME = T2.TABLE_NAME
            where 1 =1 /* you can add your condition here*/
        ";

        public static string SupportNumberType = " 'bigint','numeric','smallint','decimal','smallmoney','int','tinyint','money','float','real' ";
        public static Dictionary<Type, string> SupportDBTypes = new Dictionary<Type, string> {
            { typeof(int), SupportNumberType },
            { typeof(long), SupportNumberType },
            { typeof(double), SupportNumberType },
            { typeof(decimal), SupportNumberType },
            { typeof(float), SupportNumberType },
            { typeof(string), " 'varchar' , 'nvarchar' " },
            { typeof(DateTime), " 'datetime' " },
            { typeof(bool), " 'bit' " }
        };

        public virtual string GetTableColumnsSchmasSQL()
        {
            var type = this._searchText.GetType();
            var sql = new StringBuilder(TableColumnsSchmasSQL);

            if (this._dbSearchSetting.ContainView == false)
               sql.Append(" and Table_Type = 'BASE TABLE' ");

            var datatypesCondtion = string.Empty;
            if (!SupportDBTypes.TryGetValue(type, out datatypesCondtion))
                throw new Exception($"DBSearch not support {type.Name} type");

            sql.Append($" and T1.DATA_TYPE in ({datatypesCondtion}) ");

            return sql.ToString();
        }

        public IEnumerable<MatchColumnModel> Search()
        {
            using (this._dbCommand = this._connection.CreateCommand())
            {
                var tableSchmas = GetTableSchmas().GroupBy(g => g.TABLE_NAME).ToList();
                var results = new List<MatchColumnModel>();

                AddParameter("@p", this._searchText);

                foreach (var columns in tableSchmas)
                {
                    var tableName = columns.Key;
                    var exeist = IsSearchTextInTable(tableName, columns);
                    if (exeist)
                    {
                        var firstCol = columns.First();
                        foreach (var col in columns)
                        {
                            var matchCounts = QueryColumnGroupResultViewModel(tableName, col.COLUMN_NAME)
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
            var tableSchmasSQL = GetTableColumnsSchmasSQL();

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

        private IEnumerable<MatchColumnCountModel> QueryColumnGroupResultViewModel(string tableName, string column_name)
        {
            var matchCountSql = this.GetColumnMatchCountSQL(tableName, column_name);

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

            //TODO:Check the auto parameter size is safe for all solution?
            this._dbCommand.CommandText = exeistsCheckSql;

            //SQL:
            //exec sp_executesql N'select top 1 1 from [Table1] with (nolock) where  [col_varchar] = @p 
            //or [col_varchar_like] = @p or [col_nvarchar] = @p or [col_null] = @p  ; ',N'@p nvarchar(4)',@p=N'Test'
            var result = this._dbCommand.ExecuteScalar() != null  ;

            return result;
        }
        #endregion
    }

}
