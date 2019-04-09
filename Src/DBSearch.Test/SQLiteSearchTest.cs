using Dapper;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using Xunit;

namespace DBSearch.Test
{
    public class SQLiteSearchTest
    {
        DbConnection GetSQLiteConnection()
        {
            var _filePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().GetHashCode() + "MyDatabaseForDBSearchProject.sqlite");
            var _connectionString = $"Data Source={_filePath};Version=3;";
            SQLiteConnection.CreateFile(_filePath);
            var connection = new SQLiteConnection(_connectionString)
            {
                ConnectionString = _connectionString
            };
            connection.Open();
            #region SQL
            var sql = @"
CREATE TABLE [Table1](
    [col_varchar] [varchar](150) NULL,
    [col_varchar_like] [varchar](150) NULL,
    [col_nvarchar] [nvarchar](300) NULL,
    [col_int] [int] NULL,
    [col_datetime] [datetime] NULL,
    [col_bit] [bit] null,
    [col_null] [varchar],
    [col_float] [float]
);

CREATE TABLE [Table2](
    [col_varchar] [varchar](150) NULL,
    [col_varchar_like] [varchar](150) NULL,
    [col_nvarchar] [nvarchar](300) NULL,
    [col_int] [int] NULL,
    [col_datetime] [datetime] NULL,
    [col_bit] [bit] null,
    [col_null] [varchar],
    [col_float] [float]
);

insert into [Table1] 
select 'Test','Test','ด๚ธี',123,'2019-01-02 03:04:05',1,null,1.2 
union all
select 'Test','Test','ด๚ธี',123,'2019-01-02 03:04:05',1,null,1.2 
;

insert into [Table2] 
select 'Test','Test','ด๚ธี',123,'2019-01-02 03:04:05',1,null,1.2
;	

select * from [Table2] 
	";
            #endregion
            connection.Execute(sql);
            return connection;
        }

        [Fact]
        public void DBSearchSearchActionTest()
        {
            using (var cnn = GetSQLiteConnection())
            {
                var data = cnn.Search("Test");
                Assert.True(data.Count() == 4);
            }
        }
    }
}
