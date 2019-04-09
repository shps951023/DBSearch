using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;

namespace DBSearch
{
    internal class OracleDbSearch : DbBaseSearch, IDbSearch
    {
        public OracleDbSearch(DbConnection connection, object searchText, Action<DBSearchResult> action, string comparisonOperator = "=", string leftSymbol = "", string rightSymbol = "", string parameterSymbol = "@")
        : base(connection, searchText, action, comparisonOperator, leftSymbol, rightSymbol, parameterSymbol) { }

        public override string GetCheckSQL(IGrouping<string, ConnectionColumn> columnDatas)
        {
            var tableName = columnDatas.Key;
            var checkConditionSql = string.Join("or", columnDatas.Select(
                  (column) => $@" ""{column.ColumnName}"" {ComparisonOperator} :p ").ToArray()
            );
            var sql = $@"select 1 from ""{tableName}""  where 1=1 and ( {checkConditionSql} ) and rownum = 1 ";
            return sql;
        }

        private readonly static Dictionary<Type, string> _MapperDictionary = new Dictionary<Type, string>()
        {
            {typeof(System.Byte[])," 'BFILE','BLOB','LONG RAW','RAW' "},
            {typeof(System.Double)," 'BINARY_DOUBLE' "},
            {typeof(System.Single)," 'BINARY_FLOAT' "},
            {typeof(System.String)," 'CHAR','CLOB','LONG','NCHAR','NCLOB','NVARCHAR2','VARCHAR2','XMLTYPE','ROWID' "},
            {typeof(System.DateTime)," 'DATE','TIMESTAMP','TIMESTAMP(6)','TIMESTAMP(3)' "},
            {typeof(System.Decimal)," 'FLOAT','NUMBER' "},
        };

        public override IEnumerable<ConnectionColumn> GetConnectionColumns()
        {
            var type = SearchText.GetType();

            if (!_MapperDictionary.TryGetValue(type, out string conditionSql))
                throw new NotSupportedException($"{type.FullName} not support");

            var sql = $@"
                    select 
                        TABLE_NAME,
                        COLUMN_NAME,
                        DATA_TYPE,
                        NULLABLE as IS_NULLABLE
                    from user_tab_columns 
                    where 1=1 
                        and table_name not in (select View_name from user_views)
	                   and DATA_TYPE in ({conditionSql}) 
                        {((SearchText is string)
                            ? $"and DATA_LENGTH >= {SearchText.ToString().Length} " /*If the maximum length is less than the data itself, it is not necessary to include the search*/
                            : ""
                        )}
                ";
            Command.CommandText = sql;

            var result = new List<ConnectionColumn>();
            
            var connectionInfo = Connection.GetToStringValues();
            connectionInfo.TryGetValue("DatabaseName", out string databaseName);
            connectionInfo.TryGetValue("InstanceName", out string instanceName);

            using (var reader = Command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var data = new ConnectionColumn()
                    {
                        TableCatalog = databaseName,
                        TableSchema = instanceName,
                        TableName = reader.GetString(0),
                        ColumnName = reader.GetString(1),
                        DataType = reader.GetString(2),
                        IsNullable = reader.GetString(3),
                    };
                    result.Add(data);
                }
            }

            return result;
        }
    }
}
