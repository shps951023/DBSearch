using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;

namespace MiniSQLHelper
{
    public class MiniSqlBuilder
    {
        public Action<IDbCommand> SqlCommanAction { get; set; }
        public IList<Action<IDbDataParameter>> ParametersAction { get; set; }
        public IList<MiniSqlDataParameter> Parameters { get; set; }
        public IDbConnection Connection { get; set; }
        public StringBuilder AppendSQL { get; set; }
    }

    public sealed class MiniSqlDataParameter
    {
        public string ParameterName { get; set; }
        public object Value { get; set; }
        public ParameterDirection Direction { get; set; }
        public DbType? DbType { get; set; }
        public int? Size { get; set; }

        public byte? Precision { get; set; }
        public byte? Scale { get; set; }
    }

    public static class MiniSqlBuilderExtension
    {
        public static MiniSqlBuilder CreateCommand(this IDbConnection connection, Action<IDbCommand> sqlCommanAction)
        {
            var instance = new MiniSqlBuilder() { Connection = connection };
            instance.SqlCommanAction = sqlCommanAction;
            return instance;
        }

        public static MiniSqlBuilder CreateCommand(this IDbConnection connection, string sql, IDbTransaction transaction = null, int? commandTimeout = null, CommandType? commandType = null)
        {
            var instance = new MiniSqlBuilder() { Connection = connection };
            instance.SqlCommanAction = (command =>
            {
                command.CommandText = sql;
                if (transaction != null) command.Transaction = transaction;
                if (commandTimeout != null) command.CommandTimeout = command.CommandTimeout;
                if (commandType != null) command.CommandType = (CommandType)commandType;
            });
            return instance;
        }

        public static MiniSqlBuilder AddParameter(this MiniSqlBuilder instance, Action<IDbDataParameter> action)
        {
            if (instance.ParametersAction == null) instance.ParametersAction = new List<Action<IDbDataParameter>>();
            instance.ParametersAction.Add(action);
            return instance;
        }


        public static MiniSqlBuilder AddParameter(this MiniSqlBuilder instance, string parameterName, object value, DbType? dbType = null, int? size = null, ParameterDirection? direction = null, byte? precision = null, byte? scale = null)
        {
            if (instance.ParametersAction == null) instance.ParametersAction = new List<Action<IDbDataParameter>>();
            if (instance.Parameters == null) instance.Parameters = new List<MiniSqlDataParameter>();
            //TODO:parameter cache here by parameterName + value + dytype + size + direction + precision + scale like key
            var minisql = new MiniSqlDataParameter
            {
                ParameterName = parameterName,
                Value = value,
                Direction = direction ?? ParameterDirection.Input,
                DbType = dbType,
                Size = size,
                Precision = precision,
                Scale = scale
            };
            instance.Parameters.Add(minisql);
            return instance;
        }

        public static MiniSqlBuilder IF(this MiniSqlBuilder instance, bool isTrue, Action<MiniSqlBuilder> action)
        {
            if (isTrue) action(instance);
            return instance;
        }

        public static MiniSqlBuilder IF(this MiniSqlBuilder instance, Func<bool> isTrue, Action<MiniSqlBuilder> action)
        {
            if (isTrue()) action(instance);
            return instance;
        }

        public static MiniSqlBuilder AppendSQL(this MiniSqlBuilder instance, string appendSql)
        {
            if (instance.AppendSQL == null) instance.AppendSQL = new StringBuilder();
            instance.AppendSQL.Append(appendSql);
            return instance;
        }
    }

    /*public api method*/
    public static partial class MiniSqlHelper
    {
        public static IEnumerable<T> Query<T>(this MiniSqlBuilder instance, Func<IDataRecord, T> selector) => instance.QueryImpl<T>(selector);

        public static int Execute(this MiniSqlBuilder instance) => instance.ExecuteImpl();

        public static T ExecuteScalar<T>(this MiniSqlBuilder instance) => instance.ExecuteScalarImpl<T>();

        public static T QueryFirst<T>(this MiniSqlBuilder instance, Func<IDataRecord, T> selector) => instance.QueryRowImpl<T>(Row.First, selector);

