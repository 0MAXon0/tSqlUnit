# Примеры использования TSqlUnit

Этот документ содержит практические примеры использования библиотеки TSqlUnit для различных сценариев тестирования T-SQL кода.

## Сценарий 1: Тестирование процедуры расчета заказа

### Исходная процедура

```sql
CREATE PROCEDURE dbo.CalculateOrderTotal
    @OrderId INT
AS
BEGIN
    DECLARE @SubTotal DECIMAL(18,2)
    DECLARE @TaxRate DECIMAL(5,2)
    DECLARE @Total DECIMAL(18,2)
    
    -- Получаем сумму заказа
    SELECT @SubTotal = SUM(Quantity * UnitPrice)
    FROM OrderDetails
    WHERE OrderId = @OrderId
    
    -- Получаем ставку налога (вызов функции)
    SELECT @TaxRate = dbo.GetTaxRate('CA')
    
    -- Считаем итоговую сумму
    SET @Total = @SubTotal * (1 + @TaxRate)
    
    -- Обновляем заказ
    UPDATE Orders
    SET TotalAmount = @Total
    WHERE OrderId = @OrderId
END
```

### Unit-тест с мокированием функции GetTaxRate

```csharp
using TSqlUnit;
using Microsoft.Data.SqlClient;
using Xunit;

public class CalculateOrderTotalTests
{
    private readonly string _connectionString = "Server=localhost;Database=TestDB;Integrated Security=true;";
    
    [Fact]
    public void CalculateOrderTotal_WithFixedTaxRate_CalculatesCorrectly()
    {
        // Arrange: подготавливаем тестовые данные
        var orderId = CreateTestOrder(subTotal: 100.00m);
        
        // Act: выполняем процедуру с мокированной функцией GetTaxRate
        using (var context = new SqlTestContext(_connectionString))
        {
            context
                .ForProcedure("dbo.CalculateOrderTotal")
                .MockFunction("dbo.GetTaxRate", @"
                    CREATE FUNCTION dbo.GetTaxRate(@state VARCHAR(2))
                    RETURNS DECIMAL(5,2)
                    AS BEGIN
                        RETURN 0.10  -- Фиксируем ставку налога 10% для теста
                    END
                ")
                .Build()
                .Execute(new SqlParameter("@OrderId", orderId));
        }
        
        // Assert: проверяем результат
        var actualTotal = GetOrderTotal(orderId);
        Assert.Equal(110.00m, actualTotal); // 100 + 10% = 110
    }
    
    private int CreateTestOrder(decimal subTotal)
    {
        // Код создания тестового заказа
        // ...
        return orderId;
    }
    
    private decimal GetOrderTotal(int orderId)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            connection.Open();
            using (var cmd = new SqlCommand("SELECT TotalAmount FROM Orders WHERE OrderId = @id", connection))
            {
                cmd.Parameters.AddWithValue("@id", orderId);
                return (decimal)cmd.ExecuteScalar();
            }
        }
    }
}
```

## Сценарий 2: Тестирование процедуры с несколькими зависимостями

### Исходная процедура

```sql
CREATE PROCEDURE dbo.ProcessPayment
    @CustomerId INT,
    @Amount DECIMAL(18,2)
AS
BEGIN
    DECLARE @Discount DECIMAL(5,2)
    DECLARE @ExchangeRate DECIMAL(10,4)
    DECLARE @FinalAmount DECIMAL(18,2)
    
    -- Получаем скидку клиента
    SELECT @Discount = dbo.GetCustomerDiscount(@CustomerId)
    
    -- Получаем текущий курс валюты
    SELECT @ExchangeRate = dbo.GetExchangeRate('USD', 'EUR')
    
    -- Применяем скидку и конвертируем
    SET @FinalAmount = @Amount * (1 - @Discount) * @ExchangeRate
    
    -- Сохраняем платеж
    INSERT INTO Payments (CustomerId, Amount, ProcessedDate)
    VALUES (@CustomerId, @FinalAmount, GETDATE())
END
```

### Unit-тест с мокированием обеих функций

