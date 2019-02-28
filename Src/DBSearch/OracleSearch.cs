using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DBSearch
{
    internal class OracleSearch : DBSearchBase , IDbSearch
    {
        #region Props
        private static readonly string SupportNumberType = " 'LONG' , 'NUMBER' ";
        private static readonly Dictionary<Type, string> SupportDBTypes = new Dictionary<Type, string> {
               { typeof(int), SupportNumberType },
               { typeof(long), SupportNumberType },
               { typeof(double), SupportNumberType },
               { typeof(decimal), SupportNumberType },
               { typeof(float), SupportNumberType },
               { typeof(string), " 'NVARCHAR2' , 'VARCHAR2' " },
               //{ typeof(DateTime), " 'datetime' " },
               //{ typeof(bool), " 'bit' " }
          };
        #endregion

        internal override string GetTableColumnsSchmasSQL(object searchText)
        {
            var type = searchText.GetType();

            var condition = string.Empty;
            SupportDBTypes.TryGetValue(type, out condition);/*when use dictionary[key] if key not contatins in dictionary , it'll throw System.Collections.Generic.KeyNotFoundException',so i use TryGetValue*/
            if (condition == null)
                throw new Exception($"DBSearch not support {type.Name} type");

            var sql = new StringBuilder($@"
                select 
                    ' ' as TABLE_CATALOG
                    ,' ' as TABLE_SCHEMA
                    ,TABLE_NAME
                    ,case when T2.VIEW_NAME is null then 'BASE TABLE' else 'VIEW TABLE' end as TABLE_TYPE
                    ,COLUMN_NAME as COLUMN_NAME
                    ,NULLABLE as IS_NULLABLE
                    ,DATA_TYPE
                from user_tab_columns T1
                left join user_views T2 on T1.TABLE_NAME = T2.VIEW_NAME
                where 1=1
    
		  ");

            if (this._dbSearchSetting.ContainView == false)
                sql.Append(" and T2.VIEW_NAME is null ");

            sql.Append($" and T1.DATA_TYPE in ({condition}) ");

            return sql.ToString();
        }

        internal override string GetColumnMatchCountSQL(string tableName, string column_name, string searchTextValue)
        {
            var matchCountSql = $@"
                select ""{column_name}"" as ColumnValue,count(1) as MatchCount from ""{tableName}""
                where ""{column_name}"" {this._comparisonOperatorString} {searchTextValue} group by ""{column_name}"" ";
            return matchCountSql;
        }

        internal override string GetIsSearchTextInTableSQL(string tableName, IGrouping<string, ColumnsSchmaModel> columns)
        {
            var checkConditionSql = string.Join("or", columns.Select(column =>
            {
                string searchTextValue = ConvertSearchTextToDBValue(column.DATA_TYPE, this._searchText);
                return $@" ""{column.COLUMN_NAME}"" {_comparisonOperatorString} {searchTextValue} ";
            }));

            var exeistsCheckSql = $@"select 1 as value from ""{tableName}"" where 1=1 and rownum = 1 and ({checkConditionSql})  "; 
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

                if (columnDataType == "NVARCHAR2")
                    value = $"N'{searchText}'";
                else if (columnDataType == "VARCHAR2")
                    value = $"'{searchText}'";
            }
            else if (searchText is int || searchText is float || searchText is decimal || searchText is double)
                value = $"{searchText}";
            //else if (searchText is DateTime)
            //    value = $"'{((DateTime)searchText).ToString("s")}'";
            //else if (searchText is bool)
            //{
            //    var isTrue = (bool)searchText;
            //    value = isTrue ? "1" : "0";
            //}

            return value;
        }
    }
}