        public static T QueryFirstOrDefault<T>(this MiniSqlBuilder instance, Func<IDataRecord, T> selector) => instance.QueryRowImpl<T>(Row.FirstOrDefault, selector);

        public static T QuerySingle<T>(this MiniSqlBuilder instance, Func<IDataRecord, T> selector) => instance.QueryRowImpl<T>(Row.Single, selector);

        public static T QuerySingleOrDefault<T>(this MiniSqlBuilder instance, Func<IDataRecord, T> selector) => instance.QueryRowImpl<T>(Row.SingleOrDefault, selector);
    }

    /*Implement*/
    public static partial class MiniSqlHelper
    {
        [Flags]
        private enum Row
        {
            First = 0,
            FirstOrDefault = 1, //  & FirstOrDefault != 0: allow zero rows
            Single = 2, // & Single != 0: demand at least one row
            SingleOrDefault = 3
        }

        private static T ExecuteScalarImpl<T>(this MiniSqlBuilder instance)
        {
            IDbConnection connection = instance.Connection;
            bool wasClosed = connection.State == ConnectionState.Closed;

            try
            {
                if (wasClosed) connection.Open();
                using (var cmd = connection.CreateCommand())
                {
                    instance.SqlCommanAction(cmd);
                    if (instance.AppendSQL != null)
                        cmd.CommandText += instance.AppendSQL.ToString();

                    CommandAddParameters(instance, cmd);

                    var result = cmd.ExecuteScalar();
                    return Parse<T>(result);
                }
            }
            finally /*Avoid not closing the connection*/
            {
                if (wasClosed)
                    connection.Close();
            }
        }

        private static T QueryRowImpl<T>(this MiniSqlBuilder instance, Row row, Func<IDataRecord, T> selector)
        {
            IDbConnection connection = instance.Connection;
            bool wasClosed = connection.State == ConnectionState.Closed;

            try
            {
                if (wasClosed) connection.Open();
                using (var cmd = connection.CreateCommand())
                {
                    instance.SqlCommanAction(cmd);
                    if (instance.AppendSQL != null)
                        cmd.CommandText += instance.AppendSQL.ToString();

                    CommandAddParameters(instance, cmd);

                    var result = default(T);
                    if (row == Row.Single || row == Row.SingleOrDefault)
                    {
                        using (var r = cmd.ExecuteReader(CommandBehavior.SequentialAccess | CommandBehavior.SingleResult))
                        {
                            var isSingle = true;
                            if (r.Read() && r.FieldCount != 0)
                            {
                                result = selector(r);
                                if (r.Read()) isSingle = false;
                            }

                            while (r.Read()) { }
                            while (r.NextResult()) { }

                            if (!isSingle) throw new InvalidOperationException("Sequence contains more than one element");
                            if (result == null && row == Row.Single && !isSingle) throw new InvalidOperationException("Sequence contains no elements");
                        }
                    }
                    else //if(row == Row.First || row == Row.FirstOrDefault)
                    {
                        using (var r = cmd.ExecuteReader(CommandBehavior.SequentialAccess | CommandBehavior.SingleResult | CommandBehavior.SingleRow))
                        {
                            var existData = false;
                            if (r.Read() && r.FieldCount != 0)
                            {
                                result = selector(r);
                                existData = true;
                            }

                            while (r.Read()) { }
                            while (r.NextResult()) { }

                            if (result == null && row == Row.First && !existData) throw new InvalidOperationException("Sequence contains no elements");
                        }
                    }

                    return result;
                }
            }
            finally /*Avoid not closing the connection*/
            {
                if (wasClosed)
                    connection.Close();
            }
        }

        private static int ExecuteImpl(this MiniSqlBuilder instance)
        {
            IDbConnection connection = instance.Connection;
            bool wasClosed = connection.State == ConnectionState.Closed;

            try
            {
                if (wasClosed) connection.Open();
                using (var cmd = connection.CreateCommand())
                {
                    instance.SqlCommanAction(cmd);
                    if (instance.AppendSQL != null)
                        cmd.CommandText += instance.AppendSQL.ToString();

                    CommandAddParameters(instance, cmd);

                    return cmd.ExecuteNonQuery();
                }
            }

            finally /*Avoid not closing the connection*/
            {
                if (wasClosed)
                    connection.Close();
            }
        }

