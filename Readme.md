### Features
- 簡單易用,只需要懂得Search方法就夠
- 支持`net35;net40;net45;net451;net46;netstandard2.0;`
- 不依賴任何第三方套件
- 支持 SQL Server、Oracle、SQLite、MySQL、PGSQL、Firebird..其他資料庫理論上支持,假如不支持麻煩告知提ISSUE


### Get Start

#### 1.明確查詢

```C#
using (var connection = GetConnection())
{
    var result = connection.Search("your search data");
}
```

#### 2.模糊查詢,舉例搜尋包含Test字串的欄位值
```C#
using (var cnn = GetConnection())
{
    var data = cnn.Search("%Test%",likeSearch:true);
}
```

在sqlserver當中也可以使用正則模糊查詢,舉例搜尋A或B字母開頭的欄位值
```C#
using (var cnn = GetConnection())
{
    var data = cnn.Search("[A|B]%",likeSearch:true);
}
```

在sqlserver當中也可以使用正則模糊查詢,舉例搜尋A或B字母開頭的欄位值
```C#
using (var cnn = GetConnection())
{
    var data = cnn.Search("[A|B]%",likeSearch:true);
}
```

#### 3.類型自動判斷(支持基本類型Int16 ,Int32 ,Double ,Single ,Decimal ,Boolean ,Byte ,Int64 ,Byte[] ,String ,DateTime ,Guid)

以下為舉例示範跟測試資料
![20190407021142-image.png](https://raw.githubusercontent.com/shps951023/ImageHosting/master/img/20190407021142-image.png)[線上SQL連結](https://dbfiddle.uk/?rdbms=sqlserver_2017&fiddle=824827c951dee214bf3c3debb3a6c591)

字串
```C#
var result = connection.Search("Test");
```
![20190407020744-image.png](https://raw.githubusercontent.com/shps951023/ImageHosting/master/img/20190407020744-image.png)

日期
```C#
var result = connection.Search(DateTime.Parse("2019/1/2 03:04:05"));
```
![20190407021235-image.png](https://raw.githubusercontent.com/shps951023/ImageHosting/master/img/20190407021235-image.png)

浮點數
```C#
var result = connection.Search(1.2);
```
![20190407021359-image.png](https://raw.githubusercontent.com/shps951023/ImageHosting/master/img/20190407021359-image.png)

整數
```C#
var result = connection.Search(123);
```
![20190407021837-image.png](https://raw.githubusercontent.com/shps951023/ImageHosting/master/img/20190407021837-image.png)

