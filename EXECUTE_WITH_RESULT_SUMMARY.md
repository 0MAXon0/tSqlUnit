# Сводка реализации ExecuteWithResult

## ✅ Что добавлено

### 1. Новый класс `SqlTestResult`

Полнофункциональный класс для работы с результатами выполнения процедур.

**Файл:** `src/TSqlUnit/SqlTestResult.cs`

**Возможности:**

#### 📊 Работа с результирующими наборами (SELECT-ы)
- `ResultSets` - список всех результирующих наборов
- `GetFirstResultSet()` - получение первого набора
- `GetResultSet(index)` - получение набора по индексу

#### 📤 Работа с OUT параметрами
- `GetOutParameter<T>(name)` - получение значения OUT параметра

#### 🔢 Получение RETURN значения
- `ReturnValue` - свойство с возвращаемым значением процедуры

#### 📈 Работа со скалярными значениями
- `GetScalar<T>()` - первая колонка первой строки
- `GetScalar<T>(resultSetIndex)` - первая колонка конкретного набора
- `GetScalar<T>(resultSetIndex, columnName)` - конкретная колонка

#### 🔄 Маппинг в модели
- `MapToList<T>(mapper)` - маппинг в список объектов
- `MapToList<T>(resultSetIndex, mapper)` - маппинг конкретного набора
- `MapToObject<T>(mapper)` - маппинг первой строки в объект
- `MapToObject<T>(resultSetIndex, mapper)` - маппинг из конкретного набора

### 2. Новый метод в `SqlTestContext`

**Метод:** `ExecuteWithResult(params SqlParameter[] parameters)`

**Возвращает:** `SqlTestResult`

**Отличия от `Execute()`:**

| Характеристика | Execute() | ExecuteWithResult() |
|----------------|-----------|---------------------|
| Возвращает | SqlTestContext | SqlTestResult |
| Читает SELECT-ы | Нет | Да |
| Получает OUT параметры | Нет | Да |
| Получает RETURN | Нет | Да |
| Производительность | Выше | Ниже (читает все данные) |
| Использование | Простые тесты | Детальная валидация |

### 3. Документация

Созданы новые документы:
- **EXECUTE_WITH_RESULT_GUIDE.md** - полное руководство (300+ строк)
- **ExecuteWithResultExample.cs** - рабочие примеры (5 сценариев)

Обновлены:
- **src/TSqlUnit/README.md** - добавлен API reference для SqlTestResult

## 📝 Примеры использования

### Базовый пример

```csharp
using (var context = new SqlTestContext(connectionString))
{
    context
        .ForProcedure("dbo.GetOrderInfo")
        .Build();

    using (var result = context.ExecuteWithResult(
        new SqlParameter("@orderId", 123)))
    {
        // SELECT результаты
        var orderNumber = result.GetScalar<string>(0, "OrderNumber");
        
        // RETURN значение
        var status = result.ReturnValue;
        
        Console.WriteLine($"Order: {orderNumber}, Status: {status}");
    }
}
```

### Комплексный пример с OUT параметрами

```csharp
var totalParam = new SqlParameter("@total", SqlDbType.Decimal)
{
    Direction = ParameterDirection.Output,
    Precision = 18,
    Scale = 2
};

using (var result = context.ExecuteWithResult(
    new SqlParameter("@customerId", 42),
    totalParam))
{
    // OUT параметр
    var total = result.GetOutParameter<decimal>("@total");
    
    // RETURN
    var returnCode = result.ReturnValue ?? -1;
    
    // SELECT результат
    var customerName = result.GetScalar<string>(0, "CustomerName");
    
    Assert.AreEqual(1, returnCode);
    Assert.Greater(total, 0);
}
```

### Маппинг в модели

