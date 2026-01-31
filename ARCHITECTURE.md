# Архитектура TSqlUnit

Этот документ описывает внутреннюю архитектуру библиотеки TSqlUnit.

## Структура проекта

```
TSqlUnit/
├── Core.cs                         # Статические методы для работы с SQL объектами
├── SqlTestContext.cs               # Основной класс для мокирования (Fluent API)
├── FakeDependency.cs               # Модель данных для fake объектов
├── SqlObjectHelper.cs              # Вспомогательные методы для работы с БД
├── TestObjectNameGenerator.cs      # Генератор уникальных имен
├── TableDefinitionOptions.cs       # Опции для генерации CREATE TABLE
├── ObjectType.cs                   # Enum типов SQL объектов
└── SqlQueries/
    └── GetTableDefinition.sql      # SQL скрипт для генерации CREATE TABLE
```

## Основные компоненты

### 1. Core (Статический класс)

Центральный класс с утилитами для работы с SQL объектами.

**Основные методы:**

- `GetObjectDefinition()` - Получает определение VIEW/PROCEDURE/FUNCTION/TRIGGER через `OBJECT_DEFINITION()`
- `GetTableDefinition()` - Генерирует полный CREATE TABLE скрипт с constraints
- `GetCanonicalObjectName()` - Получает каноническое имя `[schema].[name]`
- `ReplaceObjectName()` - Умная замена имен объектов в SQL скриптах

**Особенности:**

- Использует `Lazy<T>` для кеширования SQL скриптов из embedded resources
- Все методы требуют `connectionString` для работы с БД
- Методы `ReplaceObjectName()` использует regex для безопасной замены имен

### 2. SqlTestContext (Основной класс для тестирования)

Главный класс библиотеки, реализующий Fluent API для настройки и выполнения тестов.

**Жизненный цикл:**

```
Создание → ForProcedure() → MockFunction() × N → Build() → Execute() → Cleanup()
```

**Внутренняя логика Build():**

1. Получает каноническое имя процедуры
2. Получает определение процедуры через `Core.GetObjectDefinition()`
3. Для каждой fake функции:
   - Получает каноническое имя оригинальной функции
   - Генерирует уникальное имя для fake функции
   - Заменяет имя в скрипте пользователя
   - Создает fake функцию в БД
   - Заменяет вызовы оригинальной функции в определении процедуры
4. Генерирует уникальное имя для тестовой процедуры
5. Заменяет имя процедуры в определении
6. Создает тестовую процедуру в БД

**Cleanup:**

- Удаляет тестовую процедуру
- Удаляет все fake функции
- Вызывается автоматически через `IDisposable`

### 3. TestObjectNameGenerator

Генератор уникальных имен для временных SQL объектов.

**Формат имени:**

```
Test{Type}_{OriginalName}_{GUID6}
```

Примеры:
- `TestProc_CalculateOrder_a1b2c3`
- `TestFunc_GetTaxRate_d4e5f6`
- `TestTable_Orders_g7h8i9`

**GUID:** Использует первые 6 символов GUID для уникальности.

### 4. SqlObjectHelper

Вспомогательный класс для работы с SQL Server.

**Методы:**

- `GetCanonicalName()` - Делегирует `Core.GetCanonicalObjectName()`
- `ExecuteSql()` - Выполняет произвольный SQL
- `DropObject()` - Удаляет объект (игнорирует ошибки)

### 5. FakeDependency

Модель данных для хранения информации о fake объекте.

**Свойства:**

- `OriginalName` - Имя как передал пользователь
- `CanonicalName` - Каноническое имя `[schema].[name]`
- `FakeName` - Сгенерированное имя (без схемы)
- `ObjectType` - Тип объекта (Function, Table, etc.)
- `FakeDefinition` - Скрипт пользователя
- `FakeDefinitionRenamed` - Скрипт с замененным именем

### 6. TableDefinitionOptions

Опции для настройки генерации CREATE TABLE скрипта.

**Пресеты:**

- `Default` - Минимальная конфигурация (все флаги = false)
- `Maximum` - Полная конфигурация (все флаги = true)

## Алгоритм замены имен объектов

Метод `Core.ReplaceObjectName()` использует двухэтапную логику:

### Шаг 1: Замена с явной схемой

Паттерн:
```regex
(?<![.\[\]\w])\[?{oldSchema}\]?\.\[?{oldName}\]?(?![.\[\]\w])
```

Найдет и заменит:
- `dbo.MyFunc`
- `[dbo].MyFunc`
- `dbo.[MyFunc]`
- `[dbo].[MyFunc]`

### Шаг 2: Замена без схемы (только для dbo)

Если `oldSchema == "dbo"`, дополнительно заменяет объекты без схемы:

Паттерн:
```regex
(?<![.\[\]\w])\[?{oldName}\]?(?![.\[\]\w])
```

Найдет и заменит:
- `MyFunc`
- `[MyFunc]`

**Почему только для dbo?**

В SQL Server объекты без схемы по умолчанию принадлежат схеме `dbo`. Если у нас схема `sales`, то `Users` != `sales.Users`.

