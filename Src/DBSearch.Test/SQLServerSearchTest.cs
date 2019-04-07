using Dapper;
using System;
using System.Data.Common;
using System.Data.SqlClient;
using Xunit;

namespace DbSearch.Test
{
    public class SQLServerSearchTest
    {
        private static readonly string _connectionString =
            @"Data Source=(localdb)\MSSQLLocalDB;Integrated Security=SSPI;Initial Catalog=DBSearchTestDB;";

        private static DbConnection GetConnection()
        {
            var cnn = new SqlConnection(_connectionString);
            cnn.Open();
            return cnn;
        }

        [Fact]
        public void DBSearch()
        {
            using (var cnn = GetConnection())
            {
                var result = cnn.Search("Test");
            }
        }

        [Fact]
        public void DBSearchDatetime()
        {
            using (var cnn = GetConnection())
            {
                var result = cnn.Search(DateTime.Parse("2019/1/2 03:04:05"));
            }
        }

        [Fact]
        public void DBSearchSearchActionByDapper()
        {
            Replace("Test","Test");
        }

        static void Replace(object replaceValue,object newValue)
        {
            using (var scope = new System.Transactions.TransactionScope())
            using (var connection = GetConnection())
            {
                connection.Search(replaceValue, (result) =>
                {
                    var sql = $"Update {result.TABLE_NAME} set {result.COLUMN_NAME} = @newValue where {result.COLUMN_NAME} = @replaceValue";
                    connection.Execute(sql, new { replaceValue = result.COLUMN_VALUE, newValue }); //Using Dapper ORM
                });
                scope.Complete();
            }
        }

        [Fact]
        public void DBSearchSearchAction()
        {
            using (var tn = new System.Transactions.TransactionScope())
            using (var cnn = GetConnection())
            {
                cnn.Search("%Tes%" , true , (result) =>
                {
                    using (var command = cnn.CreateCommand())
                    {
                        command.CommandText = $"Update {result.TABLE_NAME} set {result.COLUMN_NAME} = @newValue where {result.COLUMN_NAME} = @oldValue";
                        {
                            var param = command.CreateParameter();
                            param.ParameterName = "newValue";
                            param.Value = result.COLUMN_VALUE;
                            command.Parameters.Add(param);
                        }
                        {
                            var param = command.CreateParameter();
                            param.ParameterName = "oldValue";
                            param.Value = result.COLUMN_VALUE;
                            command.Parameters.Add(param);
                        }

                        var effect = command.ExecuteNonQuery();
                    }              
                });
            }
        }
    }
}