        private static IEnumerable<T> QueryImpl<T>(this MiniSqlBuilder instance, Func<IDataRecord, T> selector)
        {
            IDbConnection connection = instance.Connection;
            bool wasClosed = connection.State == ConnectionState.Closed;

            try
            {
                if (wasClosed) connection.Open();
                using (var cmd = connection.CreateCommand())
                {
                    instance.SqlCommanAction(cmd);

                    if (instance.AppendSQL != null)
                        cmd.CommandText += instance.AppendSQL.ToString();

                    CommandAddParameters(instance, cmd);

                    using (var r = cmd.ExecuteReader())
                        while (r.Read())
                            yield return selector(r);
                }
            }
            finally /*Avoid not closing the connection*/
            {
                if (wasClosed)
                    connection.Close();
            }
        }

        private static T Parse<T>(object value)
        {
            if (value == null || value is DBNull) return default(T);
            if (value is T) return (T)value;
            var type = typeof(T);
            type = Nullable.GetUnderlyingType(type) ?? type;
            return (T)Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
        }

        private static void CommandAddParameters(MiniSqlBuilder instance, IDbCommand cmd)
        {
            if (instance.ParametersAction != null)
            {
                foreach (var action in instance.ParametersAction)
                {
                    var param = cmd.CreateParameter();
                    action(param);
                    cmd.Parameters.Add(param);
                }
            }

            if (instance.Parameters != null)
            {
                foreach (var item in instance.Parameters)
                {
                    var param = cmd.CreateParameter();
                    param.ParameterName = item.ParameterName;
                    param.Value = item.Value;
                    param.Direction = item.Direction;

                    if (item.DbType != null) param.DbType = (DbType)item.DbType;
                    if (item.Size != null) param.Size = (int)item.Size;
                    if (item.Scale != null) param.Scale = (byte)item.Scale;
                    if (item.Precision != null) param.Precision = (byte)item.Precision;

                    cmd.Parameters.Add(param);
                }
            }
        }

    }

    public static class MiniSqlHelperDataReaderExtensions
    {
        public static string GetStringNullCheck(this IDataRecord reader, int ordinal)
            => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
        public static int? GetInt32NullCheck(this IDataRecord reader, int ordinal)
            => reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal) as int?;
        public static bool? GetBooleanNullCheck(this IDataRecord reader, int ordinal)
            => reader.IsDBNull(ordinal) ? null : reader.GetBoolean(ordinal) as bool?;
        public static byte? GetGetByteNullCheck(this IDataRecord reader, int ordinal)
            => reader.IsDBNull(ordinal) ? null : reader.GetByte(ordinal) as byte?;
        public static char? GetCharNullCheck(this IDataRecord reader, int ordinal)
            => reader.IsDBNull(ordinal) ? null : reader.GetChar(ordinal) as char?;
        public static DateTime? GetDateTimeNullCheck(this IDataRecord reader, int ordinal)
            => reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal) as DateTime?;
        public static decimal? GetDecimalNullCheck(this IDataRecord reader, int ordinal)
            => reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal) as decimal?;
        public static double? GetDoubleNullCheck(this IDataRecord reader, int ordinal)
            => reader.IsDBNull(ordinal) ? null : reader.GetDouble(ordinal) as double?;
        public static float? GetFloatNullCheck(this IDataRecord reader, int ordinal)
            => reader.IsDBNull(ordinal) ? null : reader.GetFloat(ordinal) as float?;
        public static Guid? GetGuidNullCheck(this IDataRecord reader, int ordinal)
            => reader.IsDBNull(ordinal) ? null : reader.GetGuid(ordinal) as Guid?;
        public static short? GetGetInt16NullCheck(this IDataRecord reader, int ordinal)
            => reader.IsDBNull(ordinal) ? null : reader.GetInt16(ordinal) as short?;
        public static long? GetGetInt64NullCheck(this IDataRecord reader, int ordinal)
            => reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal) as long?;
        public static object GetGetValueNullCheck(this IDataRecord reader, int ordinal)
            => reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal);
    }
}
