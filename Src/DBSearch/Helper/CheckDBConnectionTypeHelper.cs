using System.Collections.Generic;
using System.Data;

namespace DBSearch
{
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
}