```csharp
[Fact]
public void ProcessPayment_WithMockedDependencies_CalculatesCorrectly()
{
    using (var context = new SqlTestContext(_connectionString))
    {
        context
            .ForProcedure("dbo.ProcessPayment")
            .MockFunction("dbo.GetCustomerDiscount", @"
                CREATE FUNCTION dbo.GetCustomerDiscount(@customerId INT)
                RETURNS DECIMAL(5,2)
                AS BEGIN
                    RETURN 0.20  -- 20% скидка для теста
                END
            ")
            .MockFunction("dbo.GetExchangeRate", @"
                CREATE FUNCTION dbo.GetExchangeRate(@from VARCHAR(3), @to VARCHAR(3))
                RETURNS DECIMAL(10,4)
                AS BEGIN
                    RETURN 0.85  -- Фиксированный курс для теста
                END
            ")
            .Build()
            .Execute(
                new SqlParameter("@CustomerId", 123),
                new SqlParameter("@Amount", 1000.00m)
            );
    }
    
    // Проверяем: 1000 * (1 - 0.20) * 0.85 = 680.00
    var payment = GetLastPayment(123);
    Assert.Equal(680.00m, payment.Amount);
}
```

## Сценарий 3: Создание изолированной тестовой таблицы

```csharp
public class OrdersRepositoryTests
{
    private string _testTableName;
    
    [SetUp]
    public void Setup()
    {
        // Получаем структуру оригинальной таблицы
        var originalTableScript = Core.GetTableDefinition(
            _connectionString,
            "dbo.Orders",
            new TableDefinitionOptions
            {
                IncludeIdentity = true,
                IncludePrimaryKey = true,
                IncludeDefaults = true,
                IncludeForeignKeys = false,  // Убираем FK для изоляции
                IncludeCheckConstraints = true
            }
        );
        
        // Генерируем уникальное имя для тестовой таблицы
        _testTableName = TestObjectNameGenerator.Generate("Orders", ObjectType.Table);
        
        // Заменяем имя в скрипте
        var testTableScript = Core.ReplaceObjectName(
            originalTableScript,
            "dbo.Orders",
            $"dbo.{_testTableName}"
        );
        
        // Создаем тестовую таблицу
        using (var connection = new SqlConnection(_connectionString))
        {
            connection.Open();
            using (var cmd = new SqlCommand(testTableScript, connection))
            {
                cmd.ExecuteNonQuery();
            }
        }
    }
    
    [TearDown]
    public void Cleanup()
    {
        // Удаляем тестовую таблицу
        using (var connection = new SqlConnection(_connectionString))
        {
            connection.Open();
            using (var cmd = new SqlCommand($"DROP TABLE IF EXISTS dbo.[{_testTableName}]", connection))
            {
                cmd.ExecuteNonQuery();
            }
        }
    }
    
    [Test]
    public void Insert_ValidOrder_Succeeds()
    {
        // Тест использует изолированную тестовую таблицу
        // ...
    }
}
```

## Сценарий 4: Получение канонических имен объектов

```csharp
// Пользователь может передать имя в любом формате
var userInput1 = "MyView";           // без схемы
var userInput2 = "dbo.MyView";       // со схемой
var userInput3 = "[dbo].[MyView]";   // со скобками

// Получаем канонический формат [schema].[name]
var canonical1 = Core.GetCanonicalObjectName(_connectionString, userInput1);
var canonical2 = Core.GetCanonicalObjectName(_connectionString, userInput2);
var canonical3 = Core.GetCanonicalObjectName(_connectionString, userInput3);

// Все вернут: [dbo].[MyView]
Console.WriteLine(canonical1); // [dbo].[MyView]
Console.WriteLine(canonical2); // [dbo].[MyView]
Console.WriteLine(canonical3); // [dbo].[MyView]
```

## Сценарий 5: Замена имен объектов в скриптах

```csharp
var originalScript = @"
CREATE PROCEDURE dbo.UpdateOrders
AS
BEGIN
    -- Вызываем функцию без схемы (подразумевается dbo)
    SELECT GetStatus(OrderId) FROM Orders
    
    -- Вызываем функцию с явной схемой
    SELECT dbo.GetStatus(OrderId) FROM Orders
    
    -- Вызываем функцию со скобками
    SELECT [dbo].[GetStatus](OrderId) FROM Orders
END
";

// Заменяем все вхождения dbo.GetStatus на dbo.TestFunc_GetStatus_abc123
var modifiedScript = Core.ReplaceObjectName(
    originalScript,
    "dbo.GetStatus",
    "dbo.TestFunc_GetStatus_abc123"
);

// Результат: все три вызова заменены на [dbo].[TestFunc_GetStatus_abc123]
Console.WriteLine(modifiedScript);
```

