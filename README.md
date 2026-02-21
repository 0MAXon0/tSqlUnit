# TSqlUnit

`TSqlUnit` — библиотека для unit-тестирования T-SQL процедур с изоляцией зависимостей через fake-объекты.

[![License](https://img.shields.io/github/license/0MAXon0/tSqlUnit?style=flat-square)](LICENSE)
[![.NET Standard 2.0](https://img.shields.io/badge/.NET%20Standard-2.0-blue?style=flat-square)](https://docs.microsoft.com/en-us/dotnet/standard/net-standard)
![SQL Server](https://img.shields.io/badge/SQL%20Server-2016%2B-CC2927?style=flat-square&logo=microsoftsqlserver&logoColor=white)
![DocFX](https://img.shields.io/badge/Docs-DocFX-0F6CBD?style=flat-square)
![XML Docs](https://img.shields.io/badge/XML%20Docs-CS1591%20enforced-success?style=flat-square)
![Status](https://img.shields.io/badge/status-pre--release-orange?style=flat-square)
<!-- Подготовка к публикации -->
<!-- [![NuGet](https://img.shields.io/nuget/v/TSqlUnit.svg?style=flat-square&logo=nuget)](https://www.nuget.org/packages/TSqlUnit) -->
<!-- [![NuGet Downloads](https://img.shields.io/nuget/dt/TSqlUnit.svg?style=flat-square&logo=nuget)](https://www.nuget.org/packages/TSqlUnit) -->
<!-- [![CI](https://img.shields.io/github/actions/workflow/status/0MAXon0/tSqlUnit/build.yml?branch=main&style=flat-square)](https://github.com/0MAXon0/tSqlUnit/actions/workflows/build.yml) -->

## Быстрая навигация

- [Что решает библиотека](#что-решает-библиотека)
- [Возможности](#возможности)
- [Установка](#установка)
- [Подготовка к публикации](#подготовка-к-публикации)
- [Быстрый старт](#быстрый-старт)
- [Границы API и доступные точки расширения](#границы-api-и-доступные-точки-расширения)
- [Локальная разработка](#локальная-разработка)
- [Документация проекта](#документация-проекта)
- [XML-документация и DocFX](#xml-документация-и-docfx)
- [Требования](#требования)

## Что решает библиотека

- изоляция SQL-процедуры от функций/представлений/таблиц/процедур;
- детерминированные тесты T-SQL логики;
- быстрый доступ к `SELECT`/`OUT`/`RETURN` при проверках;
- сравнение результирующих наборов с понятным diff;
- автоматическая очистка временных объектов после теста.

## Возможности

- `SqlTestContext` с fluent API:
  - `ForProcedure(...)`
  - `MockFunction(...)`
  - `MockView(...)`
  - `MockTable(...)`
  - `MockProcedure(...)` (со spy-логированием)
  - `Build()`, `Execute(...)`, `ExecuteWithResult(...)`
- `SqlMetadataReader`:
  - `GetObjectDefinition(...)`
  - `GetTableDefinition(...)`
  - `GetCanonicalName(...)`
- `SqlScriptModifier.ReplaceObjectName(...)` для безопасной замены имен объектов.
- `DataTableComparer` для сравнения таблиц и текстового diff.
- `SqlTestSuite` для общего `Setup(...)` между тестами.

## Установка

Пакет еще не опубликован в NuGet, поэтому основной вариант сейчас — подключение проектом:

```powershell
dotnet add reference .\src\TSqlUnit\TSqlUnit.csproj
```

После публикации в NuGet установка будет выглядеть так:

```powershell
dotnet add package TSqlUnit
```

## Быстрый старт

```csharp
using System.Data;
using Microsoft.Data.SqlClient;
using TSqlUnit;

var connectionString = "Server=localhost;Database=TestDb;Integrated Security=true;TrustServerCertificate=True;";

using var context = new SqlTestContext(connectionString)
    .ForProcedure("dbo.CalculateOrder")
    .MockFunction("dbo.GetTaxRate", @"
        CREATE FUNCTION dbo.GetTaxRate(@state VARCHAR(2))
        RETURNS DECIMAL(5,2)
        AS
        BEGIN
            RETURN 0.10;
        END
    ")
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

var returnCode = result.ReturnValue ?? 0;
var total = result.GetOutParameter<decimal>("@total");
var firstSet = result.GetFirstResultSet();
```

## Что важно знать про поведение

- При нескольких fake для одного объекта работает правило **последний выигрывает** (`last fake wins`).
- `Build()` обязателен перед `Execute(...)` и `ExecuteWithResult(...)`.
- `Dispose()` автоматически вызывает `Cleanup()`.
- Сообщения ошибок в runtime-части библиотеки локализованы на русский.

## Границы API и доступные точки расширения

- `ExecuteNonQuery(...)` оставлен публичным намеренно: это дает возможность делать `INSERT/UPDATE/DELETE` в рамках конкретного теста, а не только через `SetupSql(...)`.
- `ExecuteQuery(...)` также оставлен публичным: это удобно для целевых проверок данных в середине теста.
- `SetupSql(...)` и `SetupProcedure(...)` подходят для общего pre-step перед каждым вызовом `Execute(...)`/`ExecuteWithResult(...)`.
- Внутренние технические типы (`FakeDependency`, `FakeProcedureTemplateInfo`, `TestObjectNameGenerator`) скрыты как `internal` и не входят в контракт для пользователя.

## Локальная разработка

```powershell
dotnet build .\src\TSqlUnit.sln
dotnet test .\src\TSqlUnit.sln
```

Сборка локального сайта документации:

```powershell
docfx metadata docs/docfx.json
docfx build docs/docfx.json --warningsAsErrors
docfx serve docs/_site --hostname 127.0.0.1 --port 8090
```

## Документация проекта

- [Архитектура](ARCHITECTURE.md)
- [Примеры](EXAMPLES.md)
- [Подробный README по API библиотеки](src/TSqlUnit/README.md)

## XML-документация и DocFX

В проекте библиотеки включены:

- генерация XML-документации (`GenerateDocumentationFile=true`);
- контроль `CS1591` как ошибка сборки для публичного API.

## Требования

- .NET Standard 2.0+;
- SQL Server 2016+;
- `Microsoft.Data.SqlClient` 6.1.4+.

## Лицензия

[MIT](LICENSE)
