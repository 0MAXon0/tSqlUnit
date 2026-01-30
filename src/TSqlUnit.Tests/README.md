# TSqlUnit.Tests

Консольное приложение для тестирования библиотеки TSqlUnit.

## Как запустить

1. **Настройте строку подключения** в `Program.cs`:
   ```csharp
   var connectionString = @"Server=(localdb)\mssqllocaldb;Database=TestDB;Integrated Security=true;TrustServerCertificate=True;";
   ```

2. **Измените имена объектов** на существующие в вашей БД:
   - `objectName` — процедура, функция или представление (например: `"dbo.MyStoredProc"`)
   - `tableName` — таблица (например: `"dbo.Users"`)

3. **Запустите проект:**
   ```bash
   dotnet run --project src/TSqlUnit.Tests
   ```
   
   Или из Visual Studio: установите `TSqlUnit.Tests` как StartUp проект и нажмите F5.

## Примеры строк подключения

**LocalDB (встроен в Visual Studio):**
```csharp
@"Server=(localdb)\mssqllocaldb;Database=TestDB;Integrated Security=true;"
```

**SQL Server Authentication:**
```csharp
"Server=localhost;Database=TestDB;User Id=sa;Password=YourPassword;"
```

**Windows Authentication:**
```csharp
"Server=.;Database=TestDB;Integrated Security=true;"
```

## Что тестируется

### Тест 1: GetObjectDefinition
Получение определения SQL объекта (процедура, функция, представление):
```csharp
var definition = Core.GetObjectDefinition(connectionString, "dbo.MyStoredProc");
```

### Тест 2: GetTableDefinition (Maximum)
Полный CREATE TABLE скрипт со всеми constraints:
- IDENTITY
- Computed columns
- DEFAULT constraints
- PRIMARY KEY (CLUSTERED/NONCLUSTERED)
- UNIQUE constraints
- FOREIGN KEY с ON DELETE/UPDATE
- CHECK constraints

```csharp
var definition = Core.GetTableDefinition(connectionString, "dbo.Users", TableDefinitionOptions.Maximum);
```

### Тест 3: GetTableDefinition (Default)
Минимальный CREATE TABLE скрипт (только структура колонок):
```csharp
var definition = Core.GetTableDefinition(connectionString, "dbo.Users", TableDefinitionOptions.Default);
```

## Результаты

Приложение выводит результаты с цветами:
- 🟢 **Зелёный** — объект найден и получен
- 🟡 **Жёлтый** — объект не найден
- 🔴 **Красный** — ошибка выполнения
