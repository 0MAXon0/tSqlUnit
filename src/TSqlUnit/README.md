# TSqlUnit

Библиотека для unit-тестирования T-SQL кода с возможностью мокирования зависимостей.

## Основные возможности

- ✅ Получение определений SQL объектов (представления, процедуры, функции, триггеры)
- ✅ Генерация полного CREATE TABLE скрипта с constraints
- ✅ Мокирование функций при тестировании процедур
- ✅ Фейкование таблиц при тестировании процедур
- ✅ Автоматическая очистка временных объектов
- ✅ Fluent API для удобной настройки тестов

## Установка

```bash
dotnet add package TSqlUnit
```

## Быстрый старт

### 1. Получение определения объекта

```csharp
using TSqlUnit;

var connectionString = "Server=localhost;Database=TestDB;Integrated Security=true;";

// Получить определение процедуры/функции/представления
var definition = Core.GetObjectDefinition(connectionString, "dbo.MyStoredProcedure");
Console.WriteLine(definition);
```

### 2. Получение определения таблицы

```csharp
// Минимальное определение (только структура)
var minimal = Core.GetTableDefinition(
    connectionString, 
    "dbo.Users", 
    TableDefinitionOptions.Default
);

// Полное определение (со всеми constraints)
var full = Core.GetTableDefinition(
    connectionString, 
    "dbo.Users", 
    TableDefinitionOptions.Maximum
);

// Кастомная конфигурация
var custom = Core.GetTableDefinition(
    connectionString, 
    "dbo.Users", 
    new TableDefinitionOptions
    {
        IncludeIdentity = true,
        IncludePrimaryKey = true,
        IncludeForeignKeys = false,
        IncludeCheckConstraints = false
    }
);
```

### 3. Мокирование зависимостей при тестировании процедур

```csharp
using TSqlUnit;
using Microsoft.Data.SqlClient;

// Пример: тестируем процедуру CalculateOrder, которая использует функцию GetTaxRate
using (var context = new SqlTestContext(connectionString))
{
    context
        .ForProcedure("dbo.CalculateOrder")
        .MockFunction("dbo.GetTaxRate", @"
            CREATE FUNCTION dbo.GetTaxRate(@state VARCHAR(2))
            RETURNS DECIMAL(5,2)
            AS BEGIN
                RETURN 0.15  -- Фиксированная ставка налога для теста
            END
        ")
        .Build()
        .Execute(new SqlParameter("@orderId", 123));
        
    // Cleanup() вызовется автоматически при Dispose
}
```

## API Reference

### Core

Основной класс для работы с SQL объектами.

#### GetObjectDefinition
```csharp
public static string GetObjectDefinition(string connectionString, string objectName)
```
Получает определение объекта (VIEW, PROCEDURE, FUNCTION, TRIGGER).

#### GetTableDefinition
```csharp
public static string GetTableDefinition(
    string connectionString, 
    string tableName, 
    TableDefinitionOptions options = null)
```
Генерирует CREATE TABLE скрипт.

#### GetCanonicalObjectName
```csharp
public static string GetCanonicalObjectName(string connectionString, string objectName)
```
Получает каноническое имя объекта в формате `[schema].[name]`.

#### ReplaceObjectName
```csharp
public static string ReplaceObjectName(
    string definition, 
    string oldFullName, 
    string newFullName)
```
Заменяет имя объекта в SQL скрипте. Защищает от частичных совпадений.

### SqlTestContext

Контекст для настройки и выполнения unit-тестов SQL процедур.

#### ForProcedure
```csharp
public SqlTestContext ForProcedure(string procedureName)
```
Указывает процедуру для тестирования.

#### MockFunction
```csharp
public SqlTestContext MockFunction(string functionName, string fakeDefinition)
```
Добавляет мок функции.

#### MockView
```csharp
public SqlTestContext MockView(string viewName, string fakeDefinition)
```
Добавляет мок представления.

#### MockProcedure
```csharp
public SqlTestContext MockProcedure(string procedureName, string customSqlAfterSpyInsert = null)
```
Добавляет fake процедуру и spy-таблицу для логирования вызовов.
Параметр `customSqlAfterSpyInsert` (опциональный) вклеивается в тело fake процедуры
сразу после `INSERT` в spy-таблицу.

#### MockTable
```csharp
public SqlTestContext MockTable(string tableName, TableDefinitionOptions options = null)
```
Добавляет fake таблицу: определение берется автоматически через `GetTableDefinition()`.

Повторное мокирование того же объекта поддерживает override: применяется последняя подмена (`last fake wins`).

#### SetUpSql
```csharp
public SqlTestContext SetUpSql(string setUpSql)
```
Регистрирует SQL, который выполняется перед каждым `Execute/ExecuteWithResult`.

