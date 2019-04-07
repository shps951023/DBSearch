using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;

namespace DbSearch
{
    #region Open Api
    public static class DbSearch
    {
        public static IEnumerable<DbSearchResult> Search(this DbConnection connection, string searchText, Action<DbSearchResult> action = null)
        {
            return SearchImpl(connection, searchText, action, true);
        }

        public static IEnumerable<DbSearchResult> Search(this DbConnection connection, object searchText, Action<DbSearchResult> action = null)
        {
            return SearchImpl(connection, searchText, action, false);
        }

        private static IEnumerable<DbSearchResult> SearchImpl(DbConnection connection, object searchText, Action<DbSearchResult> action, bool likeSearch)
        {
            IDbSearch db = null;
            var connectionType = CheckDBConnectionTypeHelper.GetMatchDBType(connection);
            switch (connectionType)
            {
                case DBConnectionType.SqlServer:
                case DBConnectionType.SqlCeServer:
                    db = new SqlServerDbSearch(connection, searchText, action, (likeSearch ? "like" : "="), "[", "]", "@"); break;
                case DBConnectionType.SQLite:
                    db = new SqliteDbSearch(connection, searchText, action, (likeSearch ? "like" : "="), "[", "]", "@"); break;
                case DBConnectionType.MySql:
                    db = new MySqlDbSearch(connection, searchText, action, (likeSearch ? "like" : "="), "`", "`", "@"); break;
                case DBConnectionType.Oracle:
                    db = new OracleDbSearch(connection, searchText, action, (likeSearch ? "like" : "="), "\"", "\"", ":"); break;
                case DBConnectionType.Firebird:
                    db = new FirebirdDbSearch(connection, searchText, action, (likeSearch ? "like" : "="), "\"", "\"", "?"); break;
                case DBConnectionType.Postgres:
                    db = new NpgSqlDbSearch(connection, searchText, action, (likeSearch ? "like" : "="), "\"", "\"", "@"); break;
                default:
                    db = new DbBaseSearch(connection, searchText, action, (likeSearch ? "like" : "="), "", "", "@"); break;
            }
            return db.Search();
        }
    }
    #endregion

    #region interface and implementation
    internal interface IDbSearch
    {
        IEnumerable<DbSearchResult> Search();
    }

    internal class MySqlDbSearch : DbBaseSearch, IDbSearch
    {
        public MySqlDbSearch(DbConnection connection, object searchText, Action<DbSearchResult> action, string comparisonOperator = "=", string leftSymbol = "", string rightSymbol = "", string parameterSymbol = "@")
        : base(connection, searchText, action, comparisonOperator, leftSymbol, rightSymbol, parameterSymbol) { }

        public override string GetCheckSQL(IGrouping<string, ConnectionColumn> columnDatas)
        {
            var tableName = columnDatas.Key;
            var checkConditionSql = string.Join("or", columnDatas.Select(
                 (column) => $" {LeftSymbol}{column.ColumnName}{RightSymbol} {ComparisonOperator} {ParameterSymbol}p ").ToArray()
            );
            return $"select  1 from {LeftSymbol}{tableName}{RightSymbol}  where {checkConditionSql} LIMIT 1 ";
        }
    }

    internal class FirebirdDbSearch : DbBaseSearch, IDbSearch
    {
        public FirebirdDbSearch(DbConnection connection, object searchText, Action<DbSearchResult> action, string comparisonOperator = "=", string leftSymbol = "", string rightSymbol = "", string parameterSymbol = "@")
        : base(connection, searchText, action, comparisonOperator, leftSymbol, rightSymbol, parameterSymbol) { }

        public override string GetCheckSQL(IGrouping<string, ConnectionColumn> columnDatas)
        {
            var tableName = columnDatas.Key;
            var checkConditionSql = string.Join("or", columnDatas.Select(
                 (column) => $" {LeftSymbol}{column.ColumnName}{RightSymbol} {ComparisonOperator} {ParameterSymbol}p ").ToArray()
            );
            return $"select  FIRST 1 from {LeftSymbol}{tableName}{RightSymbol}  where {checkConditionSql}  ";
        }
    }

