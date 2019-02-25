using System;
using System.Collections.Generic;
using System.Data;

namespace DBSearch
{
    internal static class MiniSqlHelper
    {
        public static IEnumerable<T> SqlQuery<T>(this IDbConnection conn, string query, Func<IDataRecord, T> selector)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = query;
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        yield return selector(r);
                    }
                }
            }
        }

        public static T SqlQueryOneRow<T>(this IDbConnection conn, string query)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = query;
                using (var r = cmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        return (T)r[0];
                    }
                    else
                    {
                        return default(T);
                    }
                }
            }
        }
    }
}
