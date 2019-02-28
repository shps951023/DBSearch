using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DBSearch
{
    internal class SQLServerSearch : DBSearchBase , IDbSearch
    {
        #region Props
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
        #endregion

        internal override string GetTableColumnsSchmasSQL(object searchText)
        {
            var type = searchText.GetType();

            var condition = string.Empty;
            DBTypes.TryGetValue(type, out condition);/*when use dictionary[key] if key not contatins in dictionary , it'll throw System.Collections.Generic.KeyNotFoundException',so i use TryGetValue*/
            if (condition == null)
                throw new Exception($"DBSearch not support {type.Name} type");

            var sql = new StringBuilder($@"
			select 
				T2.TABLE_CATALOG,T2.TABLE_NAME,T2.TABLE_SCHEMA,T2.TABLE_TYPE
				,T1.COLUMN_NAME,T1.IS_NULLABLE,T1.DATA_TYPE
			from INFORMATION_SCHEMA.COLUMNS T1 with (nolock)
			left join INFORMATION_SCHEMA.TABLES T2 on T1.TABLE_NAME = T2.TABLE_NAME
               where 1 =1 
		  ");

            if (this._dbSearchSetting.ContainView == false)
                sql.Append(" and Table_Type = 'BASE TABLE' ");

            sql.Append($" and T1.DATA_TYPE in ({condition}) ");
            if (searchText == null)
                sql.Append(" and IS_NULLABLE = 'YES' ");

            return sql.ToString();
        }

        internal override string GetColumnMatchCountSQL(string tableName, string column_name, string searchTextValue)
        {
            var matchCountSql = $@"
                select [{column_name}] [ColumnValue],count(1) [MatchCount] from [{tableName}] with (nolock) 
                where [{column_name}] {this._comparisonOperatorString} {searchTextValue} group by [{column_name}]; ";
            return matchCountSql;
        }

        internal override string GetIsSearchTextInTableSQL(string tableName, IGrouping<string, ColumnsSchmaModel> columns)
        {
            var checkConditionSql = string.Join("or", columns.Select(column =>
            {
                string searchTextValue = ConvertSearchTextToDBValue(column.DATA_TYPE, this._searchText);
                return $" [{column.COLUMN_NAME}] {_comparisonOperatorString} {searchTextValue} ";
            }));

            var exeistsCheckSql = $"select top 1 1 from [{tableName}] with (nolock) where {checkConditionSql} ; "; /*Use with (nolock) to avoid locking tables*/
            return exeistsCheckSql;
        }

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
                return "null";

            string value = string.Empty;
            if (searchText is string)
            {
                if (this._dbSearchSetting.comparisonOperator == ComparisonOperator.Like)/*only string type need like check*/
                    searchText = $"%{searchText}%";

                if (columnDataType == "nvarchar")
                    value = $"N'{searchText}'";
                else if (columnDataType == "varchar")
                    value = $"'{searchText}'";
            }
            else if (searchText is int || searchText is float || searchText is decimal || searchText is double)
                value = $"{searchText}";
            else if (searchText is DateTime)
                value = $"'{((DateTime)searchText).ToString("s")}'";
            else if (searchText is bool)
            {
                var isTrue = (bool)searchText;
                value = isTrue ? "1" : "0";
            }

            return value;
        }
    }
}
