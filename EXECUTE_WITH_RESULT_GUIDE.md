# Руководство по использованию ExecuteWithResult

## Обзор

Метод `ExecuteWithResult()` позволяет получать полную информацию о результатах выполнения тестовой процедуры:

- 📊 **SELECT результаты** - все результирующие наборы данных
- 📤 **OUT параметры** - выходные параметры процедуры
- 🔢 **RETURN значение** - возвращаемое значение процедуры
- 🔄 **Маппинг в модели** - автоматическое преобразование в C# объекты

## Быстрый старт

### Базовое использование

```csharp
using (var context = new SqlTestContext(connectionString))
{
    context
        .ForProcedure("dbo.MyProcedure")
        .Build();

    using (var result = context.ExecuteWithResult(
        new SqlParameter("@customerId", 123)))
    {
        // Получаем скалярное значение
        var total = result.GetScalar<decimal>();
        
        // Получаем RETURN значение
        var returnValue = result.ReturnValue;
        
        Console.WriteLine($"Total: {total}, Return: {returnValue}");
    }
}
```

## API Reference

### SqlTestResult

Класс для работы с результатами выполнения процедуры.

#### Свойства

##### ResultSets
```csharp
public List<DataTable> ResultSets { get; }
```
Все результирующие наборы данных (SELECT-ы).

##### ReturnValue
```csharp
public int? ReturnValue { get; }
```
Возвращаемое значение процедуры (RETURN).

#### Методы для работы с OUT параметрами

##### GetOutParameter<T>()
```csharp
public T GetOutParameter<T>(string parameterName)
```

Получает значение OUT параметра по имени.

**Пример:**
```csharp
var outParam = new SqlParameter("@totalCount", SqlDbType.Int)
{
    Direction = ParameterDirection.Output
};

using (var result = context.ExecuteWithResult(outParam))
{
    var count = result.GetOutParameter<int>("@totalCount");
    Console.WriteLine($"Total count: {count}");
}
```

#### Методы для работы с результирующими наборами

##### GetFirstResultSet()
```csharp
public DataTable GetFirstResultSet()
```

Получает первый результирующий набор данных.

**Пример:**
```csharp
using (var result = context.ExecuteWithResult())
{
    var table = result.GetFirstResultSet();
    foreach (DataRow row in table.Rows)
    {
        Console.WriteLine(row["ProductName"]);
    }
}
```

##### GetResultSet(int index)
```csharp
public DataTable GetResultSet(int index)
```

Получает результирующий набор данных по индексу.

**Пример:**
```csharp
// Процедура возвращает 3 результирующих набора
using (var result = context.ExecuteWithResult())
{
    var orders = result.GetResultSet(0);      // Первый SELECT
    var orderItems = result.GetResultSet(1);  // Второй SELECT
    var summary = result.GetResultSet(2);     // Третий SELECT
}
```

#### Методы для работы со скалярными значениями

##### GetScalar<T>()
```csharp
public T GetScalar<T>()
public T GetScalar<T>(int resultSetIndex)
public T GetScalar<T>(int resultSetIndex, string columnName)
```

Получает скалярное значение из результирующего набора.

**Примеры:**
```csharp
// Первая колонка первой строки первого результата
var total = result.GetScalar<decimal>();

// Первая колонка первой строки второго результата
var count = result.GetScalar<int>(1);

// Конкретная колонка первой строки первого результата
var name = result.GetScalar<string>(0, "CustomerName");
```

#### Методы для маппинга в модели

##### MapToList<T>()
```csharp
public List<T> MapToList<T>(Func<DataRow, T> mapper)
public List<T> MapToList<T>(int resultSetIndex, Func<DataRow, T> mapper)
```

Маппит результирующий набор в список объектов.

**Пример:**
```csharp
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
}

using (var result = context.ExecuteWithResult())
{
    var products = result.MapToList<Product>(row => new Product
    {
        Id = Convert.ToInt32(row["ProductId"]),
        Name = row["ProductName"].ToString(),
        Price = Convert.ToDecimal(row["Price"])
    });
    
    foreach (var product in products)
    {
        Console.WriteLine($"{product.Name}: {product.Price:C}");
    }
}
```

##### MapToObject<T>()
```csharp
public T MapToObject<T>(Func<DataRow, T> mapper)
public T MapToObject<T>(int resultSetIndex, Func<DataRow, T> mapper)
```

Маппит первую строку результирующего набора в объект.

**Пример:**
```csharp
using (var result = context.ExecuteWithResult())
{
    var customer = result.MapToObject<Customer>(row => new Customer
    {
        Id = Convert.ToInt32(row["CustomerId"]),
        Name = row["CustomerName"].ToString(),
        Email = row["Email"].ToString()
    });
    
    Console.WriteLine($"Customer: {customer.Name} ({customer.Email})");
}
```

## Примеры использования

### Пример 1: Процедура с OUT параметром

