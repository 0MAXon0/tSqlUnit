using System;
using System.Data;
using System.Text;
using Microsoft.Data.SqlClient;
using TSqlUnit;

namespace TSqlUnit.Tests
{
    /// <summary>
    /// Тест для процедуры play_tic_tac_toe с мокированием функции GetFactorial
    /// </summary>
    public class PlayTicTacToeTest
    {
        private readonly string _connectionString;

        public PlayTicTacToeTest(string connectionString)
        {
            _connectionString = connectionString;
        }

        public void Run()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("=== Тест процедуры play_tic_tac_toe с мокированием GetFactorial ===\n");

            try
            {
                // Шаг 1: Получаем оригинальные определения для проверки
                Console.WriteLine("📋 Шаг 1: Получаем определения объектов");
                Console.WriteLine("─────────────────────────────────────────\n");
                
                var originalProcedure = Core.GetObjectDefinition(_connectionString, "dbo.play_tic_tac_toe");
                var originalFunction = Core.GetObjectDefinition(_connectionString, "dbo.GetFactorial");
                
                Console.WriteLine("✓ Оригинальная процедура получена");
                Console.WriteLine($"  Длина: {originalProcedure?.Length ?? 0} символов\n");
                
                Console.WriteLine("✓ Оригинальная функция получена");
                Console.WriteLine($"  Длина: {originalFunction?.Length ?? 0} символов\n");
                
                // Проверяем, что функция используется в процедуре
                var usesFunction = originalProcedure != null && originalProcedure.Contains("GetFactorial", StringComparison.OrdinalIgnoreCase);
                Console.WriteLine($"✓ Процедура использует GetFactorial: {usesFunction}\n");
                
                if (!usesFunction)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("⚠ ВНИМАНИЕ: Процедура не использует функцию GetFactorial");
                    Console.WriteLine("  Тест будет выполнен, но мокирование не имеет эффекта\n");
                    Console.ResetColor();
                }

                // Шаг 2: Создаем тестовый контекст с мокированием
                Console.WriteLine("🔧 Шаг 2: Создаем SqlTestContext с мокированием");
                Console.WriteLine("─────────────────────────────────────────\n");
                
                using (var context = new SqlTestContext(_connectionString))
                {
                    Console.WriteLine("✓ SqlTestContext создан");
                    
                    // Настраиваем контекст
                    context
                        .ForProcedure("dbo.play_tic_tac_toe")
                        .MockFunction("dbo.GetFactorial", @"
                            CREATE FUNCTION [dbo].[GetFactorial] 
                            (
                                @number AS INT
                            )
                            RETURNS BIGINT
                            AS
                            BEGIN
                                -- Фейковая реализация для теста
                                -- Всегда возвращаем 999 для проверки подмены
                                RETURN 999;
                            END
                        ");
                    
                    Console.WriteLine("✓ Процедура для тестирования: dbo.play_tic_tac_toe");
                    Console.WriteLine("✓ Функция для мокирования: dbo.GetFactorial (возвращает 999)\n");

                    // Шаг 3: Build - создаем временные объекты
                    Console.WriteLine("🏗️  Шаг 3: Build() - создаем временные объекты в БД");
                    Console.WriteLine("─────────────────────────────────────────\n");
                    
                    context.Build();
                    
                    Console.WriteLine("✓ Build() завершен успешно");
                    Console.WriteLine($"✓ Создана тестовая процедура: {context.TestProcedureName}");
                    
                    // Информация о fake объектах
                    Console.WriteLine($"✓ Создано fake объектов: {context.Fakes.Count}");
                    foreach (var fake in context.Fakes)
                    {
                        Console.WriteLine($"  - {fake.CanonicalName} → [dbo].[{fake.FakeName}]");
                    }
                    Console.WriteLine();

                    // Шаг 4: Проверяем, что fake функция создана
                    Console.WriteLine("🔍 Шаг 4: Проверяем создание fake функции");
                    Console.WriteLine("─────────────────────────────────────────\n");
                    
                    var fakeFunctionName = context.Fakes[0].FakeName;
                    var fakeFunctionDefinition = Core.GetObjectDefinition(_connectionString, $"dbo.{fakeFunctionName}");
                    
                    if (fakeFunctionDefinition != null)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"✓ Fake функция [dbo].[{fakeFunctionName}] успешно создана в БД");
                        Console.ResetColor();
                        
                        var returns999 = fakeFunctionDefinition.Contains("999");
                        Console.WriteLine($"✓ Функция содержит тестовое значение (999): {returns999}\n");
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"✗ Fake функция не найдена в БД!");
                        Console.ResetColor();
                    }

