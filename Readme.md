> 【注意】目前版本處於Beta測試階段,請勿使用在商業環境。

---

### Features
- 輕量、簡單易用 (只需懂`Search`方法)
- 支持`net45;net451;net46;netstandard2.0;`
- 不依賴任何第三方套件
- 簡單多Connection加快查詢
- 自動類型判斷
- 支持 SQL Server、Oracle、SQLite (其他MySQL、PGSQL、Firebird資料庫現在是Beta版本支持,假如使用上有問題麻煩告知提ISSUE)


### 安裝

可以安裝套件從 [NuGet連結](https://www.nuget.org/packages/HtmlTableHelper) 使用 Visual Studio Package Manager 或是 NuGet UI:

```cmd
PM> install-package DBSearch
```

或是 `dotnet` command line:

```cmd
dotnet add package DBSearch
```


### Get Start

#### 明確查詢

```C#
using (var connection = GetConnection())
{
    var result = connection.Search("your search data");
}
```

#### 模糊查詢

舉例搜尋包含Test字串的欄位值
```C#
using (var cnn = GetConnection())
{
    var data = cnn.Search("%Test%");
}
```

在SqlServer當中也可以使用正則模糊查詢,舉例搜尋A或B字母開頭的欄位值
```C#
using (var cnn = GetConnection())
{
    var data = cnn.Search("[A|B]%");
}
```

#### 多連線加快查詢

舉例,建立十個連線來分工加快查詢工作
```C#
var data = cnn.Search("Test",connnectionCount=10);
```

【注意】假如連接字串使用帳號密碼方式,需要輸入connectionString參數,為何要如此麻煩可以看這篇[ConnectionString loses password](https://stackoverflow.com/questions/12467335/connectionstring-loses-password-after-connection-open).
```C#
var data = cnn.Search("Test",connnectionCount=10,connectionString=@"Data Source=192.168.1.1;User ID=sa;Password=123456;Initial Catalog=master;");
```


#### 進階應用

修改全資料庫指定值,舉例將全資料庫"Hello Gitlab"值換成"Hello Github"
```C#
Replace("Hello Gitlab","Hello Github");

static void Replace(object replaceValue,object newValue)
{
    using (var scope = new System.Transactions.TransactionScope())
    using (var connection = GetConnection())
    {
        connection.Search(replaceValue, (result) =>
        {
            var sql = $"Update {result.TableName} set {result.ColumnName} = @newValue where {result.ColumnName} = @replaceValue";
            connection.Execute(sql, new { replaceValue, newValue }); //Using Dapper ORM
        });
        scope.Complete();
    }
}
```

#### 類型自動判斷(支持基本類型Int16 ,Int32 ,Double ,Single ,Decimal ,Boolean ,Byte ,Int64 ,Byte[] ,String ,DateTime ,Guid)

以下為舉例示範跟測試資料
![20190407021142-image.png](https://raw.githubusercontent.com/shps951023/ImageHosting/master/img/20190407021142-image.png)  
[線上SQL測試連結](https://dbfiddle.uk/?rdbms=sqlserver_2017&fiddle=ab6b46621f057907349ecd3df14d3f5c)

字串
```C#
connection.Search("Test");
```
![20190408233632-image.png](https://raw.githubusercontent.com/shps951023/ImageHosting/master/img/20190408233632-image.png)

日期
```C#
connection.Search(DateTime.Parse("2019/1/2 03:04:05"));
```
![20190408233740-image.png](https://raw.githubusercontent.com/shps951023/ImageHosting/master/img/20190408233740-image.png)

小數
```C#
connection.Search(1.2);
```
![20190408233912-image.png](https://raw.githubusercontent.com/shps951023/ImageHosting/master/img/20190408233912-image.png)

整數、Boolean、Decimal、Guid
```C#
connection.Search(123);
connection.Search(true);
connection.Search(decimal.Parse("0.123"));
connection.Search(new Guid("219f7ef5-f4bd-4e9a-a9f6-1f127122d004"));
```


<!--
> 問題: 為何不使用Stored Procedure來撰寫就好?  

回答: 
主要幾個原因
1. C#撰寫可以使用`多連線非同步`執行提升速度,傳統方式查詢從頭到尾都只使用一個連線來處理
    這樣導致所有動作都要等待前一個動作完成,導致整體查詢時間延長。
    所以在DBSearch提供自訂義連線數,可以建立N個連線幫忙快速處理查詢。
2. 可以使用強型別Func來自定義處理資料邏輯,像是前面替換全資料庫特定值例子
-->

---

### 補充

因為個人工作接觸Oracle跟SQLServer居多,對其他資料庫有遺漏或錯誤邏輯的地方,期待讀者能告知。







