# Архитектура TSqlUnit

Документ описывает актуальную архитектуру библиотеки после разбиения на модули, локализации сообщений ошибок и включения контроля XML-документации.

## Цель библиотеки

`TSqlUnit` изолирует SQL-процедуру от зависимостей (функций, представлений, таблиц, процедур), создавая временную тестовую версию процедуры и fake-объекты.  
Основной сценарий: **детерминированный тест SQL-логики в реальной БД** без влияния на production-объекты.

## Структура исходников

Исходники библиотеки находятся в `src/TSqlUnit`:

```text
src/TSqlUnit
|-- Comparison/
|   |-- DataTableComparer.cs
|   |-- DataTableComparisonOptions.cs
|   `-- DataTableComparisonResult.cs
|-- Contexts/
|   |-- SqlTestContext.cs
|   |-- SqlTestResult.cs
|   `-- SqlTestSuite.cs
|-- Fakes/
|   |-- FakeDependency.cs                 (internal)
|   `-- TestObjectNameGenerator.cs        (internal)
|-- Infrastructure/
|   `-- SqlScriptModifier.cs
|-- Metadata/
|   |-- SqlMetadataReader.cs
|   |-- TableDefinitionOptions.cs
|   `-- FakeProcedureTemplateInfo.cs      (internal)
|-- Models/
|   `-- ObjectType.cs
|-- SqlQueries/
|   |-- GetTableDefinition.sql
|   `-- GetFakeProcedureTemplateInfo.sql
`-- TSqlUnit.csproj
```

Ключевые роли модулей:

- `Contexts/` — жизненный цикл теста, выполнение и чтение результатов.
- `Metadata/` — чтение SQL-метаданных и генерация шаблонов.
- `Infrastructure/` — безопасные SQL-трансформации.
- `Comparison/` — сравнение результирующих наборов и формирование diff.

## Границы ответственности

- `SqlTestContext` управляет оркестрацией, но не содержит SQL-метаданные внутри себя.
- `SqlMetadataReader` отвечает только за запросы к системным объектам SQL Server.
- `SqlScriptModifier` отвечает только за трансформацию SQL-скриптов.
- `DataTableComparer` не зависит от конкретного test framework.

## Жизненный цикл теста

1. Создание контекста: `new SqlTestContext(connectionString)`.
2. Указание цели: `ForProcedure(...)`.
3. Регистрация fake-объектов: `MockFunction/MockView/MockTable/MockProcedure`.
4. Доп. подготовка: `SetupSql(...)` / `SetupProcedure(...)`.
5. Сборка: `Build()`.
6. Выполнение: `Execute(...)` или `ExecuteWithResult(...)`.
7. Очистка: `Cleanup()` / `Dispose()`.

## Алгоритм `Build()`

`Build()` выполняет последовательность:

1. Канонизация имени целевой процедуры (`[schema].[name]`).
2. Получение исходного определения процедуры из БД.
3. Канонизация всех fake-зависимостей.
4. Разрешение конфликтов по правилу **last fake wins**.
5. Создание fake-объектов и замена ссылок в SQL-тексте процедуры.
6. Генерация имени тестовой процедуры.
7. Создание временной тестовой процедуры в БД.

## Выполнение и результаты

- `Execute(...)` — только выполнение процедуры.
- `ExecuteWithResult(...)` — выполнение + чтение:
  - всех результирующих наборов (`ResultSets`),
  - `OUT`/`INPUTOUTPUT` параметров (`GetOutParameter<T>()`),
  - `RETURN` значения (`ReturnValue`).

## Очистка

`Cleanup()` удаляет:

- тестовую процедуру;
- все созданные fake-объекты;
- spy-таблицы fake-процедур.

Ошибки удаления подавляются намеренно, чтобы cleanup не ломал финал теста.

## Сравнение данных

`DataTableComparer` поддерживает:

- строгую проверку структуры;
- сравнение с/без учета порядка строк;
- сортировку по заданным колонкам;
- diff-таблицу с маркерами `_m_` (`<`, `>`, `=`).

## Документация и контроль качества

- В `TSqlUnit.csproj` включена генерация XML-документации.
- Для библиотеки включен `CS1591` как ошибка сборки (публичный API без XML-комментариев недопустим).
- DocFX-конфиг находится в `docs/docfx.json`.

## Принятые соглашения по именованию

- Публичный API: `PascalCase`.
- Приватные поля/локальные переменные: `camelCase` с префиксом `_` для полей.
- Для setup используется единое написание `setup` без вариантов `setUp`.
