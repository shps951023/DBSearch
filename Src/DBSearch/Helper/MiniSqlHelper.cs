using System;
using System.Collections.Generic;
using System.Data;

namespace DBSearch
{
    internal static class MiniSqlHelper
    {
        public static IEnumerable<T> SqlQuery<T>(this IDbConnection conn, string query, Func<IDataRecord, T> selector, object param = null)
        {
            bool wasClosed = conn.State == ConnectionState.Closed;
            if (wasClosed) conn.Open();
            using (var cmd = conn.CreateCommand())
            {
                AddParameters(param, cmd);
                cmd.CommandText = query;
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                        yield return selector(r);
            }
        }

        public static object SqlQuerySingleOrDefault(this IDbConnection conn, string query, object param = null)
        {
            bool wasClosed = conn.State == ConnectionState.Closed;
            if (wasClosed) conn.Open();
            using (var cmd = conn.CreateCommand())
            {
                AddParameters(param, cmd);
                cmd.CommandText = query;
                using (var r = cmd.ExecuteReader())
                    if (r.Read())
                        return r[0];
                    else
                        return null;
            }
        }

        private static void AddParameters(object param, IDbCommand cmd)
        {
            if (param != null)
            {
                var type = param.GetType();
                var props = type.GetProperties();
                foreach (var prop in props)
                {
                    var para = CreateParameter(cmd, prop.Name, prop.GetValue(param));
                    cmd.Parameters.Add(para);
                }
            }
        }

        private static IDbDataParameter CreateParameter(IDbCommand command, string parameterName, object value)
        {
            var parameter = command.CreateParameter();

            parameter.ParameterName = parameterName;
            parameter.Value = value;

            return parameter;
        }
    }
}
