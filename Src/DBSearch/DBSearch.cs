using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;

public static class DBSearch
{
    /// <summary>
    /// Only String Type Support Like Search
    /// </summary>
    public static IEnumerable<DbSearchResult> Search(this DbConnection connection, string searchText, bool likeSearch = false, Action<DbSearchResult> action = null)
    {
        return SearchImpl(connection, searchText, action, likeSearch);
    }

    public static IEnumerable<DbSearchResult> Search(this DbConnection connection, string searchText, Action<DbSearchResult> action)
    {
        return SearchImpl(connection, searchText, action, false);
    }

    public static IEnumerable<DbSearchResult> Search(this DbConnection connection, object searchText, Action<DbSearchResult> action=null)
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

public interface IDbSearch
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
             (column) => $" {LeftSymbol}{column.COLUMN_NAME}{RightSymbol} {ComparisonOperator} {ParameterSymbol}p ").ToArray()
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
             (column) => $" {LeftSymbol}{column.COLUMN_NAME}{RightSymbol} {ComparisonOperator} {ParameterSymbol}p ").ToArray()
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
             (column) => $" {LeftSymbol}{column.COLUMN_NAME}{RightSymbol} {ComparisonOperator} {ParameterSymbol}p ").ToArray()
        );
        return $"select top 1 1 from {LeftSymbol}{tableName}{RightSymbol}  where {checkConditionSql} ";
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
             (column) => $" {LeftSymbol}{column.COLUMN_NAME}{RightSymbol} {ComparisonOperator} {ParameterSymbol}p ").ToArray()
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
             (column) => $" {LeftSymbol}{column.COLUMN_NAME}{RightSymbol} {ComparisonOperator} {ParameterSymbol}p ").ToArray()
        );
        return $"select 1 from {LeftSymbol}{tableName}{RightSymbol}  where {checkConditionSql} Limit 1 ";
    }
}

internal class OracleDbSearch : DbBaseSearch, IDbSearch
{
    public OracleDbSearch(DbConnection connection, object searchText, Action<DbSearchResult> action, string comparisonOperator = "=", string leftSymbol = "", string rightSymbol = "", string parameterSymbol = "@")
    : base(connection,searchText,action,comparisonOperator, leftSymbol, rightSymbol, parameterSymbol) { }

    public override string GetCheckSQL(IGrouping<string, ConnectionColumn> columnDatas)
    {
        var tableName = columnDatas.Key;
        var checkConditionSql = string.Join("or", columnDatas.Select(
             (column) => $" {LeftSymbol}{column.COLUMN_NAME}{RightSymbol} {ComparisonOperator} {ParameterSymbol}p ").ToArray()
        );
        return $"select 1 from {LeftSymbol}{tableName}{RightSymbol}  where {checkConditionSql} rownum = 1 ";
    }
}

internal class DbBaseSearch : IDbSearch
{
    public DbConnection Connection { get; set; }
    public object SearchText { get; set; }
    public string ComparisonOperator { get; set; }
    public string LeftSymbol { get; set; }
    public string RightSymbol { get; set; }
    public string ParameterSymbol { get; set; }
    public Action<DbSearchResult> Action { get; set; }

    public DbBaseSearch(DbConnection connection, object searchText, Action<DbSearchResult> action, string comparisonOperator = "=", string leftSymbol = "", string rightSymbol = "", string parameterSymbol = "@")
    {
        this.Connection = connection;
        this.SearchText = searchText;
        this.Action = action;
        this.ComparisonOperator = comparisonOperator;
        this.LeftSymbol = leftSymbol;
        this.RightSymbol = rightSymbol;
        this.ParameterSymbol = parameterSymbol;
    }

    public virtual string GetCheckSQL(IGrouping<string, ConnectionColumn> columnDatas)
    {
        var tableName = columnDatas.Key;
        var checkConditionSql = string.Join("or", columnDatas.Select(
             (column) => $" {LeftSymbol}{column.COLUMN_NAME}{RightSymbol} {ComparisonOperator} {ParameterSymbol}p ").ToArray()
        );
        return $"select 1 from {LeftSymbol}{tableName}{RightSymbol}  where {checkConditionSql} ";
    }

