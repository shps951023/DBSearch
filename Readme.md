> The current version is in Beta, do not use in commercial environment.

[![NuGet](https://img.shields.io/nuget/v/DBSearch.svg)](https://www.nuget.org/packages/DBSearch)
![](https://img.shields.io/nuget/dt/DBSearch.svg)

---

### Online Demo
- [DBSearch Sql Server Easy Search Demo  | .NET Fiddle](https://dotnetfiddle.net/9o1mNe)

### Features
- Mini、Easy(Just need to understand `Search` method)
- support `net45;net451;net46;netstandard2.0;`
- support mutilple Connection faster query 
- auto get match database type
- Support SQL Server、Oracle、SQLite、MySQL、PGSQL、Firebird


### Installation

You can install the package [from NuGet](https://www.nuget.org/packages/DBSearch) using the Visual Studio Package Manager or NuGet UI:

```cmd
PM> install-package DBSearch
```

or `dotnet` command line:

```cmd
dotnet add package DBSearch
```


### Get Start

#### Easy Search

```C#
using (var connection = GetConnection())
{
    var result = connection.Search("your search data");
}
```

#### Like Search

```C#
using (var connection = GetConnection())
{
    var data = connection.Search("%Test%");
}
```


#### Mutiple Connection Search

e.g : Create 10 connection to speed up the query.
```C#
var data = connection.Search("Test",connectionCount : 10);
```

p.s : if connection string use password then it have to add connectionString parameter.
[ConnectionString loses password when connection open](https://stackoverflow.com/questions/12467335/connectionstring-loses-password-after-connection-open)

```C#
var data = connection.Search("Test",connectionCount : 10,connectionString : @"Data Source=192.168.1.1;User ID=sa;Password=123456;Initial Catalog=master;");
```


#### Advanced

Eidt all database someone value  
For example, change the "Hello Gitlab" value of the entire database to "Hello Github", and please make sure to backup and Log it.  
```C#
Replace("Hello Gitlab","Hello Github");

static void Replace(object replaceValue,object newValue)
{
    using (var scope = new System.Transactions.TransactionScope())
    using (var connection = GetConnection())
    {
        //Log Action
        connection.Search(replaceValue, (result) =>
        {
            var sql = $"Update {result.TableName} set {result.ColumnName} = @newValue where {result.ColumnName} = @replaceValue";
            connection.Execute(sql, new { replaceValue, newValue }); //Using Dapper ORM
        });
        scope.Complete();
    }
}
```

#### Auto Check Type

![20190407021142-image.png](https://raw.githubusercontent.com/shps951023/ImageHosting/master/img/20190407021142-image.png)  
[Online Sql Link](https://dbfiddle.uk/?rdbms=sqlserver_2017&fiddle=ab6b46621f057907349ecd3df14d3f5c)

string
```C#
connection.Search("Test");
```
![20190408233632-image.png](https://raw.githubusercontent.com/shps951023/ImageHosting/master/img/20190408233632-image.png)

Datetime
```C#
connection.Search(DateTime.Parse("2019/1/2 03:04:05"));
```
![20190408233740-image.png](https://raw.githubusercontent.com/shps951023/ImageHosting/master/img/20190408233740-image.png)

float
```C#
connection.Search(1.2);
```
![20190408233912-image.png](https://raw.githubusercontent.com/shps951023/ImageHosting/master/img/20190408233912-image.png)

int、Boolean、Decimal、Guid
```C#
connection.Search(123);
connection.Search(true);
connection.Search(decimal.Parse("0.123"));
connection.Search(new Guid("219f7ef5-f4bd-4e9a-a9f6-1f127122d004"));
```