## Сценарий 6: Интеграция с популярными тестовыми фреймворками

### xUnit

```csharp
public class OrderProcessingTests : IDisposable
{
    private readonly string _connectionString;
    
    public OrderProcessingTests()
    {
        _connectionString = TestConfiguration.GetConnectionString();
    }
    
    [Fact]
    public void ProcessOrder_WithDiscount_AppliesCorrectly()
    {
        using (var context = new SqlTestContext(_connectionString))
        {
            context
                .ForProcedure("dbo.ProcessOrder")
                .MockFunction("dbo.CalculateDiscount", @"
                    CREATE FUNCTION dbo.CalculateDiscount(@orderId INT)
                    RETURNS DECIMAL(5,2)
                    AS BEGIN
                        RETURN 0.15
                    END
                ")
                .Build()
                .Execute(new SqlParameter("@orderId", 1));
        }
        
        // Assertions...
    }
    
    public void Dispose()
    {
        // Cleanup if needed
    }
}
```

### NUnit

```csharp
[TestFixture]
public class PaymentProcessingTests
{
    private string _connectionString;
    
    [SetUp]
    public void Setup()
    {
        _connectionString = TestConfiguration.GetConnectionString();
    }
    
    [Test]
    public void ProcessPayment_WithMockedTaxCalculation_Succeeds()
    {
        using (var context = new SqlTestContext(_connectionString))
        {
            context
                .ForProcedure("dbo.ProcessPayment")
                .MockFunction("dbo.CalculateTax", @"
                    CREATE FUNCTION dbo.CalculateTax(@amount DECIMAL(18,2))
                    RETURNS DECIMAL(18,2)
                    AS BEGIN
                        RETURN @amount * 0.08
                    END
                ")
                .Build()
                .Execute(new SqlParameter("@paymentId", 42));
        }
        
        // Assertions...
    }
}
```

## Полезные паттерны

### Паттерн: Базовый класс для тестов

```csharp
public abstract class SqlTestBase : IDisposable
{
    protected readonly string ConnectionString;
    private readonly List<string> _objectsToCleanup = new List<string>();
    
    protected SqlTestBase()
    {
        ConnectionString = TestConfiguration.GetConnectionString();
    }
    
    protected void RegisterForCleanup(string objectName, string objectType)
    {
        _objectsToCleanup.Add($"DROP {objectType} IF EXISTS {objectName}");
    }
    
    public void Dispose()
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            connection.Open();
            foreach (var sql in _objectsToCleanup)
            {
                try
                {
                    using (var cmd = new SqlCommand(sql, connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
                catch
                {
                    // Log but don't fail
                }
            }
        }
    }
}

// Использование
public class MyTests : SqlTestBase
{
    [Test]
    public void MyTest()
    {
        // Test implementation
    }
}
```

### Паттерн: Фабрика для тестовых данных

```csharp
public class TestDataFactory
{
    private readonly string _connectionString;
    
    public TestDataFactory(string connectionString)
    {
        _connectionString = connectionString;
    }
    
    public int CreateTestOrder(decimal amount, int customerId)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            connection.Open();
            using (var cmd = new SqlCommand(@"
                INSERT INTO Orders (CustomerId, Amount, CreatedDate)
                OUTPUT INSERTED.OrderId
                VALUES (@customerId, @amount, GETDATE())", 
                connection))
            {
                cmd.Parameters.AddWithValue("@customerId", customerId);
                cmd.Parameters.AddWithValue("@amount", amount);
                return (int)cmd.ExecuteScalar();
            }
        }
    }
    
    public int CreateTestCustomer(string name, string email)
    {
        // Similar implementation
        return customerId;
    }
}
```

## Заключение

Библиотека TSqlUnit позволяет писать настоящие unit-тесты для T-SQL кода с изоляцией зависимостей. Используйте мокирование функций для контроля внешних зависимостей и создавайте изолированные копии таблиц для тестирования без влияния на реальные данные.
