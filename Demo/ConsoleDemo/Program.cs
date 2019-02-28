using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DBSearch;
using System.Data.SqlClient;

namespace ConsoleDemo
{
    class Program
    {
        private static readonly string _connectionString = 
            @"Data Source=(localdb)\MSSQLLocalDB;Integrated Security=SSPI;Initial Catalog=DBSearchTestDB;";

        static void Main(string[] args)
        {
            using (var cnn = new SqlConnection(_connectionString))
            {
                cnn.Open();
                var result = cnn.Search("Test", columnData =>
                {
                    var text = string.Format("{0},{1},{2},{3}", columnData.TableName, columnData.ColumnName, columnData.ColumnValue, columnData.MatchCount);
                    Console.WriteLine(text);
                });
            }
        }
    }
}
