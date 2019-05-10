using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using DbSqlHelper;

namespace DBSearch
{
    #region Open Api
    public static class DBSearch
    {
        public static IEnumerable<DBSearchResult> Search(this DbConnection connection, string searchText, bool likeSearch, Action<DBSearchResult> action = null, int connectionCount = 1, string connectionString = null)
        {
            return Search(connection, searchText, action, likeSearch, connectionCount, connectionString);
        }

        public static IEnumerable<DBSearchResult> Search(this DbConnection connection, string searchText, Action<DBSearchResult> action = null, int connectionCount = 1, string connectionString = null)
        {
            return Search(connection, searchText, action, true, connectionCount, connectionString);
        }

        public static IEnumerable<DBSearchResult> Search(this DbConnection connection, object searchText, Action<DBSearchResult> action = null, int connectionCount = 1,string connectionString = null)
        {
            return Search(connection, searchText, action, false, connectionCount, connectionString);
        }

        private static IEnumerable<DBSearchResult> Search(DbConnection connection, object searchText, Action<DBSearchResult> action, bool likeSearch, int connectionCount, string connectionString)
        {
            DbBaseSearch db = null;
            var connectionType = connection.GetDbConnectionType();
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
                    db = new PgSqlDbSearch(connection, searchText, action, (likeSearch ? "like" : "="), "\"", "\"", "@"); break;
                default:
                    db = new DbBaseSearch(connection, searchText, action, (likeSearch ? "like" : "="), "", "", "@"); break;
            }
            db.ConnectionCount = connectionCount;
            db.ConnectionString = connectionString;
            db.DBConnectionType = connectionType;
            return db.Search();
        }
    }
    #endregion

    #region interface and implementation
    internal interface IDbSearch
    {
        IEnumerable<DBSearchResult> Search();
    }

    internal class DbBaseSearch : IDbSearch
    {
        public DbConnection Connection { get; set; }
        public string ConnectionString { get; set; }
        public DbCommand Command { get; set; }
        public DBConnectionType DBConnectionType { get; set; }

        public object SearchText { get; set; }
        public string ComparisonOperator { get; set; }
        public string LeftSymbol { get; set; }
        public string RightSymbol { get; set; }
        public string ParameterSymbol { get; set; }
        public Action<DBSearchResult> Action { get; set; }
        public int ConnectionCount { get; set; } = 1;

        public DbBaseSearch(DbConnection connection, object searchText, Action<DBSearchResult> action, string comparisonOperator = "=", string leftSymbol = "", string rightSymbol = "", string parameterSymbol = "@")
        {
            Connection = connection;
            SearchText = searchText;
            Action = action;
            ComparisonOperator = comparisonOperator;
            LeftSymbol = leftSymbol;
            RightSymbol = rightSymbol;
            ParameterSymbol = parameterSymbol;
        }

        public virtual IEnumerable<DBSearchResult> Search()
        {
            if (this.ConnectionString == null)
                this.ConnectionString = Connection.ConnectionString;

            bool wasClosed = Connection.State == ConnectionState.Closed;
            if (wasClosed) Connection.Open();

            using (Command = Connection.CreateCommand())
            {
                AddParameter(Command);

                var columns = new ConcurrentBag<ConnectionColumn>();
                if (ConnectionCount == 1)
                {
                    var list = GetConnectionColumns().GroupBy(g => g.TableName).ToList();
                    list.ForEach(p =>
                    {
                        Command.CommandText = GetCheckSQL(p);
                        var exist = Convert.ToInt32(Command.ExecuteScalar()) == 1;
                        if (exist)
                            foreach (var item in p)
                                columns.Add(item);
                    });
                }
                else if (ConnectionCount > 1)
                {
                    var connectionType = Connection.GetType();
                    var _columnsList = GetConnectionColumns().GroupBy(g => g.TableName).ToList();
                    _columnsList.GroupBy(g => _columnsList.IndexOf(g) % ConnectionCount).AsParallel().ForAll(s =>
                    {
                        using (var _connection = (Activator.CreateInstance(connectionType, this.ConnectionString) as DbConnection))
                        {
                            using (var _command = _connection.CreateCommand())
                            {
                                _connection.ConnectionString = this.ConnectionString;
                                AddParameter(_command);
                                _connection.Open();

                                foreach (var p in s)
                                {
                                    _command.CommandText = GetCheckSQL(p);
                                    var exist = Convert.ToInt32(_command.ExecuteScalar()) == 1;
                                    if (exist)
                                        foreach (var item in p)
                                            columns.Add(item);
                                }
                            }
                        }
                    });
                }

                var results = new List<DBSearchResult>();
                foreach (var column in columns)
                {
                    var tableName = column.TableName;
                    string matchCountSql = GetMatchCountSql(column, tableName);
                    Command.CommandText = matchCountSql;

                    var matchCount = Convert.ToInt64(Command.ExecuteScalar());
                    if (matchCount > 0)
                    {
                        var data = new DBSearchResult()
                        {
                            TableSchema = column.TableSchema,
                            TableCatalog = column.TableCatalog,
                            TableName = tableName,
                            ColumnName = column.ColumnName,
                            MatchCount = matchCount,
                            DataType = column.DataType,
                            IsNullable = column.IsNullable,
                        };
                        results.Add(data);

                        Action?.Invoke(data);
                    }
                }
                return results;
            }
        }

        public virtual string GetCheckSQL(IGrouping<string, ConnectionColumn> columnDatas)
        {
            var tableName = columnDatas.Key;
            var checkConditionSql = string.Join("or", columnDatas.Select(
                  (column) => $" {LeftSymbol}{column.ColumnName}{RightSymbol} {ComparisonOperator} {ParameterSymbol}p ").ToArray()
            );
            return $"select 1 from {LeftSymbol}{tableName}{RightSymbol}  where {checkConditionSql} ";
        }

        public virtual string GetMatchCountSql(ConnectionColumn column, string tableName)
        {
            return $@"select count(1) MatchCount 
				    from {LeftSymbol}{tableName}{RightSymbol}  
                        where {LeftSymbol}{column.ColumnName}{RightSymbol} {ComparisonOperator} {ParameterSymbol}p  ";
        }

        private bool IsSqlServer()
        {
            return this.DBConnectionType == DBConnectionType.SqlCeServer || this.DBConnectionType == DBConnectionType.SqlServer;
        }

        private void AddParameter(DbCommand command)
        {
            var param = command.CreateParameter();
            param.ParameterName = $"{ParameterSymbol}p";
            param.Value = SearchText;
            command.Parameters.Add(param);
        }

        public virtual IEnumerable<ConnectionColumn> GetConnectionColumns()
        {
            //Logic: like string search, no need to search date and numeric type, also can avoid error caused by type inconsistency
            var searchType = SearchText.GetType();
            var dataTypes = Connection.GetSchema("DataTypes").Select().Where(w => (w["DataType"] as string) != null);
            var usingType = dataTypes.Where(w => (w["DataType"] as string) == searchType.FullName).Select(s => (s["TypeName"] as string).ToLower()).ToArray();

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
    #endregion

    #region Models

    public class DBSearchResult
    {
        public string TableSchema { get; set; }
        public string TableCatalog { get; set; }
        public string TableName { get; set; }
        public string ColumnName { get; set; }
        public Int64 MatchCount { get; set; }
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
    #endregion
}


