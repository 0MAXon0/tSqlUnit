using System.Text;
using TSqlUnit;
using TSqlUnit.Tests;

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
    // Тест play_tic_tac_toe с получением всех результатов
    var test = new PlayTicTacToeTest(connectionString);
    test.Run();
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Ошибка: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
    Console.ResetColor();
}

Console.WriteLine("\n✓ Тест завершен");

Console.ReadKey();