#### SetUpProcedure
```csharp
public SqlTestContext SetUpProcedure(string procedureName)
```
Регистрирует вызов процедуры, который выполняется перед каждым `Execute/ExecuteWithResult`.

#### Build
```csharp
public SqlTestContext Build()
```
Создает все временные объекты в БД.

#### Execute
```csharp
public SqlTestContext Execute(params SqlParameter[] parameters)
```
Выполняет тестовую процедуру с параметрами.

#### ExecuteWithResult
```csharp
public SqlTestResult ExecuteWithResult(params SqlParameter[] parameters)
```
Выполняет тестовую процедуру с параметрами и возвращает результаты (SELECT-ы, OUT параметры, RETURN значение).

#### GetSpyProcedureLog
```csharp
public DataTable GetSpyProcedureLog(string procedureName)
```
Возвращает логи вызовов fake процедуры из spy-таблицы.

#### ExecuteNonQuery
```csharp
public int ExecuteNonQuery(string sql, params SqlParameter[] parameters)
```
Выполняет произвольный SQL (удобно для seed/cleanup в тестах).

#### ExecuteQuery
```csharp
public DataTable ExecuteQuery(string sql, params SqlParameter[] parameters)
```
Выполняет произвольный SQL и возвращает первый результирующий набор.

#### TryGetFake / GetFake / GetFakeName
```csharp
public bool TryGetFake(ObjectType objectType, string objectName, out FakeDependency fake)
public FakeDependency GetFake(ObjectType objectType, string objectName)
public string GetFakeName(ObjectType objectType, string objectName)
```
Доступ к информации о созданных fake-объектах.

### Cleanup
```csharp
public void Cleanup()
```
Удаляет все временные объекты.

## SqlTestResult

Класс для работы с результатами выполнения процедуры.

### ResultSets
```csharp
public List<DataTable> ResultSets { get; }
```
Все результирующие наборы данных (SELECT-ы).

### ReturnValue
```csharp
public int? ReturnValue { get; }
```
Возвращаемое значение процедуры (RETURN).

### GetOutParameter<T>
```csharp
public T GetOutParameter<T>(string parameterName)
```
Получает значение OUT параметра.

### GetScalar<T>
```csharp
public T GetScalar<T>()
public T GetScalar<T>(int resultSetIndex)
public T GetScalar<T>(int resultSetIndex, string columnName)
```
Получает скалярное значение из результирующего набора.

### MapToList<T>
```csharp
public List<T> MapToList<T>(Func<DataRow, T> mapper)
public List<T> MapToList<T>(int resultSetIndex, Func<DataRow, T> mapper)
```
Маппит результирующий набор в список объектов.

### MapToObject<T>
```csharp
public T MapToObject<T>(Func<DataRow, T> mapper)
public T MapToObject<T>(int resultSetIndex, Func<DataRow, T> mapper)
```
Маппит первую строку результирующего набора в объект.

### GetFirstResultSetAsText / GetResultSetAsText
```csharp
public string GetFirstResultSetAsText(int maxRows = 200, int maxCellLength = 120)
public string GetResultSetAsText(int resultSetIndex, int maxRows = 200, int maxCellLength = 120)
```
Возвращает результирующие наборы в виде удобной текстовой таблицы.

## DataTableComparer

Утилиты для сравнения и проекции `DataTable` без привязки к конкретному test framework.

### SelectColumns
```csharp
public static DataTable SelectColumns(DataTable source, params string[] requestedColumns)
```
Выбирает подмножество колонок из результирующей таблицы.

### Compare
```csharp
public static DataTableComparisonResult Compare(
    DataTable expected,
    DataTable actual,
    DataTableComparisonOptions options = null)
```
Сравнивает таблицы и возвращает `IsEqual`, `DiffMessage` и `DiffTable`.
При различиях в `DiffTable` первая колонка `_m_` содержит:
- `<` — строка есть только в expected
- `>` — строка есть только в actual
- `=` — строка совпала в expected и actual

### EnsureEqual
```csharp
public static void EnsureEqual(
    DataTable expected,
    DataTable actual,
    DataTableComparisonOptions options = null)
```
Выбрасывает исключение, если таблицы различаются.

### FormatAsTextTable
```csharp
public static string FormatAsTextTable(
    DataTable table,
    int maxRows = 200,
    int maxCellLength = 120)
```
Форматирует `DataTable` в человекочитаемую текстовую таблицу.

## SqlTestSuite

Класс для набора тестов с общим SetUp (аналог концепции set up в tSQLt).

### SetUp
```csharp
public SqlTestSuite SetUp(Action<SqlTestContext> setUpAction)
```
Регистрирует общее действие, которое применяется к каждому создаваемому контексту.

