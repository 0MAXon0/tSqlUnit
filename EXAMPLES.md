# Примеры использования TSqlUnit

Ниже собраны актуальные сценарии под текущий API библиотеки.

## Базовый шаблон теста

```csharp
using Microsoft.Data.SqlClient;
using TSqlUnit;
using Xunit;

public class MySqlTests
{
    private const string ConnectionString =
        "Server=localhost;Database=TestDb;Integrated Security=true;TrustServerCertificate=True;";

    [Fact]
    public void Example()
    {
        using var context = new SqlTestContext(ConnectionString)
            .ForProcedure("dbo.MyProcedure")
            .Build();

        context.Execute(new SqlParameter("@id", 1));
    }
}
```

## 1) Мокирование функции и проверка RETURN/OUT

```csharp
using System.Data;
using Microsoft.Data.SqlClient;
using TSqlUnit;
using Xunit;

public class OrderTests
{
    private const string ConnectionString =
        "Server=localhost;Database=TestDb;Integrated Security=true;TrustServerCertificate=True;";

    [Fact]
    public void CalculateOrder_UsesMockedTaxRate()
    {
        using var context = new SqlTestContext(ConnectionString)
            .ForProcedure("dbo.CalculateOrder")
            .MockFunction("dbo.GetTaxRate", @"
                CREATE FUNCTION dbo.GetTaxRate(@state VARCHAR(2))
                RETURNS DECIMAL(5,2)
                AS
                BEGIN
                    RETURN 0.10;
                END")
            .Build();

        var outTotal = new SqlParameter("@total", SqlDbType.Decimal)
        {
            Direction = ParameterDirection.Output,
            Precision = 18,
            Scale = 2
        };

        using var result = context.ExecuteWithResult(
            new SqlParameter("@orderId", 123),
            outTotal);

        Assert.Equal(1, result.ReturnValue ?? 0);
        Assert.Equal(110.00m, result.GetOutParameter<decimal>("@total"));
    }
}
```

## 2) Общий setup через `SqlTestSuite` + override fake

```csharp
using TSqlUnit;
using Xunit;

public class SuiteTests
{
    private const string ConnectionString =
        "Server=localhost;Database=TestDb;Integrated Security=true;TrustServerCertificate=True;";

    [Fact]
    public void LastFakeWins_ForTheSameObject()
    {
        var suite = new SqlTestSuite(ConnectionString)
            .Setup(ctx => ctx.MockFunction("dbo.GetDiscount", @"
                CREATE FUNCTION dbo.GetDiscount(@customerId INT)
                RETURNS DECIMAL(5,2)
                AS
                BEGIN
                    RETURN 0.05;
                END"));

        using var context = suite
            .ForProcedure("dbo.CreateInvoice")
            .MockFunction("dbo.GetDiscount", @"
                CREATE FUNCTION dbo.GetDiscount(@customerId INT)
                RETURNS DECIMAL(5,2)
                AS
                BEGIN
                    RETURN 0.20;
                END")
            .Build();

        context.Execute();
        // Проверка результата в БД...
    }
}
```

## 3) Fake процедуры + чтение spy-лога

```csharp
using System;
using System.Data;
using TSqlUnit;
using Xunit;

public class SpyProcedureTests
{
    private const string ConnectionString =
        "Server=localhost;Database=TestDb;Integrated Security=true;TrustServerCertificate=True;";

    [Fact]
    public void MockProcedure_WritesInputParametersToSpyLog()
    {
        using var context = new SqlTestContext(ConnectionString)
            .ForProcedure("dbo.ProcessData")
            .MockProcedure("dbo.GenerateRandomData", "SELECT N'тест' AS [text];")
            .Build();

        context.Execute();

        var spyLog = context.GetSpyProcedureLog("dbo.GenerateRandomData");

        Assert.NotNull(spyLog);
        Assert.True(spyLog.Rows.Count > 0);
        Assert.True(spyLog.Columns.Contains("_id_"));
    }
}
```

## 4) Fake таблица + подготовка данных через `SetupSql`

```csharp
using System.Data;
using TSqlUnit;
using Xunit;

public class FakeTableTests
{
    private const string ConnectionString =
        "Server=localhost;Database=TestDb;Integrated Security=true;TrustServerCertificate=True;";

    [Fact]
    public void MockTable_AllowsIsolatedDataSetup()
    {
        using var context = new SqlTestContext(ConnectionString)
            .ForProcedure("dbo.GetProducts")
            .MockTable("dbo.Products", TableDefinitionOptions.Default)
            .Build();

        var fakeProductsName = context.GetFakeName(ObjectType.Table, "dbo.Products");
        context.SetupSql($@"
            INSERT INTO [dbo].[{fakeProductsName}] ([CategoryID], [Name], [Price])
            VALUES (1, N'Milk', 120.50),
                   (2, N'Bread', 45.00);");

        using var result = context.ExecuteWithResult();
        var products = result.GetFirstResultSet();

        Assert.NotNull(products);
        Assert.Equal(2, products.Rows.Count);
    }
}
```

## 5) Сравнение таблиц и человекочитаемый diff

```csharp
using System.Data;
using TSqlUnit;
using Xunit;

public class CompareTests
{
    [Fact]
    public void Compare_ReturnsDiffTable()
    {
        var expected = new DataTable();
        expected.Columns.Add("Id", typeof(int));
        expected.Columns.Add("Name", typeof(string));
        expected.Rows.Add(1, "A");
        expected.Rows.Add(2, "B");

        var actual = new DataTable();
        actual.Columns.Add("Id", typeof(int));
        actual.Columns.Add("Name", typeof(string));
        actual.Rows.Add(1, "A");
        actual.Rows.Add(3, "C");

        var comparison = DataTableComparer.Compare(
            expected,
            actual,
            new DataTableComparisonOptions
            {
                IgnoreRowOrder = true,
                SortByColumns = new[] { "Id" },
                IncludeMatchedRowsInDiff = true
            });

        Assert.False(comparison.IsEqual);
        Assert.NotNull(comparison.DiffTable);
        Assert.Contains("_m_", comparison.DiffMessage);
    }
}
```

## 6) Работа с метаданными и заменой имен

```csharp
using TSqlUnit;

var connectionString = "Server=localhost;Database=TestDb;Integrated Security=true;TrustServerCertificate=True;";

var canonical = SqlMetadataReader.GetCanonicalName(connectionString, "dbo.Orders");
var createTableSql = SqlMetadataReader.GetTableDefinition(connectionString, canonical, TableDefinitionOptions.Maximum);

var testCreateSql = SqlScriptModifier.ReplaceObjectName(
    createTableSql,
    "dbo.Orders",
    "dbo.Orders_Test");
```

## Практические рекомендации

- Старайся держать один тест = один `SqlTestContext`.
- Для повторяющихся fake-настроек используй `SqlTestSuite`.
- Для проверки результирующих наборов удобно сочетать:
  - `DataTableComparer.SelectColumns(...)`
  - `DataTableComparer.Compare(...)`
- Всегда используй `using`/`Dispose`, чтобы cleanup гарантированно выполнился.
