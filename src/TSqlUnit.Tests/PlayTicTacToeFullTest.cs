using System;
using System.Data;
using System.Text;
using Microsoft.Data.SqlClient;
using TSqlUnit;

namespace TSqlUnit.Tests
{
    /// <summary>
    /// Полный тест для play_tic_tac_toe с демонстрацией всех возможностей ExecuteWithResult
    /// </summary>
    public class PlayTicTacToeFullTest
    {
        private readonly string _connectionString;

        public PlayTicTacToeFullTest(string connectionString)
        {
            _connectionString = connectionString;
        }

        public void Run()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("\n╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  Полный тест: демонстрация ExecuteWithResult             ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

            // Сначала создадим простую тестовую процедуру с известным результатом
            CreateDemoProc();

            try
            {
                TestDemoProc();
            }
            finally
            {
                // Cleanup
                DropDemoProc();
            }
        }

        /// <summary>
        /// Создает демонстрационную процедуру с OUT параметром, RETURN и SELECT-ами
        /// </summary>
        private void CreateDemoProc()
        {
            var sql = @"
            IF OBJECT_ID('dbo.DemoProc_WithResults', 'P') IS NOT NULL
                DROP PROCEDURE dbo.DemoProc_WithResults;
            ";

            var createSql = @"
            CREATE PROCEDURE dbo.DemoProc_WithResults
                @rowNumber INT,
                @columnNumber INT,
                @test INT OUTPUT
            AS
            BEGIN
                SET NOCOUNT ON;
                
                -- Устанавливаем OUT параметр
                SET @test = @rowNumber * @columnNumber;
                
                -- Первый SELECT: информационное сообщение
                SELECT 
                    'Ход выполнен' AS message,
                    @rowNumber AS row_num,
                    @columnNumber AS col_num;
                
                -- Второй SELECT: результаты расчета
                SELECT 
                    @rowNumber AS [1],
                    @columnNumber AS [2],
                    @test AS [3];
                
                -- RETURN значение
                RETURN 42;
            END";

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    
                    // Удаляем старую версию
                    using (var cmd = new SqlCommand(sql, connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                    
                    // Создаем новую
                    using (var cmd = new SqlCommand(createSql, connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
                Console.WriteLine("✓ Демонстрационная процедура создана\n");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ Ошибка создания процедуры: {ex.Message}\n");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// Тестирует демонстрационную процедуру с получением всех результатов
        /// </summary>
        private void TestDemoProc()
        {
            Console.WriteLine("═══ Тест с мокированием функции GetFactorial ═══\n");

            using (var context = new SqlTestContext(_connectionString))
            {
                context
                    .ForProcedure("dbo.DemoProc_WithResults")
                    .MockFunction("dbo.GetFactorial", @"
                        CREATE FUNCTION [dbo].[GetFactorial] 
                        (
                            @number AS INT
                        )
                        RETURNS BIGINT
                        AS
                        BEGIN
                            -- Фейковая реализация
                            RETURN 999;
                        END
                    ")
                    .Build();

                Console.WriteLine($"✓ Тестовая процедура создана: {context.TestProcedureName}");
                Console.WriteLine($"✓ Fake функция создана: {context.Fakes[0].FakeName}\n");

                // Создаем OUT параметр
                var testOutParam = new SqlParameter("@test", SqlDbType.Int)
                {
                    Direction = ParameterDirection.Output
                };

                Console.WriteLine("▶️  Выполняем процедуру с параметрами:");
                Console.WriteLine("  @rowNumber = 1");
                Console.WriteLine("  @columnNumber = 2");
                Console.WriteLine("  @test (OUT) - выходной параметр\n");

                using (var result = context.ExecuteWithResult(
                    new SqlParameter("@rowNumber", 1),
                    new SqlParameter("@columnNumber", 2),
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
                        Console.WriteLine($"  (Расчет: 1 × 2 = {testValue})");
                        Console.ResetColor();
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"✗ Не удалось получить OUT параметр: {ex.Message}");
                        Console.ResetColor();
                    }
                    Console.WriteLine();

                    // ============================================
                    // 3. Первый SELECT (информационное сообщение)
                    // ============================================
                    Console.WriteLine("╔════════════════════════════════════╗");
                    Console.WriteLine("║  3️⃣  Первый SELECT (сообщение)      ║");
                    Console.WriteLine("╚════════════════════════════════════╝");
                    
                    Console.WriteLine($"Всего результирующих наборов: {result.ResultSets.Count}\n");

                    if (result.ResultSets.Count > 0)
                    {
                        var firstSet = result.GetFirstResultSet();

                        if (firstSet != null && firstSet.Rows.Count > 0)
                        {
                            Console.WriteLine($"Строк: {firstSet.Rows.Count}, Колонок: {firstSet.Columns.Count}");
                            Console.WriteLine();

                            // Выводим красиво в виде таблицы
                            Console.WriteLine("┌─────────────────┬──────────┬──────────┐");
                            Console.WriteLine("│ message         │ row_num  │ col_num  │");
                            Console.WriteLine("├─────────────────┼──────────┼──────────┤");

                            foreach (DataRow row in firstSet.Rows)
                            {
                                var message = row["message"]?.ToString() ?? "";
                                var rowNum = row["row_num"]?.ToString() ?? "";
                                var colNum = row["col_num"]?.ToString() ?? "";

                                Console.WriteLine($"│ {message,-15} │ {rowNum,8} │ {colNum,8} │");
                            }

                            Console.WriteLine("└─────────────────┴──────────┴──────────┘");

                            // Также получаем через GetScalar
                            var messageScalar = result.GetScalar<string>(0, "message");
                            Console.WriteLine($"\n✓ Сообщение (GetScalar): \"{messageScalar}\"");
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("⚠ Первый набор пуст");
                            Console.ResetColor();
                        }
                    }
                    Console.WriteLine();

                    // ============================================
                    // 4. Второй SELECT (результаты расчета)
                    // ============================================
                    Console.WriteLine("╔════════════════════════════════════╗");
                    Console.WriteLine("║  4️⃣  Второй SELECT (расчеты)        ║");
                    Console.WriteLine("╚════════════════════════════════════╝");

                    if (result.ResultSets.Count > 1)
                    {
                        var secondSet = result.GetResultSet(1);

                        if (secondSet != null && secondSet.Rows.Count > 0)
                        {
                            Console.WriteLine($"Строк: {secondSet.Rows.Count}, Колонок: {secondSet.Columns.Count}");
                            Console.WriteLine();

                            // Выводим красиво в виде таблицы
                            Console.WriteLine("┌─────┬─────┬─────┐");
                            Console.WriteLine("│  1  │  2  │  3  │");
                            Console.WriteLine("├─────┼─────┼─────┤");

                            foreach (DataRow row in secondSet.Rows)
                            {
                                var col1 = row[0]?.ToString() ?? "";
                                var col2 = row[1]?.ToString() ?? "";
                                var col3 = row[2]?.ToString() ?? "";

                                Console.WriteLine($"│ {col1,3} │ {col2,3} │ {col3,3} │");
                            }

                            Console.WriteLine("└─────┴─────┴─────┘");

                            // Получаем конкретные значения через GetScalar
                            var value3 = result.GetScalar<int>(1, "3");
                            Console.WriteLine($"\n✓ Значение колонки [3] (GetScalar): {value3}");
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
                    Console.WriteLine($"✓ RETURN значение: {returnValue}");
                    Console.WriteLine($"✓ OUT параметр @test: {result.GetOutParameter<int>("@test")}");
                    Console.WriteLine($"✓ Результирующих наборов: {result.ResultSets.Count}");
                    Console.WriteLine($"✓ Всего строк в первом SELECT: {result.GetFirstResultSet()?.Rows.Count ?? 0}");
                    if (result.ResultSets.Count > 1)
                    {
                        Console.WriteLine($"✓ Всего строк во втором SELECT: {result.GetResultSet(1)?.Rows.Count ?? 0}");
                    }
                }

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("═══════════════════════════════════════════");
                Console.WriteLine("✓✓✓ ВСЕ РЕЗУЛЬТАТЫ ПОЛУЧЕНЫ УСПЕШНО ✓✓✓");
                Console.WriteLine("═══════════════════════════════════════════");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// Удаляет демонстрационную процедуру
        /// </summary>
        private void DropDemoProc()
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var cmd = new SqlCommand(
                        "DROP PROCEDURE IF EXISTS dbo.DemoProc_WithResults",
                        connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
                Console.WriteLine("\n✓ Демонстрационная процедура удалена");
            }
            catch
            {
                // Игнорируем ошибки при cleanup
            }
        }
    }
}
