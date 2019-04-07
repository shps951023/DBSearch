using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using Oracle.ManagedDataAccess.Client;
using DbSearch;

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
                cnn.Search("Test",result => {
                    Console.WriteLine($"TableName:{result.TableName}/ColumnName:{result.ColumnName}/MatchCount:{result.MatchCount}");
                });
            }
        }
    }
}