**SQL:**
```sql
CREATE PROCEDURE dbo.CalculateTotal
    @orderId INT,
    @total DECIMAL(18,2) OUTPUT
AS
BEGIN
    SELECT @total = SUM(Quantity * UnitPrice)
    FROM OrderDetails
    WHERE OrderId = @orderId;
    
    SELECT @orderId AS OrderId, @total AS Total;
    
    RETURN 0;
END
```

**C#:**
```csharp
var totalParam = new SqlParameter("@total", SqlDbType.Decimal)
{
    Direction = ParameterDirection.Output,
    Precision = 18,
    Scale = 2
};

using (var result = context.ExecuteWithResult(
    new SqlParameter("@orderId", 123),
    totalParam))
{
    // Получаем OUT параметр
    var totalFromOut = result.GetOutParameter<decimal>("@total");
    
    // Или получаем из SELECT
    var totalFromSelect = result.GetScalar<decimal>(0, "Total");
    
    // Проверяем RETURN
    var returnCode = result.ReturnValue ?? -1;
    
    Console.WriteLine($"Total: {totalFromOut} (return: {returnCode})");
}
```

### Пример 2: Несколько результирующих наборов

**SQL:**
```sql
CREATE PROCEDURE dbo.GetOrderInfo
    @orderId INT
AS
BEGIN
    -- Первый результат: информация о заказе
    SELECT OrderId, OrderDate, CustomerId, TotalAmount
    FROM Orders
    WHERE OrderId = @orderId;
    
    -- Второй результат: позиции заказа
    SELECT ProductId, ProductName, Quantity, UnitPrice
    FROM OrderDetails
    WHERE OrderId = @orderId;
    
    -- Третий результат: история статусов
    SELECT StatusId, StatusName, ChangedDate
    FROM OrderStatusHistory
    WHERE OrderId = @orderId
    ORDER BY ChangedDate;
    
    RETURN 1;
END
```

**C#:**
```csharp
using (var result = context.ExecuteWithResult(
    new SqlParameter("@orderId", 123)))
{
    // Первый набор: информация о заказе
    var order = result.MapToObject<Order>(0, row => new Order
    {
        OrderId = Convert.ToInt32(row["OrderId"]),
        OrderDate = Convert.ToDateTime(row["OrderDate"]),
        CustomerId = Convert.ToInt32(row["CustomerId"]),
        TotalAmount = Convert.ToDecimal(row["TotalAmount"])
    });
    
    // Второй набор: позиции заказа
    var items = result.MapToList<OrderItem>(1, row => new OrderItem
    {
        ProductId = Convert.ToInt32(row["ProductId"]),
        ProductName = row["ProductName"].ToString(),
        Quantity = Convert.ToInt32(row["Quantity"]),
        UnitPrice = Convert.ToDecimal(row["UnitPrice"])
    });
    
    // Третий набор: история
    var history = result.MapToList<StatusHistory>(2, row => new StatusHistory
    {
        StatusId = Convert.ToInt32(row["StatusId"]),
        StatusName = row["StatusName"].ToString(),
        ChangedDate = Convert.ToDateTime(row["ChangedDate"])
    });
    
    Console.WriteLine($"Order {order.OrderId}: {items.Count} items, {history.Count} status changes");
}
```

### Пример 3: Комплексный тест с мокированием

**SQL (оригинальная процедура):**
```sql
CREATE PROCEDURE dbo.ProcessPayment
    @customerId INT,
    @amount DECIMAL(18,2),
    @transactionId INT OUTPUT
AS
BEGIN
    DECLARE @discount DECIMAL(5,2)
    DECLARE @taxRate DECIMAL(5,2)
    DECLARE @finalAmount DECIMAL(18,2)
    
    -- Получаем скидку клиента (функция, которую будем мокировать)
    SELECT @discount = dbo.GetCustomerDiscount(@customerId)
    
    -- Получаем налог (функция, которую будем мокировать)
    SELECT @taxRate = dbo.GetTaxRate('US')
    
    -- Рассчитываем итоговую сумму
    SET @finalAmount = @amount * (1 - @discount) * (1 + @taxRate)
    
    -- Сохраняем транзакцию
    INSERT INTO Transactions (CustomerId, Amount, ProcessedDate)
    VALUES (@customerId, @finalAmount, GETDATE())
    
    SET @transactionId = SCOPE_IDENTITY()
    
    -- Возвращаем информацию
    SELECT @transactionId AS TransactionId,
           @amount AS OriginalAmount,
           @discount AS Discount,
           @taxRate AS TaxRate,
           @finalAmount AS FinalAmount
    
    RETURN 1 -- Success
END
```

