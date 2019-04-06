using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using Xunit;
using Dapper;
using Newtonsoft.Json;

namespace DBSearch.Test
{
    public class SQLServerSearchTest
    {
        private static readonly string _connectionString =
            @"Data Source=(localdb)\MSSQLLocalDB;Integrated Security=SSPI;Initial Catalog=DBSearchTestDB;";

        private static DbConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }

        [Fact]
        public void DBSearch()
        {
            using (var cnn = GetConnection())
            {
                cnn.Open();
                var data = cnn.Search("Test");
                var data2 = cnn.Search(DateTime.Parse("2019/1/2 03:04:05"));
            }
        }

        /// <summary>
        /// 示範取得其中一筆資料,增加判斷的詳細度
        /// </summary>
        [Fact]
        public void DBSearchSearchAction()
        {
            using (var cnn = GetConnection())
            {
                cnn.Open();

                var results = new List<string>();
                var data = cnn.Search("Test",(result)=> {
                    var sql = $"select top 1 * from {result.TABLE_NAME} where {result.COLUMN_NAME} = @p";
                    var firstData = cnn.Query(sql, new { p = result.COLUMN_NAME }); /*By Dapper ORM Query*/

                    var json = JsonConvert.SerializeObject(firstData);/*By Json.NET*/
                    results.Add(json);
                });

                var expectedJson = @"[{""col_varchar"":""Test"",""col_varchar_like"":""Test2"",""col_nvarchar"":""測試"",""col_int"":123,""col_datetime"":""2019-01-02T03:04:05"",""col_bit"":true,""col_null"":null,""col_float"":1.2}]";
                Assert.Equal(expectedJson, results.First());
            }
        }
    }
}