```csharp
public class Order
{
    public int OrderId { get; set; }
    public string OrderNumber { get; set; }
    public decimal Total { get; set; }
}

using (var result = context.ExecuteWithResult())
{
    var orders = result.MapToList<Order>(row => new Order
    {
        OrderId = Convert.ToInt32(row["OrderId"]),
        OrderNumber = row["OrderNumber"].ToString(),
        Total = Convert.ToDecimal(row["Total"])
    });
    
    Assert.AreEqual(5, orders.Count);
}
```

### Несколько результирующих наборов

```csharp
// Процедура возвращает 3 SELECT-а
using (var result = context.ExecuteWithResult())
{
    // Первый SELECT: заказы
    var orders = result.MapToList<Order>(0, orderMapper);
    
    // Второй SELECT: позиции заказов
    var items = result.MapToList<OrderItem>(1, itemMapper);
    
    // Третий SELECT: итоговая статистика
    var totalOrders = result.GetScalar<int>(2, "TotalOrders");
    var totalAmount = result.GetScalar<decimal>(2, "TotalAmount");
    
    Console.WriteLine($"{orders.Count} orders, {items.Count} items");
}
```

## 🔧 Технические детали

### Реализация

1. **SqlTestResult** использует `SqlDataReader` для чтения данных
2. Все результирующие наборы читаются в память (`DataTable`)
3. После чтения данных `SqlDataReader` закрывается
4. `SqlCommand` сохраняется для доступа к параметрам
5. Реализует `IDisposable` для корректной очистки ресурсов

### Совместимость

- ✅ .NET Standard 2.0
- ✅ Обратная совместимость: старый `Execute()` работает как прежде
- ✅ Все существующие тесты продолжают работать

### Производительность

- `ExecuteWithResult()` медленнее `Execute()`, так как читает все данные в память
- Рекомендуется использовать `Execute()` для простых тестов без валидации результатов
- Используйте `ExecuteWithResult()` только когда нужно проверить данные

## 📊 Статистика

- **Новых классов:** 1 (SqlTestResult)
- **Новых методов:** 10+ публичных API
- **Строк кода:** ~300 новых строк
- **Документации:** ~800 строк
- **Примеров:** 5 рабочих сценариев

## ✅ Статус

**РЕАЛИЗОВАНО И ПРОТЕСТИРОВАНО ✅**

Все запрошенные функции реализованы:

1. ✅ Получение OUT параметров
2. ✅ Получение RETURN значения
3. ✅ Чтение результирующих наборов (SELECT-ы)
4. ✅ Маппинг в модели
5. ✅ Получение скалярных значений
6. ✅ Поддержка нескольких результирующих наборов
7. ✅ Полная документация
8. ✅ Рабочие примеры

## 🚀 Следующие шаги

Для использования нового функционала:

1. Пересобрать проект:
   ```bash
   dotnet build src/TSqlUnit.sln
   ```

2. Изучить примеры:
   ```bash
   dotnet run --project src/TSqlUnit.Tests/TSqlUnit.Tests.csproj
   ```

3. Прочитать полное руководство:
   - `EXECUTE_WITH_RESULT_GUIDE.md`

4. Посмотреть код примеров:
   - `src/TSqlUnit.Tests/ExecuteWithResultExample.cs`

## 🎯 Best Practices

1. **Всегда используйте `using`** для `SqlTestResult`
   ```csharp
   using (var result = context.ExecuteWithResult()) { }
   ```

2. **Проверяйте наличие данных**
   ```csharp
   if (table != null && table.Rows.Count > 0) { }
   ```

3. **Явно указывайте индекс результата**
   ```csharp
   var data = result.MapToList<T>(0, mapper); // Явно: первый набор
   ```

4. **Используйте типизированные параметры**
   ```csharp
   var param = new SqlParameter("@value", SqlDbType.Int)
   {
       Direction = ParameterDirection.Output
   };
   ```

## 📚 Документация

- [Полное руководство](EXECUTE_WITH_RESULT_GUIDE.md)
- [API Reference](src/TSqlUnit/README.md)
- [Примеры](src/TSqlUnit.Tests/ExecuteWithResultExample.cs)

---

**Готово к использованию!** 🎉