### ForProcedure
```csharp
public SqlTestContext ForProcedure(string procedureName)
```
Создает новый контекст для теста и применяет все зарегистрированные set up действия.

### TableDefinitionOptions

Опции для генерации CREATE TABLE скрипта.

```csharp
public class TableDefinitionOptions
{
    public bool IncludeComputedColumns { get; set; }      // Вычисляемые столбцы
    public bool IncludeNotNull { get; set; }              // NOT NULL constraints
    public bool IncludeIdentity { get; set; }             // IDENTITY
    public bool IncludeDefaults { get; set; }             // DEFAULT values
    public bool IncludePrimaryKey { get; set; }           // PRIMARY KEY
    public bool IncludeForeignKeys { get; set; }          // FOREIGN KEYs
    public bool IncludeCheckConstraints { get; set; }     // CHECK constraints
    public bool IncludeUniqueConstraints { get; set; }    // UNIQUE constraints
    
    public static TableDefinitionOptions Default { get; }   // Минимальная конфигурация
    public static TableDefinitionOptions Maximum { get; }   // Полная конфигурация
}
```

## Минимальные права доступа

Для работы библиотеки требуются следующие права:

- `VIEW DEFINITION` - для получения определений объектов
- `CREATE PROCEDURE` - для создания тестовых процедур (только для SqlTestContext)
- `CREATE FUNCTION` - для создания fake функций (только для SqlTestContext)
- `CREATE TABLE` - для создания fake таблиц (только для SqlTestContext)
- Права на чтение системных представлений:
  - `sys.objects`
  - `sys.columns`
  - `sys.indexes`
  - `sys.foreign_keys`
  - `sys.check_constraints`

## Совместимость

- .NET Standard 2.0+ (.NET Framework 4.6.1+, .NET Core 2.0+, .NET 5+)
- SQL Server 2016+

## Примеры использования

### Пример 1: Тестирование процедуры с мокированием функции

```csharp
// Исходная процедура использует функцию GetDiscount для расчета скидки
// Для теста мы хотим зафиксировать скидку в 10%

using (var context = new SqlTestContext(connectionString))
{
    context
        .ForProcedure("dbo.CreateInvoice")
        .MockFunction("dbo.GetDiscount", @"
            CREATE FUNCTION dbo.GetDiscount(@customerId INT)
            RETURNS DECIMAL(5,2)
            AS BEGIN
                RETURN 0.10  -- Фиксированная скидка 10% для теста
            END
        ")
        .Build()
        .Execute(
            new SqlParameter("@customerId", 42),
            new SqlParameter("@amount", 1000.00m)
        );
    
    // Проверяем результаты в БД...
}
```

### Пример 2: Работа с результатами процедуры

```csharp
// Процедура возвращает SELECT, OUT параметр и RETURN значение
using (var context = new SqlTestContext(connectionString))
{
    context
        .ForProcedure("dbo.CalculateOrder")
        .MockFunction("dbo.GetTaxRate", @"
            CREATE FUNCTION dbo.GetTaxRate(@state VARCHAR(2))
            RETURNS DECIMAL(5,2)
            AS BEGIN
                RETURN 0.08  -- 8% налог для теста
            END
        ")
        .Build();

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
        var total = result.GetOutParameter<decimal>("@total");
        
        // Получаем RETURN значение
        var returnCode = result.ReturnValue ?? 0;
        
        // Получаем скалярное значение из SELECT
        var orderNumber = result.GetScalar<string>(0, "OrderNumber");
        
        // Маппим результаты в модель
        var orderInfo = result.MapToObject<OrderInfo>(row => new OrderInfo
        {
            OrderId = Convert.ToInt32(row["OrderId"]),
            Total = Convert.ToDecimal(row["Total"]),
            Status = row["Status"].ToString()
        });
        
        // Assertions
        Assert.AreEqual(972.00m, total);
        Assert.AreEqual(1, returnCode);
    }
}
```

### Пример 3: Получение структуры таблицы для создания тестовой копии

```csharp
// Получаем структуру таблицы без foreign keys для создания изолированной тестовой копии
var tableScript = Core.GetTableDefinition(
    connectionString,
    "dbo.Orders",
    new TableDefinitionOptions
    {
        IncludeIdentity = true,
        IncludePrimaryKey = true,
        IncludeForeignKeys = false,  // Убираем FK для изоляции
        IncludeDefaults = true
    }
);

// Изменяем имя таблицы на TestOrders
var testTableScript = Core.ReplaceObjectName(
    tableScript,
    "dbo.Orders",
    "dbo.TestOrders"
);

// Создаем тестовую таблицу
using (var connection = new SqlConnection(connectionString))
{
    connection.Open();
    using (var cmd = new SqlCommand(testTableScript, connection))
    {
        cmd.ExecuteNonQuery();
    }
}
```

## Лицензия

MIT
