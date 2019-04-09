using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBSearch
{
    internal class SqliteDbSearch : DbBaseSearch, IDbSearch
    {
        public SqliteDbSearch(DbConnection connection, object searchText, Action<DBSearchResult> action, string comparisonOperator = "=", string leftSymbol = "", string rightSymbol = "", string parameterSymbol = "@")
        : base(connection, searchText, action, comparisonOperator, leftSymbol, rightSymbol, parameterSymbol) { }

        public override string GetCheckSQL(IGrouping<string, ConnectionColumn> columnDatas)
        {
            var tableName = columnDatas.Key;
            var checkConditionSql = string.Join("or", columnDatas.Select(
                  (column) => $" [{column.ColumnName}] {ComparisonOperator} @p ").ToArray()
            );
            return $"select 1 from [{tableName}]  where {checkConditionSql} LIMIT 1 ";
        }

        private readonly static Dictionary<Type, string[]> _MapperDictionary = new Dictionary<Type, string[]>()
        {
            {typeof(System.Int16),new string [] {"smallint"}},
            {typeof(System.Int32),new string [] {"int"}},
            {typeof(System.Double),new string [] {"real","float","double"}},
            {typeof(System.Single),new string [] {"single"}},
            {typeof(System.Decimal),new string [] {"money","currency","decimal","numeric"}},
            {typeof(System.Boolean),new string [] {"bit","yesno","logical","bool","boolean"}},
            {typeof(System.Byte),new string [] {"tinyint"}},
            {typeof(System.Int64),new string [] {"integer","counter","autoincrement","identity","long","bigint"}},
            {typeof(System.Byte[]),new string [] {"binary","varbinary","blob","image","general","oleobject"}},
            {typeof(System.String),new string [] {"varchar","nvarchar","memo","longtext","note","text","ntext","string","char","nchar"}},
            {typeof(System.DateTime),new string [] {"datetime","smalldate","timestamp","date","time"}},
            {typeof(System.Guid),new string [] {"uniqueidentifier","guid"}},
        };

        public override IEnumerable<ConnectionColumn> GetConnectionColumns()
        {
            //Logic: like string search, no need to search date and numeric type, also can avoid error caused by type inconsistency
            var searchType = SearchText.GetType();

            if (!_MapperDictionary.TryGetValue(searchType, out string[] usingType))
                throw new NotSupportedException($"{searchType.FullName} not support");

            var table = Connection.GetSchema("Tables").Select()
                 .Where(w => (w["TABLE_TYPE"] as string).ToLower().IndexOf("table") != -1) /*The purpose is to filter out the View*/
                 .Select(s => (s["TABLE_NAME"] as string).ToLower()).ToArray();
            var columnSchma = Connection.GetSchema("Columns");
            var columns = Connection.GetSchema("Columns").Select().Where(w => table.Contains((w["TABLE_NAME"] as string).ToLower()))
                 .Where(w => usingType.Contains((w["DATA_TYPE"] as string).ToLower()))
                 .Select(s => new ConnectionColumn
                 {
                     TableCatalog = s["TABLE_CATALOG"] as string,
                     TableSchema = s["TABLE_SCHEMA"] as string,
                     TableName = s["TABLE_NAME"] as string,
                     ColumnName = s["COLUMN_NAME"] as string,
                     DataType = s["DATA_TYPE"] as string,
                     IsNullable = s["IS_NULLABLE"] as string,
                 });

            return columns;
        }
    }
}
