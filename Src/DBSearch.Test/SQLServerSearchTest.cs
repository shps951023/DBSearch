using Dapper;
using System;
using System.Data.Common;
using System.Data.SqlClient;
using Xunit;

namespace DBSearch.Test
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

        /// <summary>
        /// 示範取得其中一筆資料,增加判斷的詳細度
        /// </summary>
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