**C# (unit тест):**
```csharp
[Test]
public void ProcessPayment_WithMockedDependencies_CalculatesCorrectly()
{
    using (var context = new SqlTestContext(_connectionString))
    {
        // Мокируем зависимости
        context
            .ForProcedure("dbo.ProcessPayment")
            .MockFunction("dbo.GetCustomerDiscount", @"
                CREATE FUNCTION dbo.GetCustomerDiscount(@customerId INT)
                RETURNS DECIMAL(5,2)
                AS BEGIN
                    RETURN 0.10  -- Фиксированная скидка 10%
                END
            ")
            .MockFunction("dbo.GetTaxRate", @"
                CREATE FUNCTION dbo.GetTaxRate(@country VARCHAR(2))
                RETURNS DECIMAL(5,2)
                AS BEGIN
                    RETURN 0.08  -- Фиксированный налог 8%
                END
            ")
            .Build();

        // Подготавливаем параметры
        var transactionIdParam = new SqlParameter("@transactionId", SqlDbType.Int)
        {
            Direction = ParameterDirection.Output
        };

        // Выполняем процедуру
        using (var result = context.ExecuteWithResult(
            new SqlParameter("@customerId", 123),
            new SqlParameter("@amount", 1000.00m),
            transactionIdParam))
        {
            // Проверяем OUT параметр
            var transactionId = result.GetOutParameter<int>("@transactionId");
            Assert.Greater(transactionId, 0);
            
            // Проверяем RETURN значение
            Assert.AreEqual(1, result.ReturnValue);
            
            // Проверяем расчеты из SELECT
            var originalAmount = result.GetScalar<decimal>(0, "OriginalAmount");
            var discount = result.GetScalar<decimal>(0, "Discount");
            var taxRate = result.GetScalar<decimal>(0, "TaxRate");
            var finalAmount = result.GetScalar<decimal>(0, "FinalAmount");
            
            Assert.AreEqual(1000.00m, originalAmount);
            Assert.AreEqual(0.10m, discount);
            Assert.AreEqual(0.08m, taxRate);
            
            // Проверяем правильность расчета: 1000 * (1 - 0.10) * (1 + 0.08) = 972
            Assert.AreEqual(972.00m, finalAmount);
        }
    }
}
```

## Сравнение методов

### Execute() vs ExecuteWithResult()

| Метод | Возвращает | Использование | Производительность |
|-------|-----------|---------------|-------------------|
| `Execute()` | `SqlTestContext` | Когда результаты не нужны | Быстрее (ExecuteNonQuery) |
| `ExecuteWithResult()` | `SqlTestResult` | Когда нужны результаты | Медленнее (читает все наборы) |

**Рекомендация:** Используйте `Execute()` для простых тестов без проверки результатов, и `ExecuteWithResult()` когда нужно валидировать данные.

## Best Practices

### 1. Всегда используйте using для SqlTestResult

```csharp
// ✓ Правильно
using (var result = context.ExecuteWithResult())
{
    // Работа с результатами
}

// ✗ Неправильно (утечка памяти)
var result = context.ExecuteWithResult();
var data = result.GetScalar<int>();
// result.Dispose() не вызван!
```

### 2. Проверяйте наличие данных

```csharp
// ✓ Правильно
var table = result.GetFirstResultSet();
if (table != null && table.Rows.Count > 0)
{
    var value = table.Rows[0]["ColumnName"];
}

// ✗ Неправильно (может упасть с NullReferenceException)
var value = result.GetFirstResultSet().Rows[0]["ColumnName"];
```

### 3. Указывайте индекс результирующего набора явно

```csharp
// ✓ Правильно (явно указываем, какой результат читаем)
var orders = result.MapToList<Order>(0, mapper);
var items = result.MapToList<OrderItem>(1, mapper);

// ⚠ Можно, но менее понятно
var orders = result.MapToList<Order>(mapper); // Всегда первый набор
```

### 4. Используйте типизированные параметры

```csharp
// ✓ Правильно
var param = new SqlParameter("@total", SqlDbType.Decimal)
{
    Direction = ParameterDirection.Output,
    Precision = 18,
    Scale = 2
};

// ⚠ Менее надежно (SQL Server определит тип сам)
var param = new SqlParameter("@total", 0m)
{
    Direction = ParameterDirection.Output
};
```

## Troubleshooting

### Проблема: GetOutParameter выбрасывает исключение

**Причина:** Параметр не объявлен как OUTPUT.

**Решение:**
```csharp
var param = new SqlParameter("@value", SqlDbType.Int)
{
    Direction = ParameterDirection.Output  // ← Обязательно!
};
```

### Проблема: ResultSets пустой

**Причина:** Процедура не возвращает результирующие наборы (SELECT).

**Решение:** Проверьте, что процедура действительно выполняет SELECT.

### Проблема: ReturnValue всегда null

**Причина:** Процедура не использует RETURN.

**Решение:** Добавьте RETURN в процедуру:
```sql
CREATE PROCEDURE dbo.MyProc
AS
BEGIN
    -- код процедуры
    RETURN 0  -- ← Обязательно!
END
```

## Заключение

`ExecuteWithResult()` предоставляет полный контроль над результатами выполнения тестовых процедур, позволяя писать детальные и надежные unit-тесты для T-SQL кода.
