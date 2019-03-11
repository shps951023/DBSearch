using System;
using System.Collections.Generic;
using System.Data;

namespace DBSearch
{
    internal static class MiniSqlHelper
    {
        public static IEnumerable<T> SqlQuery<T>(this IDbConnection cnn, string query, Func<IDataRecord, T> selector, object param = null)
        {
            bool wasClosed = cnn.State == ConnectionState.Closed;
            if (wasClosed) cnn.Open();
            try
            {
                using (var cmd = cnn.CreateCommand())
                {
                    AddParameters(param, cmd);
                    cmd.CommandText = query;
                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                            yield return selector(r);
                }
            }
            finally /*Avoid not closing the connection*/
            {
                if (wasClosed) cnn.Close();
            }
        }

        public static object SqlQuerySingleOrDefault(this IDbConnection cnn, string query, object param = null)
        {
            bool wasClosed = cnn.State == ConnectionState.Closed;
            if (wasClosed) cnn.Open();
            try
            {
                using (var cmd = cnn.CreateCommand())
                {
                    AddParameters(param, cmd);
                    cmd.CommandText = query;
                    return cmd.ExecuteScalar();
                }
            }
            finally
            {
                if (wasClosed) cnn.Close();
            }
        }

        /// <summary>
        /// It is less efficient because of reflection
        /// </summary>
        private static void AddParameters(object param, IDbCommand cmd)
        {
            if (param != null)
            {
                var type = param.GetType();
                var props = TypePropertiesCacheHelper.GetTypePropertiesCache(type);
                foreach (var prop in props)
                {
                    var value = prop.GetValue(param, null);
                    var para = CreateParameter(cmd, prop.Name, value);
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
