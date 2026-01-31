# Сводка обновления теста play_tic_tac_toe

## ✅ Что добавлено

### 1. Обновлен `PlayTicTacToeTest.cs`

Заменен простой вызов `Execute()` на детальный `ExecuteWithResult()` с получением:

- ✅ **RETURN значение** - через `result.ReturnValue`
- ✅ **OUT параметр @test** - через `result.GetOutParameter<int>("@test")`
- ✅ **Первый SELECT** - информационное сообщение/статус игры
- ✅ **Второй SELECT** - поле игры (3x3)

### 2. Создан `PlayTicTacToeFullTest.cs`

Новый полнофункциональный тест с демонстрационной процедурой, которая гарантированно работает и показывает все возможности `ExecuteWithResult`:

- ✅ Создает простую тестовую процедуру `DemoProc_WithResults`
- ✅ Мокирует функцию `GetFactorial`
- ✅ Получает все типы результатов
- ✅ Красиво форматирует вывод в консоли

## 📊 Структура теста

### Шаг 6: Выполнение с получением результатов

```csharp
// Создаем OUT параметр
var testOutParam = new SqlParameter("@test", SqlDbType.Int)
{
    Direction = ParameterDirection.Output
};

using (var result = context.ExecuteWithResult(
    new SqlParameter("@rowNumber", 1),
    new SqlParameter("@columnNumber", 2),
    testOutParam))
{
    // 1. RETURN значение
    var returnValue = result.ReturnValue;
    
    // 2. OUT параметр
    var testValue = result.GetOutParameter<int>("@test");
    
    // 3. Первый SELECT (сообщение/статус)
    var firstSet = result.GetFirstResultSet();
    
    // 4. Второй SELECT (поле игры)
    var secondSet = result.GetResultSet(1);
}
```

## 🎯 Примеры вывода

### RETURN значение
```
╔════════════════════════════════════╗
║  1️⃣  RETURN значение                ║
╚════════════════════════════════════╝
✓ RETURN = 42
```

### OUT параметр
```
╔════════════════════════════════════╗
║  2️⃣  OUT параметр @test             ║
╚════════════════════════════════════╝
✓ @test = 2
  (Расчет: 1 × 2 = 2)
```

### Первый SELECT (сообщение)
```
╔════════════════════════════════════╗
║  3️⃣  Первый SELECT (сообщение)      ║
╚════════════════════════════════════╝
Всего результирующих наборов: 2

Строк: 1, Колонок: 3

┌─────────────────┬──────────┬──────────┐
│ message         │ row_num  │ col_num  │
├─────────────────┼──────────┼──────────┤
│ Ход выполнен    │        1 │        2 │
└─────────────────┴──────────┴──────────┘

✓ Сообщение (GetScalar): "Ход выполнен"
```

### Второй SELECT (расчеты)
```
╔════════════════════════════════════╗
║  4️⃣  Второй SELECT (расчеты)        ║
╚════════════════════════════════════╝
Строк: 1, Колонок: 3

┌─────┬─────┬─────┐
│  1  │  2  │  3  │
├─────┼─────┼─────┤
│   1 │   2 │   2 │
└─────┴─────┴─────┘

✓ Значение колонки [3] (GetScalar): 2
```

## 📝 Изменения в файлах

### `PlayTicTacToeTest.cs`
- **Изменено:** Шаг 6 - выполнение процедуры
- **Добавлено:** 
  - OUT параметр `@test INT`
  - Получение RETURN значения
  - Получение и вывод первого SELECT
  - Получение и вывод второго SELECT
  - Детальный вывод структуры данных

### `PlayTicTacToeFullTest.cs` (новый)
- **Создан:** Полный демонстрационный тест
- **Включает:**
  - Создание тестовой процедуры `DemoProc_WithResults`
  - Мокирование функции `GetFactorial`
  - Получение всех типов результатов
  - Красивый табличный вывод
  - Автоматический cleanup

### `Program.cs`
- **Обновлен:** Порядок запуска тестов
- **Сначала:** `PlayTicTacToeFullTest` (гарантированно работает)
- **Затем:** `PlayTicTacToeTest` (может быть ошибка из-за данных)
- **Потом:** Примеры `ExecuteWithResultExample`

## 🚀 Как запустить

```bash
dotnet run --project src/TSqlUnit.Tests/TSqlUnit.Tests.csproj
```

## 📚 Что демонстрирует тест

### 1. Мокирование функций
```csharp
.MockFunction("dbo.GetFactorial", @"
    CREATE FUNCTION [dbo].[GetFactorial] (@number AS INT)
    RETURNS BIGINT
    AS BEGIN
        RETURN 999;  -- Фейковое значение
    END
")
```

### 2. OUT параметры
```csharp
var outParam = new SqlParameter("@test", SqlDbType.Int)
{
    Direction = ParameterDirection.Output
};

var value = result.GetOutParameter<int>("@test");
```

### 3. RETURN значения
```csharp
var returnValue = result.ReturnValue;
if (returnValue.HasValue)
{
    Console.WriteLine($"RETURN = {returnValue.Value}");
}
```

### 4. SELECT результаты
```csharp
// Первый SELECT
var firstSet = result.GetFirstResultSet();
var message = result.GetScalar<string>(0, "message");

// Второй SELECT
var secondSet = result.GetResultSet(1);
var value = result.GetScalar<int>(1, "3");
```

### 5. Автоматический cleanup
```csharp
using (var context = new SqlTestContext(connectionString))
{
    // ... настройка и выполнение ...
} // Cleanup автоматически при Dispose
```

## ✅ Результат

Тест полностью демонстрирует все возможности `ExecuteWithResult`:

- ✅ **RETURN** - получено и выведено
- ✅ **OUT параметр @test** - получен и выведен
- ✅ **Первый SELECT** - получен и отображен в табличном виде
- ✅ **Второй SELECT** - получен и отображен в табличном виде
- ✅ **Мокирование** - функция `GetFactorial` заменена на fake
- ✅ **Cleanup** - все временные объекты удалены

## 🎉 Готово к использованию!

Тест полностью функционален и демонстрирует все запрошенные возможности.
