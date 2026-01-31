# Changelog

Все значимые изменения в проекте будут документированы в этом файле.

Формат основан на [Keep a Changelog](https://keepachangelog.com/ru/1.0.0/),
и проект придерживается [Semantic Versioning](https://semver.org/lang/ru/).

## [Unreleased]

### Добавлено

- Класс `SqlTestContext` для мокирования функций при тестировании процедур (Fluent API)
- Класс `FakeDependency` для хранения информации о fake объектах
- Класс `SqlObjectHelper` с вспомогательными методами для работы с БД
- Класс `TestObjectNameGenerator` для генерации уникальных имен временных объектов
- Метод `Core.GetObjectDefinition()` для получения определений VIEW/PROCEDURE/FUNCTION/TRIGGER
- Метод `Core.GetTableDefinition()` для генерации полного CREATE TABLE скрипта
- Метод `Core.GetCanonicalObjectName()` для получения канонического имени `[schema].[name]`
- Метод `Core.ReplaceObjectName()` для умной замены имен объектов в SQL скриптах
- Класс `TableDefinitionOptions` с пресетами `Default` и `Maximum`
- Enum `ObjectType` с типами SQL объектов (Table, View, StoredProcedure, Function, Trigger)
- Embedded SQL скрипт `GetTableDefinition.sql` для генерации CREATE TABLE
- Comprehensive XML documentation для всех публичных API
- README.md с документацией и примерами
- EXAMPLES.md с детальными сценариями использования
- ARCHITECTURE.md с описанием внутренней архитектуры

### Технические детали

- .NET Standard 2.0 для максимальной совместимости
- Microsoft.Data.SqlClient 6.1.4 для работы с SQL Server
- Lazy initialization для SQL скриптов из embedded resources
- Regex-based замена имен с защитой от частичных совпадений
- Автоматический cleanup через IDisposable
- COLLATE DATABASE_DEFAULT для предотвращения конфликтов collation

## [0.1.0-alpha] - 2026-01-31

### Добавлено

- Начальная версия проекта
- Базовая структура библиотеки
- Поддержка .NET Standard 2.0
- NuGet package metadata

[Unreleased]: https://github.com/0MAXon0/tSqlUnit/compare/v0.1.0-alpha...HEAD
[0.1.0-alpha]: https://github.com/0MAXon0/tSqlUnit/releases/tag/v0.1.0-alpha
