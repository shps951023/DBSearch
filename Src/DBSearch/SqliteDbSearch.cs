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
                  (column) => $" {LeftSymbol}{column.ColumnName}{RightSymbol} {ComparisonOperator} {ParameterSymbol}p ").ToArray()
            );
            return $"select 1 from {LeftSymbol}{tableName}{RightSymbol}  where {checkConditionSql} LIMIT 1 ";
        }
    }
}
