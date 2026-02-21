# TSqlUnit API Guide

Этот документ — краткая техническая шпаргалка по публичному API библиотеки `TSqlUnit`.

## Основной сценарий

```csharp
using Microsoft.Data.SqlClient;
using TSqlUnit;

using var context = new SqlTestContext(connectionString)
    .ForProcedure("dbo.MyProcedure")
    .MockFunction("dbo.GetRate", @"
        CREATE FUNCTION dbo.GetRate(@id INT)
        RETURNS DECIMAL(10,4)
        AS
        BEGIN
            RETURN 1.25;
        END")
    .Build();

context.Execute(new SqlParameter("@id", 10));
```

## `SqlTestContext`

Класс для конфигурации теста, построения окружения с подменами и выполнения тестовой процедуры.

### Конфигурация

- `ForProcedure(string procedureName)` — выбрать тестируемую процедуру.
- `MockFunction(string functionName, string fakeDefinition)` — подменить функцию.
- `MockView(string viewName, string fakeDefinition)` — подменить представление.
- `MockTable(string tableName, TableDefinitionOptions options = null)` — создать fake-таблицу на основе метаданных.
- `MockProcedure(string procedureName, string customSqlAfterSpyInsert = null)` — создать fake-процедуру + spy-таблицу.

### Дополнительный setup

- `SetupSql(string setupSql)` — SQL, выполняемый перед каждым `Execute/ExecuteWithResult`.
- `SetupProcedure(string procedureName)` — вызов setup-процедуры перед выполнением.

### Выполнение

- `Build()` — создает временные объекты и тестовую процедуру.
- `Execute(params SqlParameter[] parameters)` — выполнить без чтения результирующих наборов.
- `ExecuteWithResult(params SqlParameter[] parameters)` — выполнить и вернуть `SqlTestResult`.

### Служебные методы

- `GetSpyProcedureLog(string procedureName)` — получить таблицу логов вызовов fake-процедуры.
- `ExecuteNonQuery(string sql, params SqlParameter[] parameters)` — произвольный SQL без результирующих наборов.
- `ExecuteQuery(string sql, params SqlParameter[] parameters)` — произвольный SQL c возвратом `DataTable`.
- `GetFakeName(ObjectType objectType, string objectName)` — получить имя созданного fake-объекта.
- `Cleanup()` / `Dispose()` — удалить временные объекты.

### Важная семантика

- Для повторного fake одного и того же объекта действует правило **last fake wins**.
- После `Build()` конфигурация больше не изменяется.

## `SqlTestResult`

Обертка над результатом `ExecuteWithResult`.

- `ResultSets` — список всех результирующих наборов данных.
- `ReturnValue` — значение `RETURN`.
- `GetOutParameter<T>(string parameterName)` — значение `OUT`/`INPUTOUTPUT`.
- `GetFirstResultSet()`, `GetResultSet(int index)`.
- `GetScalar<T>()`, `GetScalar<T>(int)`, `GetScalar<T>(int, string)`.
- `MapToList<T>(...)`, `MapToObject<T>(...)`.
- `GetFirstResultSetAsText(...)`, `GetResultSetAsText(...)`.

## `SqlTestSuite`

Общий setup для группы тестов:

```csharp
var suite = new SqlTestSuite(connectionString)
    .Setup(ctx => ctx.MockFunction("dbo.GetRate", "..."));

using var context = suite.ForProcedure("dbo.Calculate").Build();
```

## `SqlMetadataReader`

Статический класс для чтения метаданных из SQL Server:

- `GetObjectDefinition(connectionString, objectName)`
- `GetTableDefinition(connectionString, tableName, options)`
- `GetCanonicalName(connectionString, objectName)`

## `SqlScriptModifier`

- `ReplaceObjectName(sqlScript, oldName, newName)` — замена имени SQL-объекта с учетом схемы и скобок.

## `DataTableComparer`

Утилиты для проверок результирующих наборов:

- `SelectColumns(...)`
- `Compare(expected, actual, options)`
- `EnsureEqual(expected, actual, options)`
- `FormatAsTextTable(table, maxRows, maxCellLength)`

`Compare(...)` возвращает `DataTableComparisonResult` с:

- `IsEqual`
- `DiffMessage`
- `DiffTable` (колонка `_m_`: `<`, `>`, `=`)

## Дополнительные типы

- `ObjectType` — тип SQL-объекта (`StoredProcedure`, `Function`, `Table`, `View`, `Trigger`).
- `TableDefinitionOptions` — флаги для генерации `CREATE TABLE`.

Внутренние служебные типы (`FakeDependency`, `FakeProcedureTemplateInfo`, `TestObjectNameGenerator`) имеют `internal`-доступ и не являются частью публичного API.

## Требуемые права в SQL Server

Минимально:

- `VIEW DEFINITION`
- `CREATE PROCEDURE`
- `CREATE FUNCTION`
- `CREATE TABLE`
- права чтения системных представлений (`sys.objects`, `sys.columns`, и т.д.)

## Контроль документации

- XML-комментарии генерируются при сборке.
- `CS1591` включен как ошибка для публичного API.
- HTML-дока строится через DocFX (`docs/docfx.json`).
