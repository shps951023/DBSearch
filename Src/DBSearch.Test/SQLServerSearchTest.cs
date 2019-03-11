using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using Xunit;

namespace DBSearch.Test
{
    public class DBSearchTestSetting
    {
        internal static readonly string _connectionString = 
            @"Data Source=(localdb)\MSSQLLocalDB;Integrated Security=SSPI;Initial Catalog=DBSearchTestDB;";
    }

    public class SQLServerSearchTest
    {

        [Fact]
        public void DBSearchSearchActionTest()
        {
            using (var cnn = new SqlConnection(DBSearchTestSetting._connectionString))
            {
                cnn.Search("Test", columnData =>
                {
                    Assert.Contains(columnData.TableName, new[] { "Table1", "Table2" });
                });

                cnn.Search("Test", ComparisonOperator.Like, columnData =>
                {
                    Assert.Contains(columnData.TableName, new[] { "Table1", "Table2" });
                });
            }
        }

        [Fact]
        public void DBSearchSearchTest()
        {
            using (var cnn = new SqlConnection(DBSearchTestSetting._connectionString))
            {
                cnn.Open();
                {
                    var result = cnn.Search("Test", ComparisonOperator.Like);
                    var table1 = result.Where(w => w.TableName == "Table1" && w.ColumnName == "col_varchar"
                        && (string)w.ColumnValue == "Test").First();
                    Assert.Equal(2, table1.MatchCount);

                    var table1LikeColumn = result.Where(w => w.TableName == "Table1" && w.ColumnName == "col_varchar_like"
                        && (string)w.ColumnValue == "Test2").First();
                    Assert.Equal(2, table1LikeColumn.MatchCount);
                }
            }
        }

        [Fact]
        public void DBSearchSearchEmptyTest()
        {
            using (var cnn = new SqlConnection(DBSearchTestSetting._connectionString))
            {
                cnn.Open();
                var result = cnn.Search("");
            }
        }

        [Fact]
        public void DBSearchSearch_EachType_Test()
        {
            using (var cnn = new SqlConnection(DBSearchTestSetting._connectionString))
            {
                cnn.Open();
                {
                    var input = DateTime.Parse("2019/01/02 03:04:05");
                    var result = cnn.Search(input).First().ColumnValue as DateTime?;
                    Assert.Equal(input, result);
                }
                {
                    var input = 1.2;
                    var result = cnn.Search(input).First().ColumnValue as double?;
                    Assert.Equal(input, result);
                }
                {
                    var input = Decimal.Parse("1.2");
                    var result = Convert.ToDecimal(cnn.Search(input).First().ColumnValue);
                    Assert.Equal(input, result);
                }
            }
        }


        private SQLServerSearch dbSearch = DBSearchFactory.CreateInstance<SQLServerSearch>(null, null, null);

        public static IEnumerable<object[]> GetDeclareSearchTextSqlData =>
            new List<object[]>
            {
                new object[] { "Test", "varchar", "'Test'" },
                new object[] { "ด๚ธี", "nvarchar", "N'ด๚ธี'" },
                new object[] { 123, "int", "123" },
                new object[] { 1.2, "float", "1.2" },
                new object[] { Decimal.Parse("1.2"), "decimal", "1.2" },
                new object[] { true, "bit", "1" },
                new object[] { false, "bit", "0" },
                new object[] { DateTime.Parse("2019/01/02 03:04:05"), "datetime", "'2019-01-02T03:04:05'" },
            };
        [Theory, MemberData(nameof(GetDeclareSearchTextSqlData))]
        public void ConvertSearchTextToDBValue_Test(object searchText, string columnDataType, string excepted)
        {
            var result = dbSearch.ConvertSearchTextToDBValue(columnDataType, searchText);
            Assert.Equal(excepted, result);
        }
    }
}