    internal class SqlServerDbSearch : DbBaseSearch, IDbSearch
    {
        public SqlServerDbSearch(DbConnection connection, object searchText, Action<DbSearchResult> action, string comparisonOperator = "=", string leftSymbol = "", string rightSymbol = "", string parameterSymbol = "@")
        : base(connection, searchText, action, comparisonOperator, leftSymbol, rightSymbol, parameterSymbol) { }

        public override string GetCheckSQL(IGrouping<string, ConnectionColumn> columnDatas)
        {
            var tableName = columnDatas.Key;
            var checkConditionSql = string.Join("or", columnDatas.Select(
                 (column) => $" {LeftSymbol}{column.ColumnName}{RightSymbol} {ComparisonOperator} {ParameterSymbol}p ").ToArray()
            );
            return $"select top 1 1 from {LeftSymbol}{tableName}{RightSymbol}  where {checkConditionSql} ";
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
                    left join INFORMATION_SCHEMA.TABLES T2 on T1.TABLE_NAME = T2.TABLE_NAME
                    where 1 =1  and Table_Type = 'BASE TABLE' 
	                     and T1.DATA_TYPE in ({conditionSql}) 
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

    internal class SqliteDbSearch : DbBaseSearch, IDbSearch
    {
        public SqliteDbSearch(DbConnection connection, object searchText, Action<DbSearchResult> action, string comparisonOperator = "=", string leftSymbol = "", string rightSymbol = "", string parameterSymbol = "@")
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

    internal class NpgSqlDbSearch : DbBaseSearch, IDbSearch
    {
        public NpgSqlDbSearch(DbConnection connection, object searchText, Action<DbSearchResult> action, string comparisonOperator = "=", string leftSymbol = "", string rightSymbol = "", string parameterSymbol = "@")
        : base(connection, searchText, action, comparisonOperator, leftSymbol, rightSymbol, parameterSymbol) { }

        public override string GetCheckSQL(IGrouping<string, ConnectionColumn> columnDatas)
        {
            var tableName = columnDatas.Key;
            var checkConditionSql = string.Join("or", columnDatas.Select(
                 (column) => $" {LeftSymbol}{column.ColumnName}{RightSymbol} {ComparisonOperator} {ParameterSymbol}p ").ToArray()
            );
            return $"select 1 from {LeftSymbol}{tableName}{RightSymbol}  where {checkConditionSql} Limit 1 ";
        }
    }

    internal class OracleDbSearch : DbBaseSearch, IDbSearch
    {
        public OracleDbSearch(DbConnection connection, object searchText, Action<DbSearchResult> action, string comparisonOperator = "=", string leftSymbol = "", string rightSymbol = "", string parameterSymbol = "@")
        : base(connection, searchText, action, comparisonOperator, leftSymbol, rightSymbol, parameterSymbol) { }

        public override string GetCheckSQL(IGrouping<string, ConnectionColumn> columnDatas)
        {
            var tableName = columnDatas.Key;
            var checkConditionSql = string.Join("or", columnDatas.Select(
                 (column) => $" {LeftSymbol}{column.ColumnName}{RightSymbol} {ComparisonOperator} {ParameterSymbol}p ").ToArray()
            );
            return $"select 1 from {LeftSymbol}{tableName}{RightSymbol}  where {checkConditionSql} rownum = 1 ";
        }
    }

    internal class DbBaseSearch : IDbSearch
    {
        public DbConnection Connection { get; set; }
        public DbCommand Command { get; set; }

        public object SearchText { get; set; }
        public string ComparisonOperator { get; set; }
        public string LeftSymbol { get; set; }
        public string RightSymbol { get; set; }
        public string ParameterSymbol { get; set; }
        public Action<DbSearchResult> Action { get; set; }

        public DbBaseSearch(DbConnection connection, object searchText, Action<DbSearchResult> action, string comparisonOperator = "=", string leftSymbol = "", string rightSymbol = "", string parameterSymbol = "@")
        {
            Connection = connection;
            SearchText = searchText;
            Action = action;
            ComparisonOperator = comparisonOperator;
            LeftSymbol = leftSymbol;
            RightSymbol = rightSymbol;
            ParameterSymbol = parameterSymbol;
        }

        public virtual string GetCheckSQL(IGrouping<string, ConnectionColumn> columnDatas)
        {
            var tableName = columnDatas.Key;
            var checkConditionSql = string.Join("or", columnDatas.Select(
                 (column) => $" {LeftSymbol}{column.ColumnName}{RightSymbol} {ComparisonOperator} {ParameterSymbol}p ").ToArray()
            );
            return $"select 1 from {LeftSymbol}{tableName}{RightSymbol}  where {checkConditionSql} ";
        }

        public virtual IEnumerable<DbSearchResult> Search()
        {
            using (Command = Connection.CreateCommand())
            {
                var param = Command.CreateParameter();
                param.ParameterName = $"{ParameterSymbol}p";
                param.Value = SearchText;
                Command.Parameters.Add(param);

                var columns = GetConnectionColumns();
                columns.GroupBy(g => g.TableName).Where(p =>
                {
                    Command.CommandText = GetCheckSQL(p);
                    var exist = (Command.ExecuteScalar() as int?) == 1;
                    return exist;
                });

                var results = new List<DbSearchResult>();
                foreach (var column in columns)
                {
                    var tableName = column.TableName;
                    var matchCountSql = $@"
                        select '{column.TableSchema}' {LeftSymbol}TABLE_SCHEMA{RightSymbol},'{column.TableCatalog}' {LeftSymbol}TABLE_CATALOG{RightSymbol},'{tableName}' {LeftSymbol}TABLE_NAME{RightSymbol},
							'{column.ColumnName}' {LeftSymbol}COLUMN_NAME{RightSymbol},count(1) {LeftSymbol}MatchCount{RightSymbol},
							'{column.DataType}' {LeftSymbol}DATA_TYPE{RightSymbol},'{column.IsNullable}' {LeftSymbol}IS_NULLABLE{RightSymbol}
						from {LeftSymbol}{tableName}{RightSymbol} 
                        where {LeftSymbol}{column.ColumnName}{RightSymbol} {ComparisonOperator} {ParameterSymbol}p  ";
                    Command.CommandText = matchCountSql;

                    var datas = new List<DbSearchResult>();

                    using (var reader = Command.ExecuteReader(CommandBehavior.SequentialAccess | CommandBehavior.SingleResult | CommandBehavior.SingleRow))
                    {
                        if ((reader.Read() && reader.FieldCount != 0))
                        {
                            var data = new DbSearchResult()
                            {
                                TableSchema = reader.GetString(0),
                                TableCatalog = reader.GetString(1),
                                TableName = reader.GetString(2),
                                ColumnName = reader.GetString(3),
                                MatchCount = (!reader.IsDBNull(4)) ? reader.GetInt32(4) : 0,
                                DataType = reader.GetString(5),
                                IsNullable = reader.GetString(6),
                            };
                            if (data.MatchCount > 0)
                                datas.Add(data);
                        }
                        while (reader.Read()) { }
                        while (reader.NextResult()) { }
                    }

                    foreach (var item in datas)
                        Action?.Invoke(item);

                    results.AddRange(datas);
                }
                return results;
            }
        }

        public virtual IEnumerable<ConnectionColumn> GetConnectionColumns()
        {
            var table = GetConnectionTable();
            var columns = Connection.GetSchema("Columns").Select().Select(s =>
                 new ConnectionColumn
                 {
                     TableCatalog = s["TABLE_CATALOG"] as string,
                     TableSchema = s["TABLE_SCHEMA"] as string,
                     TableName = s["TABLE_NAME"] as string,
                     ColumnName = s["COLUMN_NAME"] as string,
                     DataType = s["DATA_TYPE"] as string,
                     IsNullable = s["IS_NULLABLE"] as string,
                 }).Join(table,t1 => new { t1.TableCatalog ,t1.TableSchema,t1.TableName},
                    t2 => new { t2.TableCatalog, t2.TableSchema, t2.TableName },(t1,t2)=> t1
                 ); /*only need table type*/

            //Logic: like string search, no need to search date and numeric type, also can avoid error caused by type inconsistency
            var searchType = SearchText.GetType();
            var types = GetConnectionTypeSchema();
            var usingType = types.Where(w => w.DataType == searchType.FullName).Select(s => s.TypeName);
            columns = columns.Where(w => usingType.Contains(w.DATA_TYPE));

            return columns;
        }

        public virtual IEnumerable<ConnectionTable> GetConnectionTable()
        {
            var table = Connection.GetSchema("Tables");
            var data = table.Select()
                 .Where(w => (w["TABLE_TYPE"] as string).ToLower().IndexOf("table") != -1) /*The purpose is to filter out the View*/
                 .Select(s =>
                    new ConnectionTable
                    {
                        TableCatalog = s["TABLE_CATALOG"] as string,
                        TableSchema = s["TABLE_SCHEMA"] as string,
                        TableName = s["TABLE_NAME"] as string,
                        TableType = s["TABLE_TYPE"] as string
                    });
            return data;
        }

        public virtual IEnumerable<ConnectionDataType> GetConnectionTypeSchema()
        {
            var DataTypes = Connection.GetSchema("DataTypes");
            var data = DataTypes.Select().Select(s => new ConnectionDataType { DataType = s["DataType"] as string, TypeName = s["TypeName"] as string })
                 .Where(w => w.DataType != null);
            return data;
        }
    }
    #endregion

    #region Models

    public class DbSearchResult
    {
        public string TableSchema { get; set; }
        public string TableCatalog { get; set; }
        public string TableName { get; set; }
        public string ColumnName { get; set; }
        public int MatchCount { get; set; }
        public string DataType { get; set; }
        public string IsNullable { get; set; }
    }

    internal class ConnectionColumn
    {
        public string TableCatalog { get; set; }
        public string TableSchema { get; set; }
        public string TableName { get; set; }
        public string ColumnName { get; set; }
        public string DataType { get; set; }
        public string IsNullable { get; set; }
    }

    internal class ConnectionTable
    {
        public string TableCatalog { get; set; }
        public string TableSchema { get; set; }
        public string TableName { get; set; }
        public string TableType { get; set; }
    }

    internal class ConnectionDataType
    {
        public string DataType { get; set; }
        public string TypeName { get; set; }
    }


    #endregion

    #region Extensions
    internal static class CheckDBConnectionTypeHelper
    {
        private static readonly DBConnectionType DefaultAdapter = DBConnectionType.Unknown;
        private static readonly Dictionary<string, DBConnectionType> AdapterDictionary
              = new Dictionary<string, DBConnectionType>
              {
                  ["oracleconnection"] = DBConnectionType.Oracle,
                  ["sqlconnection"] = DBConnectionType.SqlServer,
                  ["sqlceconnection"] = DBConnectionType.SqlCeServer,
                  ["npgsqlconnection"] = DBConnectionType.Postgres,
                  ["sqliteconnection"] = DBConnectionType.SQLite,
                  ["mysqlconnection"] = DBConnectionType.MySql,
                  ["fbconnection"] = DBConnectionType.Firebird
              };
        public static DBConnectionType GetMatchDBType(IDbConnection connection)
        {
            var name = connection.GetType().Name.ToLower();
            return !AdapterDictionary.ContainsKey(name) ? DefaultAdapter : AdapterDictionary[name];
        }
    }

    internal enum DBConnectionType
    {
        SqlServer, SqlCeServer, Postgres, SQLite, MySql, Oracle, Firebird, Unknown
    }
    #endregion
}


