### Features
- 簡單易用 (只需懂`Search`方法)
- 支持`net35;net40;net45;net451;net46;netstandard2.0;`
- 不依賴任何第三方套件
- 支持 SQL Server、Oracle、SQLite、MySQL、PGSQL、Firebird,其他資料庫理論上有機會支持,假如不行麻煩告知提ISSUE
- 簡單多Connection加快查詢

### Get Start

#### 明確查詢

```C#
using (var connection = GetConnection())
{
    var result = connection.Search("your search data");
}
```

#### 模糊查詢,舉例搜尋包含Test字串的欄位值
```C#
using (var cnn = GetConnection())
{
    var data = cnn.Search("%Test%");
}
```

在sqlserver當中也可以使用正則模糊查詢,舉例搜尋A或B字母開頭的欄位值
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


#### 進階應用

修改全資料庫指定值,舉例將全資料庫"Hello GitLab"值換成"Hello Github"
```C#
Replace("Hello GitLab","Hello Github");

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
[線上SQL測試連結](https://dbfiddle.uk/?rdbms=sqlserver_2017&fiddle=824827c951dee214bf3c3debb3a6c591)

字串
```C#
var result = connection.Search("Test");
```
![20190407023122-image.png](https://raw.githubusercontent.com/shps951023/ImageHosting/master/img/20190407023122-image.png)

日期
```C#
var result = connection.Search(DateTime.Parse("2019/1/2 03:04:05"));
```
![20190407023046-image.png](https://raw.githubusercontent.com/shps951023/ImageHosting/master/img/20190407023046-image.png)

浮點數
```C#
var result = connection.Search(1.2);
```
![20190407022958-image.png](https://raw.githubusercontent.com/shps951023/ImageHosting/master/img/20190407022958-image.png)

整數
```C#
var result = connection.Search(123);
```
![20190407022915-image.png](https://raw.githubusercontent.com/shps951023/ImageHosting/master/img/20190407022915-image.png)


### 補充

> 問題: 為何不使用Stored Procedure來撰寫就好?  

回答: 
主要幾個原因
1. C#撰寫可以使用多連線非同步執行提升速度,傳統方式查詢從頭到尾都只使用一個連線來處理
    這樣導致所有動作都要等待前一個動作完成,導致整體查詢時間延長。
    所以在DBSearch提供自訂義連線數,可以建立N個連線幫忙快速處理查詢。
2. 可以使用強型別Func來自定義處理資料邏輯,像是前面替換全資料庫特定值例子

最後,因為個人工作接觸Oracle跟SQLServer居多,對其他資料庫有遺漏或錯誤邏輯的地方,期待讀者能告知。







