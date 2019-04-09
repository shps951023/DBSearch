using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;

namespace DBSearch
{
    internal class SqlServerDbSearch : DbBaseSearch, IDbSearch
    {
        public SqlServerDbSearch(DbConnection connection, object searchText, Action<DBSearchResult> action, string comparisonOperator = "=", string leftSymbol = "", string rightSymbol = "", string parameterSymbol = "@")
        : base(connection, searchText, action, comparisonOperator, leftSymbol, rightSymbol, parameterSymbol) { }

        public override string GetCheckSQL(IGrouping<string, ConnectionColumn> columnDatas)
        {
            var tableName = columnDatas.Key;
            var checkConditionSql = string.Join("or", columnDatas.Select(
                  (column) => $" [{column.ColumnName}] {ComparisonOperator} @p ").ToArray()
            );
            return string.Format("select top 1 1 from [0] with (nolock)  where {1} ", tableName, checkConditionSql);
        }

        private readonly static Dictionary<Type, string> _MapperDictionary = new Dictionary<Type, string>()
        {
            {typeof(System.Int16)," 'smallint' "},
            {typeof(System.Int32)," 'int' "},
            {typeof(System.Single)," 'real' "},
            {typeof(System.Double)," 'float' "},
            {typeof(System.Decimal)," 'money','smallmoney','decimal','numeric' "},
            {typeof(System.Boolean)," 'bit' "},
            {typeof(System.SByte)," 'tinyint' "},
            {typeof(System.Int64)," 'bigint' "},
            {typeof(System.Byte[])," 'timestamp','binary','image','varbinary' "},
            {typeof(System.String)," 'text','ntext','xml','varchar','char','nchar','nvarchar' "},
            {typeof(System.DateTime)," 'datetime','smalldatetime','date','datetime2' "},
            {typeof(System.Object)," 'sql_variant' "},
            {typeof(System.Guid)," 'uniqueidentifier' "},
            {typeof(System.TimeSpan)," 'time' "},
            {typeof(System.DateTimeOffset)," 'datetimeoffset' "},
        };

        public override IEnumerable<ConnectionColumn> GetConnectionColumns()
        {
            var type = SearchText.GetType();

            if (!_MapperDictionary.TryGetValue(type, out string conditionSql))
                throw new NotSupportedException($"{type.FullName} not support");

            var sql = $@"
                    select 
	                    T2.TABLE_CATALOG 
                        ,T2.TABLE_SCHEMA 
                        ,T2.TABLE_NAME 
                        ,T1.COLUMN_NAME
                        ,T1.DATA_TYPE
	                   ,T1.IS_NULLABLE
                    from INFORMATION_SCHEMA.COLUMNS T1 with (nolock)
                    left join INFORMATION_SCHEMA.TABLES T2 with (nolock) on T1.TABLE_NAME = T2.TABLE_NAME
                    where 1 =1  and Table_Type = 'BASE TABLE' 
	                     and T1.DATA_TYPE in ({conditionSql}) 
                          {((SearchText is string)
                            ? $"and T1.CHARACTER_MAXIMUM_LENGTH >= {SearchText.ToString().Length} " /*If the maximum length is less than the data itself, it is not necessary to include the search*/
                            : ""
                          )}
                ";

            Command.CommandText = sql;

            var result = new List<ConnectionColumn>();
            using (var reader = Command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var data = new ConnectionColumn()
                    {
                        TableCatalog = reader.GetString(0),
                        TableSchema = reader.GetString(1),
                        TableName = reader.GetString(2),
                        ColumnName = reader.GetString(3),
                        DataType = reader.GetString(4),
                        IsNullable = reader.GetString(5),
                    };
                    result.Add(data);
                }
            }

            return result;
        }
    }
}
