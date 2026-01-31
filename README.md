# TSqlUnit

Библиотека на C# для unit-тестирования T-SQL кода с возможностью мокирования зависимостей.

[![License](https://img.shields.io/github/license/0MAXon0/tSqlUnit?style=flat-square)](LICENSE)
[![.NET Standard 2.0](https://img.shields.io/badge/.NET%20Standard-2.0-blue?style=flat-square)](https://docs.microsoft.com/en-us/dotnet/standard/net-standard)

---

## 🎯 Зачем это нужно?

Традиционное тестирование T-SQL кода сложное и неудобное:
- ❌ Невозможность изолировать внешние зависимости (функции, процедуры)
- ❌ Сложно контролировать поведение зависимостей в тестах
- ❌ Много boilerplate-кода для получения метаданных объектов
- ❌ Трудно создавать тестовые копии таблиц со всеми constraints

**TSqlUnit решает эти проблемы:**
- ✅ Мокирование функций при тестировании процедур
- ✅ Fluent API для удобной настройки тестов
- ✅ Автоматическая очистка временных объектов
- ✅ Получение полных определений SQL объектов
- ✅ Умная замена имен объектов в скриптах

---

## ✨ Возможности

- 🧪 **Мокирование функций** — Подменяйте зависимости для изолированного тестирования процедур
- 📋 **Получение определений объектов** — Получайте CREATE скрипты для VIEW, PROCEDURE, FUNCTION, TRIGGER
- 🔧 **Генерация CREATE TABLE** — Полный скрипт с IDENTITY, constraints, foreign keys
- 🔄 **Умная замена имен** — Безопасная замена имен объектов с учетом schema и скобок
- 🎯 **Fluent API** — Читаемый и выразительный синтаксис
- 🧹 **Автоматический cleanup** — Временные объекты удаляются автоматически
- 📦 **Минимальные зависимости** — Только Microsoft.Data.SqlClient
- 🔍 **Канонические имена** — Получение [schema].[name] из любого формата ввода

---

## 📦 Установка

```bash
dotnet add package TSqlUnit
```

Или через NuGet Package Manager:

```
Install-Package TSqlUnit
```

---

## 🚀 Быстрый старт

### Базовый пример: мокирование функции

```csharp
using TSqlUnit;
using Microsoft.Data.SqlClient;

var connectionString = "Server=localhost;Database=TestDB;Integrated Security=true;";

// Тестируем процедуру, которая использует функцию GetTaxRate
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

### Получение определения объекта

```csharp
using TSqlUnit;

var connectionString = "Server=localhost;Database=TestDB;Integrated Security=true;";

// Получить определение процедуры/функции/представления
var definition = Core.GetObjectDefinition(connectionString, "dbo.MyStoredProcedure");
Console.WriteLine(definition);
```

### Генерация CREATE TABLE скрипта

```csharp
// Полное определение со всеми constraints
var fullScript = Core.GetTableDefinition(
    connectionString,
    "dbo.Orders",
    TableDefinitionOptions.Maximum
);

// Минимальное определение (только структура)
var minimalScript = Core.GetTableDefinition(
    connectionString,
    "dbo.Orders",
    TableDefinitionOptions.Default
);
```

---

## 📚 Документация

- [Полная документация](src/TSqlUnit/README.md)
- [Примеры использования](EXAMPLES.md)
- [API Reference](src/TSqlUnit/README.md#api-reference)

---

## 🔧 Требования

- .NET Standard 2.0+ (.NET Framework 4.6.1+, .NET Core 2.0+, .NET 5+)
- SQL Server 2016+
- Microsoft.Data.SqlClient 6.1.4+

---

## 📝 Лицензия

[MIT License](LICENSE)

---

## 🤝 Вклад в проект

Contributions приветствуются! Пожалуйста, создайте issue или pull request.

---

## 📧 Контакты

- GitHub: [@0MAXon0](https://github.com/0MAXon0)
- Repository: [tSqlUnit](https://github.com/0MAXon0/tSqlUnit)