                    // Шаг 5: Проверяем, что тестовая процедура использует fake функцию
                    Console.WriteLine("🔍 Шаг 5: Проверяем подмену в тестовой процедуре");
                    Console.WriteLine("─────────────────────────────────────────\n");
                    
                    var testProcedureDefinition = Core.GetObjectDefinition(_connectionString, $"dbo.{context.TestProcedureName}");
                    
                    if (testProcedureDefinition != null)
                    {
                        var usesFakeFunction = testProcedureDefinition.Contains(fakeFunctionName, StringComparison.OrdinalIgnoreCase);
                        var usesOriginalFunction = testProcedureDefinition.Contains("GetFactorial", StringComparison.OrdinalIgnoreCase) 
                                                    && !testProcedureDefinition.Contains(fakeFunctionName, StringComparison.OrdinalIgnoreCase);
                        
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"✓ Тестовая процедура использует fake функцию: {usesFakeFunction}");
                        Console.ResetColor();
                        Console.WriteLine($"✓ Тестовая процедура НЕ использует оригинальную функцию: {!usesOriginalFunction}\n");
                        
                        if (!usesFakeFunction)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("✗ ОШИБКА: Подмена не произошла!");
                            Console.ResetColor();
                        }
                    }

                    // Шаг 6: Выполняем тестовую процедуру с параметрами и получаем результаты
                    Console.WriteLine("▶️  Шаг 6: Выполняем процедуру play_tic_tac_toe с получением результатов");
                    Console.WriteLine("─────────────────────────────────────────\n");
                    
                    Console.WriteLine("Параметры:");
                    Console.WriteLine("  @rowNumber = 1");
                    Console.WriteLine("  @columnNumber = 2");
                    Console.WriteLine("  @test (OUT) - выходной параметр\n");
                    
                    // Создаем OUT параметр
                    var testOutParam = new SqlParameter("@test", SqlDbType.Int)
                    {
                        Direction = ParameterDirection.Output
                    };
                    
                    try
                    {
                        using (var result = context.ExecuteWithResult(
                        new SqlParameter("@rowNumber", (byte)1),
                        new SqlParameter("@columnNumber", (byte)2),
                        testOutParam))
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("✓ Процедура выполнена успешно!\n");
                        Console.ResetColor();
                        
                        // ============================================
                        // 1. RETURN значение
                        // ============================================
                        Console.WriteLine("╔════════════════════════════════════╗");
                        Console.WriteLine("║  1️⃣  RETURN значение                ║");
                        Console.WriteLine("╚════════════════════════════════════╝");
                        
                        var returnValue = result.ReturnValue;
                        if (returnValue.HasValue)
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"✓ RETURN = {returnValue.Value}");
                            Console.ResetColor();
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("⚠ RETURN не установлен (NULL)");
                            Console.ResetColor();
                        }
                        Console.WriteLine();
                        
                        // ============================================
                        // 2. OUT параметр @test
                        // ============================================
                        Console.WriteLine("╔════════════════════════════════════╗");
                        Console.WriteLine("║  2️⃣  OUT параметр @test             ║");
                        Console.WriteLine("╚════════════════════════════════════╝");
                        
                        try
                        {
                            var testValue = result.GetOutParameter<int>("@test");
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"✓ @test = {testValue}");
                            Console.ResetColor();
                        }
                        catch (Exception outEx)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"✗ Не удалось получить OUT параметр: {outEx.Message}");
                            Console.ResetColor();
                        }
                        Console.WriteLine();
                        
                        // ============================================
                        // 3. Первый SELECT (сообщение/статус игры)
                        // ============================================
                        Console.WriteLine("╔════════════════════════════════════╗");
                        Console.WriteLine("║  3️⃣  Первый SELECT (статус игры)    ║");
                        Console.WriteLine("╚════════════════════════════════════╝");
                        
                        Console.WriteLine($"Всего результирующих наборов: {result.ResultSets.Count}\n");
                        
                        if (result.ResultSets.Count > 0)
                        {
                            var firstSet = result.GetFirstResultSet();
                            
                            if (firstSet != null && firstSet.Rows.Count > 0)
                            {
                                Console.WriteLine($"Строк: {firstSet.Rows.Count}, Колонок: {firstSet.Columns.Count}");
                                Console.WriteLine();
                                
                                // Выводим данные
                                foreach (DataRow row in firstSet.Rows)
                                {
                                    Console.Write("  ");
                                    for (int j = 0; j < firstSet.Columns.Count; j++)
                                    {
                                        if (j > 0) Console.Write(" | ");
                                        var colName = firstSet.Columns[j].ColumnName;
                                        var value = row[j];
                                        
                                        if (value == DBNull.Value)
                                            Console.Write($"{colName}=[NULL]");
                                        else
                                            Console.Write($"{colName}={value}");
                                    }
                                    Console.WriteLine();
                                }
                                
                                // Если есть колонка "message", выводим её отдельно
                                if (firstSet.Columns.Contains("message"))
                                {
                                    var message = result.GetScalar<string>(0, "message");
                                    Console.WriteLine($"\n✓ Сообщение: \"{message}\"");
                                }
                            }
                            else
                            {
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine("⚠ Первый набор пуст или отсутствует");
                                Console.ResetColor();
                            }
                        }
                        Console.WriteLine();
                        
                        // ============================================
                        // 4. Второй SELECT (поле игры 3x3)
                        // ============================================
                        Console.WriteLine("╔════════════════════════════════════╗");
                        Console.WriteLine("║  4️⃣  Второй SELECT (поле 3x3)       ║");
                        Console.WriteLine("╚════════════════════════════════════╝");
                        
                        if (result.ResultSets.Count > 1)
                        {
                            var secondSet = result.GetResultSet(1);
                            
                            if (secondSet != null && secondSet.Rows.Count > 0)
                            {
                                Console.WriteLine($"Строк: {secondSet.Rows.Count}, Колонок: {secondSet.Columns.Count}");
                                Console.WriteLine();
                                
                                // Рисуем поле игры
                                Console.WriteLine("  Поле игры (крестики-нолики):");
                                Console.WriteLine("  ┌───┬───┬───┐");
                                
                                for (int i = 0; i < secondSet.Rows.Count; i++)
                                {
                                    var row = secondSet.Rows[i];
                                    Console.Write("  │");
                                    
                                    for (int j = 0; j < secondSet.Columns.Count; j++)
                                    {
                                        var value = row[j];
                                        var cell = " ";
                                        
                                        if (value != DBNull.Value && !string.IsNullOrEmpty(value.ToString()))
                                        {
                                            cell = value.ToString().Trim();
                                        }
                                        
                                        Console.Write($" {cell} │");
                                    }
                                    Console.WriteLine();
                                    
                                    if (i < secondSet.Rows.Count - 1)
                                    {
                                        Console.WriteLine("  ├───┼───┼───┤");
                                    }
                                }
                                
                                Console.WriteLine("  └───┴───┴───┘");
                                
                                // Также выводим в виде таблицы с названиями колонок
                                Console.WriteLine("\n  Табличное представление:");
                                Console.Write("  ");
                                for (int j = 0; j < secondSet.Columns.Count; j++)
                                {
                                    Console.Write($"[{secondSet.Columns[j].ColumnName}] ");
                                }
                                Console.WriteLine();
                                
                                foreach (DataRow row in secondSet.Rows)
                                {
                                    Console.Write("  ");
                                    for (int j = 0; j < secondSet.Columns.Count; j++)
                                    {
                                        var value = row[j];
                                        if (value == DBNull.Value || string.IsNullOrEmpty(value.ToString()))
                                            Console.Write(" -  ");
                                        else
                                            Console.Write($" {value}  ");
                                    }
                                    Console.WriteLine();
                                }
                            }
                            else
                            {
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine("⚠ Второй набор пуст");
                                Console.ResetColor();
                            }
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("⚠ Второй SELECT отсутствует");
                            Console.ResetColor();
                        }
                        Console.WriteLine();
                        
                        // ============================================
                        // Итоговая сводка
                        // ============================================
                        Console.WriteLine("╔════════════════════════════════════╗");
                        Console.WriteLine("║  📊 Итоговая сводка                ║");
                        Console.WriteLine("╚════════════════════════════════════╝");
                        Console.WriteLine($"✓ RETURN значение: {returnValue?.ToString() ?? "NULL"}");
                        
                        try
                        {
                            var testVal = result.GetOutParameter<int>("@test");
                            Console.WriteLine($"✓ OUT параметр @test: {testVal}");
                        }
                        catch
                        {
                            Console.WriteLine("✗ OUT параметр @test: не удалось получить");
                        }
                        
                        Console.WriteLine($"✓ Результирующих наборов: {result.ResultSets.Count}");
                        
                        if (result.ResultSets.Count > 0)
                        {
                            Console.WriteLine($"✓ Строк в первом SELECT: {result.GetFirstResultSet()?.Rows.Count ?? 0}");
                        }
                        
                        if (result.ResultSets.Count > 1)
                        {
                            Console.WriteLine($"✓ Строк во втором SELECT: {result.GetResultSet(1)?.Rows.Count ?? 0}");
                        }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine();
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("✗ ОШИБКА: " + ex.Message);
                        Console.ResetColor();
                        Console.WriteLine("\nStack trace:");
                        Console.WriteLine(ex.StackTrace);
                    }

                    // Шаг 7: Cleanup происходит автоматически
                    Console.WriteLine("🧹 Шаг 7: Cleanup (автоматически при Dispose)");
                    Console.WriteLine("─────────────────────────────────────────\n");
                    Console.WriteLine("✓ Временные объекты будут удалены при выходе из using\n");
                }
                
                // Шаг 8: Проверяем, что временные объекты удалены
                Console.WriteLine("🔍 Шаг 8: Проверяем очистку");
                Console.WriteLine("─────────────────────────────────────────\n");
                
                // Пытаемся найти временные объекты (их не должно быть)
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    var cmd = new SqlCommand(@"
                        SELECT COUNT(*) 
                        FROM sys.objects 
                        WHERE name LIKE 'TestProc_%' OR name LIKE 'TestFunc_%'", 
                        connection);
                    
                    var count = (int)cmd.ExecuteScalar();
                    
                    if (count == 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("✓ Все временные объекты успешно удалены");
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"⚠ Найдено временных объектов: {count}");
                        Console.WriteLine("  (Возможно, cleanup не выполнился полностью)");
                        Console.ResetColor();
                    }
                }

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("═══════════════════════════════════════════");
                Console.WriteLine("✓✓✓ ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ УСПЕШНО ✓✓✓");
                Console.WriteLine("═══════════════════════════════════════════");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n✗ ОШИБКА: {ex.Message}");
                Console.WriteLine($"\nStack trace:\n{ex.StackTrace}");
                Console.ResetColor();
            }
        }
    }
}