## Генерация CREATE TABLE скрипта

SQL скрипт `GetTableDefinition.sql` использует CTE для сбора информации:

1. **table_info** - Информация о столбцах (включая computed, identity, defaults)
2. **pk_info** - Информация о PRIMARY KEY (включая CLUSTERED/NONCLUSTERED)
3. **uq_info** - Информация о UNIQUE constraints
4. **fk_info** - Информация о FOREIGN KEYs (включая ON DELETE/UPDATE)
5. **chk_info** - Информация о CHECK constraints

Финальный скрипт собирается через `STRING_AGG` и `FOR XML PATH`.

**Использование `COLLATE DATABASE_DEFAULT`:**

Все строковые поля из системных таблиц используют `COLLATE DATABASE_DEFAULT` для предотвращения конфликтов collation.

## Встроенные ресурсы (Embedded Resources)

SQL скрипты хранятся как embedded resources и кешируются через `Lazy<T>`.

**Преимущества:**

- SQL скрипты компилируются в DLL
- Не требуется дополнительных файлов при распространении
- Быстрый доступ (кеширование)
- Версионирование вместе с кодом

**Доступ:**

```csharp
private static string GetEmbeddedSql(string fileName)
{
    var assembly = typeof(Core).Assembly;
    var resourceName = $"TSqlUnit.SqlQueries.{fileName}";
    using (var stream = assembly.GetManifestResourceStream(resourceName))
    using (var reader = new StreamReader(stream))
    {
        return reader.ReadToEnd();
    }
}
```

## Совместимость с .NET Standard 2.0

Библиотека таргетирует .NET Standard 2.0 для максимальной совместимости.

**Ограничения:**

- Нельзя использовать `switch` expression (C# 8.0+)
- Нельзя использовать range operator `[..6]` (C# 8.0+)
- Нельзя использовать `??=` (C# 8.0+)
- Используем `string.Format()` вместо string interpolation где нужна поддержка старых версий

**Вместо:**

```csharp
var name = $"Test_{obj}";        // ✗
var sub = guid[..6];             // ✗
list ??= new List<T>();          // ✗
```

**Используем:**

```csharp
var name = string.Format("Test_{0}", obj);  // ✓
var sub = guid.Substring(0, 6);             // ✓
if (list == null) list = new List<T>();    // ✓
```

## Безопасность

### SQL Injection

Все пользовательские данные передаются через параметры `SqlParameter`:

```csharp
command.Parameters.AddWithValue("@objectName", objectName);
```

### Права доступа

Минимальные требуемые права:

- `VIEW DEFINITION` - для получения определений
- `CREATE PROCEDURE` - для создания тестовых процедур
- `CREATE FUNCTION` - для создания fake функций
- `DROP PROCEDURE` / `DROP FUNCTION` - для cleanup

## Паттерны проектирования

### 1. Fluent Interface (Fluent API)

`SqlTestContext` использует Fluent Interface для читаемости:

```csharp
context
    .ForProcedure("dbo.MyProc")
    .MockFunction("dbo.Func1", "...")
    .MockFunction("dbo.Func2", "...")
    .Build()
    .Execute();
```

### 2. Builder Pattern

`SqlTestContext.Build()` собирает все компоненты перед выполнением.

### 3. Lazy Initialization

SQL скрипты загружаются только при первом использовании:

```csharp
private static readonly Lazy<string> _getTableDefinitionSql = 
    new Lazy<string>(() => GetEmbeddedSql("GetTableDefinition.sql"));
```

### 4. Dispose Pattern

Автоматическая очистка через `IDisposable`:

```csharp
using (var context = new SqlTestContext(connectionString))
{
    // ...
} // Cleanup() вызовется автоматически
```

## Будущие улучшения

### Планируется добавить:

- [ ] Мокирование таблиц (создание fake таблиц с тестовыми данными)
- [ ] Мокирование представлений (views)
- [ ] Поддержка табличных функций (table-valued functions)
- [ ] Транзакционная изоляция тестов (автоматический ROLLBACK)
- [ ] Snapshot testing (сравнение результатов с эталоном)
- [ ] Интеграция с популярными assertion библиотеками (FluentAssertions, Shouldly)
- [ ] Parallel execution (параллельное выполнение тестов)
- [ ] Detailed error reporting (детальные отчеты об ошибках)

## FAQ

### Почему не используется Entity Framework Core?

EF Core - это ORM для работы с данными, а не для тестирования T-SQL кода. Наша задача - получать метаданные объектов и управлять DDL, а не работать с данными.

### Почему статические методы в Core?

Для простоты использования. Методы не хранят состояние и могут быть статическими.

### Можно ли мокировать процедуры?

Пока нет, но это планируется в будущих версиях. Текущая реализация фокусируется на функциях, так как они чаще всего являются зависимостями.

### Можно ли использовать с MS Test / NUnit / xUnit?

Да! `SqlTestContext` - это обычный C# класс, который можно использовать с любым тестовым фреймворком.