    public IEnumerable<DbSearchResult> Search()
    {
        using (var command = Connection.CreateCommand())
        {
            var param = command.CreateParameter();
            param.ParameterName = $"{ParameterSymbol}p";
            param.Value = SearchText;
            command.Parameters.Add(param);

            var columns = GetConnectionColumns();
            columns.GroupBy(g => g.TABLE_NAME).Where(p =>
            {
                command.CommandText = GetCheckSQL(p);
                var exist = (command.ExecuteScalar() as int?) == 1;
                return exist;
            });

            var results = new List<DbSearchResult>();
            foreach (var column in columns)
            {
                var tableName = column.TABLE_NAME;
                var matchCountSql = $@"
                        select '{column.TABLE_SCHEMA}' {LeftSymbol}TABLE_SCHEMA{RightSymbol},'{column.TABLE_CATALOG}' {LeftSymbol}TABLE_CATALOG{RightSymbol},'{tableName}' {LeftSymbol}TABLE_NAME{RightSymbol},
							'{column.COLUMN_NAME}' {LeftSymbol}COLUMN_NAME{RightSymbol},count(1) {LeftSymbol}MatchCount{RightSymbol},
							'{column.DATA_TYPE}' {LeftSymbol}DATA_TYPE{RightSymbol},'{column.IS_NULLABLE}' {LeftSymbol}IS_NULLABLE{RightSymbol}
                                    ,{column.COLUMN_NAME} {LeftSymbol}COLUMN_NAME{RightSymbol}
						from {LeftSymbol}{tableName}{RightSymbol} 
                        where {LeftSymbol}{column.COLUMN_NAME}{RightSymbol} {ComparisonOperator} {ParameterSymbol}p group by {LeftSymbol}{column.COLUMN_NAME}{RightSymbol} ";
                command.CommandText = matchCountSql;

                var datas = new List<DbSearchResult>();
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var data = new DbSearchResult()
                        {
                            TABLE_SCHEMA = reader.GetString(0),
                            TABLE_CATALOG = reader.GetString(1),
                            TABLE_NAME = reader.GetString(2),
                            COLUMN_NAME = reader.GetString(3),
                            MatchCount = (!reader.IsDBNull(4)) ? reader.GetInt32(4) : 0,
                            DATA_TYPE = reader.GetString(5),
                            IS_NULLABLE = reader.GetString(6),
                            COLUMN_VALUE = reader.GetValue(7),
                        };
                        datas.Add(data);
                    }
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
        var table = Connection.GetSchema("Columns");
        var columns = table.Select().Select(s =>
             new ConnectionColumn
             {
                 TABLE_CATALOG = s["TABLE_CATALOG"] as string,
                 TABLE_SCHEMA = s["TABLE_SCHEMA"] as string,
                 TABLE_NAME = s["TABLE_NAME"] as string,
                 COLUMN_NAME = s["COLUMN_NAME"] as string,
                 DATA_TYPE = s["DATA_TYPE"] as string,
                 IS_NULLABLE = s["IS_NULLABLE"] as string,
                 CHARACTER_MAXIMUM_LENGTH = s["CHARACTER_MAXIMUM_LENGTH"] as string
             });

        //邏輯: 像是字串搜尋,不需要搜尋日期跟數字類型,也可以避免類型不一致導致error
        var searchType = SearchText.GetType();
        var types = GetConnectionTypeSchema();
        var usingType = types.Where(w => w.DataType == searchType.FullName).Select(s => s.TypeName);
        columns = columns.Where(w => usingType.Contains(w.DATA_TYPE));

        return columns;
    }

    public virtual IEnumerable<ConnectionTable> GetConnectionTable()
    {
        var table = Connection.GetSchema("Tables");
        var data = table.Select().Select(s =>
                       new ConnectionTable
                       {
                           TABLE_CATALOG = s["TABLE_CATALOG"] as string,
                           TABLE_SCHEMA = s["TABLE_SCHEMA"] as string,
                           TABLE_NAME = s["TABLE_NAME"] as string,
                           TABLE_TYPE = s["TABLE_TYPE"] as string
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

#region Models

public class DbSearchResult
{
    public string TABLE_SCHEMA { get; set; }
    public string TABLE_CATALOG { get; set; }
    public string TABLE_NAME { get; set; }
    public string COLUMN_NAME { get; set; }
    public object COLUMN_VALUE { get; set; }
    public int MatchCount { get; set; }
    public string DATA_TYPE { get; set; }
    public string IS_NULLABLE { get; set; }
}

public class ConnectionColumn
{
    public string TABLE_CATALOG { get; set; }
    public string TABLE_SCHEMA { get; set; }
    public string TABLE_NAME { get; set; }
    public string COLUMN_NAME { get; set; }
    public string DATA_TYPE { get; set; }
    public string IS_NULLABLE { get; set; }
    public string CHARACTER_MAXIMUM_LENGTH { get; set; }
}

public class ConnectionTable
{
    public string TABLE_CATALOG { get; set; }
    public string TABLE_SCHEMA { get; set; }
    public string TABLE_NAME { get; set; }
    public string TABLE_TYPE { get; set; }
}

public class ConnectionDataType
{
    public string DataType { get; set; }
    public string TypeName { get; set; }
}


#endregion

#region Extensions
public static class CheckDBConnectionTypeHelper
{
    private static readonly DBConnectionType DefaultAdapter = DBConnectionType.None;
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

public enum DBConnectionType
{
    SqlServer, SqlCeServer, Postgres, SQLite, MySql, Oracle, Firebird, None
}
#endregion

