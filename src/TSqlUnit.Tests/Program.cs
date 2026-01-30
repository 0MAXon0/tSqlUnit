using System.Text;
using TSqlUnit;

// Установка кодировки консоли для корректного отображения символов
Console.OutputEncoding = Encoding.UTF8;

// Укажите вашу строку подключения
var connectionString = @"Server=MAXon;Database=TEST;Integrated Security=true;TrustServerCertificate=True;";

// Альтернативные варианты строки подключения:
// var connectionString = "Server=localhost;Database=TestDB;User Id=sa;Password=YourPassword;";
// var connectionString = "Server=.;Database=TestDB;Integrated Security=true;";

Console.WriteLine("=== Тестирование TSqlUnit ===\n");

try
{
    TestGetObjectDefinition(connectionString);
    Console.WriteLine();
    
    TestGetTableDefinitionFull(connectionString);
    Console.WriteLine();
    
    TestGetTableDefinitionMinimal(connectionString);
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Ошибка: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
    Console.ResetColor();
}

Console.WriteLine("\nНажмите любую клавишу для выхода...");
Console.ReadKey();


static void TestGetObjectDefinition(string connectionString)
{
    Console.WriteLine("--- Тест 1: GetObjectDefinition ---");
    
    // Замените на имя существующей процедуры/функции/представления в вашей БД
    var objectName = "[dbo].[play_tic_tac_toe]"; // или "sys.sp_who", "INFORMATION_SCHEMA.TABLES"
    
    Console.WriteLine($"Получаем определение объекта: {objectName}");
    
    var definition = Core.GetObjectDefinition(connectionString, objectName);
    
    if (definition != null)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✓ Объект найден:");
        Console.ResetColor();
        Console.WriteLine(definition);
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("⚠ Объект не найден или не имеет определения");
        Console.ResetColor();
    }
}

static void TestGetTableDefinitionFull(string connectionString)
{
    Console.WriteLine("--- Тест 2: GetTableDefinition (полный) ---");
    
    // Замените на имя существующей таблицы в вашей БД
    var tableName = "dbo.Products"; // или любая другая таблица
    
    Console.WriteLine($"Получаем полное определение таблицы: {tableName}");
    
    var definition = Core.GetTableDefinition(connectionString, tableName, TableDefinitionOptions.Maximum);
    
    if (definition != null)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✓ Таблица найдена:");
        Console.ResetColor();
        Console.WriteLine(definition);
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("⚠ Таблица не найдена");
        Console.ResetColor();
    }
}

static void TestGetTableDefinitionMinimal(string connectionString)
{
    Console.WriteLine("--- Тест 3: GetTableDefinition (минимальный) ---");
    
    // Замените на имя существующей таблицы в вашей БД
    var tableName = "dbo.Products";
    
    Console.WriteLine($"Получаем минимальное определение таблицы: {tableName}");
    Console.WriteLine("(только структура колонок, без constraints)");
    
    var definition = Core.GetTableDefinition(connectionString, tableName, TableDefinitionOptions.Default);
    
    if (definition != null)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✓ Таблица найдена:");
        Console.ResetColor();
        Console.WriteLine(definition);
    }
    else
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("⚠ Таблица не найдена");
        Console.ResetColor();
    }
}
