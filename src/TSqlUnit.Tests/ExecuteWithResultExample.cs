using System;
using System.Text;
using Microsoft.Data.SqlClient;
using System.Data;
using TSqlUnit;

namespace TSqlUnit.Tests
{
    /// <summary>
    /// Примеры использования ExecuteWithResult для получения результатов выполнения процедуры
    /// </summary>
    public class ExecuteWithResultExample
    {
        private readonly string _connectionString;

        public ExecuteWithResultExample(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// Демонстрация всех возможностей ExecuteWithResult
        /// </summary>
        public void RunDemo()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("\n╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  Примеры работы с ExecuteWithResult                      ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

            // Создаем тестовую процедуру с разными типами результатов
            CreateTestProcedure();

            try
            {
                Example1_SimpleSelect();
                Example2_OutParameters();
                Example3_ReturnValue();
                Example4_MultipleResultSets();
                Example5_MapToModel();
            }
            finally
            {
                // Cleanup
                DropTestProcedure();
            }
        }

        /// <summary>
        /// Пример 1: Получение простого SELECT результата
        /// </summary>
        private void Example1_SimpleSelect()
        {
            Console.WriteLine("═══ Пример 1: Простой SELECT ═══\n");

            using (var context = new SqlTestContext(_connectionString))
            {
                context
                    .ForProcedure("dbo.TestProc_Results")
                    .Build();

                using (var result = context.ExecuteWithResult(
                    new SqlParameter("@inputValue", 42)))
                {
                    // Получаем скалярное значение
                    var scalar = result.GetScalar<int>();
                    Console.WriteLine($"✓ Скалярное значение: {scalar}");

                    // Получаем значение из конкретной колонки
                    var message = result.GetScalar<string>(0, "Message");
                    Console.WriteLine($"✓ Сообщение: {message}\n");
                }
            }
        }

        /// <summary>
        /// Пример 2: Работа с OUT параметрами
        /// </summary>
        private void Example2_OutParameters()
        {
            Console.WriteLine("═══ Пример 2: OUT параметры ═══\n");

            using (var context = new SqlTestContext(_connectionString))
            {
                context
                    .ForProcedure("dbo.TestProc_Results")
                    .Build();

                var outParam = new SqlParameter("@outputValue", SqlDbType.Int)
                {
                    Direction = ParameterDirection.Output
                };

                using (var result = context.ExecuteWithResult(
                    new SqlParameter("@inputValue", 100),
                    outParam))
                {
                    var outValue = result.GetOutParameter<int>("@outputValue");
                    Console.WriteLine($"✓ OUT параметр @outputValue: {outValue}\n");
                }
            }
        }

        /// <summary>
        /// Пример 3: Получение RETURN значения
        /// </summary>
        private void Example3_ReturnValue()
        {
            Console.WriteLine("═══ Пример 3: RETURN значение ═══\n");

            using (var context = new SqlTestContext(_connectionString))
            {
                context
                    .ForProcedure("dbo.TestProc_Results")
                    .Build();

                using (var result = context.ExecuteWithResult(
                    new SqlParameter("@inputValue", 5)))
                {
                    var returnValue = result.ReturnValue;
                    Console.WriteLine($"✓ RETURN значение: {returnValue ?? 0}\n");
                }
            }
        }

        /// <summary>
        /// Пример 4: Работа с несколькими результирующими наборами
        /// </summary>
        private void Example4_MultipleResultSets()
        {
            Console.WriteLine("═══ Пример 4: Несколько результирующих наборов ═══\n");

            using (var context = new SqlTestContext(_connectionString))
            {
                context
                    .ForProcedure("dbo.TestProc_Results")
                    .Build();

                using (var result = context.ExecuteWithResult(
                    new SqlParameter("@inputValue", 10)))
                {
                    Console.WriteLine($"✓ Количество результирующих наборов: {result.ResultSets.Count}");

                    // Первый результирующий набор
                    var firstSet = result.GetFirstResultSet();
                    Console.WriteLine($"✓ Первый набор: {firstSet.Rows.Count} строк");

                    // Второй результирующий набор (если есть)
                    if (result.ResultSets.Count > 1)
                    {
                        var secondSet = result.GetResultSet(1);
                        Console.WriteLine($"✓ Второй набор: {secondSet.Rows.Count} строк");
                    }

                    Console.WriteLine();
                }
            }
        }

        /// <summary>
        /// Пример 5: Маппинг результатов в модель
        /// </summary>
        private void Example5_MapToModel()
        {
            Console.WriteLine("═══ Пример 5: Маппинг в модель ═══\n");

            using (var context = new SqlTestContext(_connectionString))
            {
                context
                    .ForProcedure("dbo.TestProc_Results")
                    .Build();

                using (var result = context.ExecuteWithResult(
                    new SqlParameter("@inputValue", 7)))
                {
                    // Маппим в список объектов
                    var items = result.MapToList<TestResultModel>(row => new TestResultModel
                    {
                        Value = Convert.ToInt32(row["Value"]),
                        Message = row["Message"].ToString()
                    });

                    Console.WriteLine($"✓ Получено объектов: {items.Count}");
                    foreach (var item in items)
                    {
                        Console.WriteLine($"  - Value: {item.Value}, Message: {item.Message}");
                    }

                    // Или маппим только первую строку
                    var firstItem = result.MapToObject<TestResultModel>(row => new TestResultModel
                    {
                        Value = Convert.ToInt32(row["Value"]),
                        Message = row["Message"].ToString()
                    });

                    Console.WriteLine($"\n✓ Первый объект: Value={firstItem.Value}, Message={firstItem.Message}\n");
                }
            }
        }

        /// <summary>
        /// Создает тестовую процедуру с разными типами результатов
        /// </summary>
        private void CreateTestProcedure()
        {
            var sql = @"
            IF OBJECT_ID('dbo.TestProc_Results', 'P') IS NOT NULL
                DROP PROCEDURE dbo.TestProc_Results;
            GO
            
            CREATE PROCEDURE dbo.TestProc_Results
                @inputValue INT,
                @outputValue INT OUTPUT
            AS
            BEGIN
                SET NOCOUNT ON;
                
                -- Устанавливаем OUT параметр
                SET @outputValue = @inputValue * 2;
                
                -- Первый результирующий набор
                SELECT 
                    @inputValue AS Value,
                    'Первый результат' AS Message;
                
                -- Второй результирующий набор (опционально)
                IF @inputValue > 5
                BEGIN
                    SELECT 
                        @inputValue * 10 AS Value,
                        'Второй результат' AS Message;
                END
                
                -- RETURN значение
                RETURN @inputValue + 1000;
            END";

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    
                    // Выполняем каждую команду отдельно (из-за GO)
                    var commands = sql.Split(new[] { "GO" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var cmdText in commands)
                    {
                        if (!string.IsNullOrWhiteSpace(cmdText))
                        {
                            using (var command = new SqlCommand(cmdText.Trim(), connection))
                            {
                                command.ExecuteNonQuery();
                            }
                        }
                    }
                }
                Console.WriteLine("✓ Тестовая процедура создана\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠ Ошибка создания процедуры: {ex.Message}\n");
            }
        }

        /// <summary>
        /// Удаляет тестовую процедуру
        /// </summary>
        private void DropTestProcedure()
        {
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(
                        "DROP PROCEDURE IF EXISTS dbo.TestProc_Results", 
                        connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
                Console.WriteLine("\n✓ Тестовая процедура удалена");
            }
            catch
            {
                // Игнорируем ошибки при cleanup
            }
        }

        /// <summary>
        /// Модель для маппинга результатов
        /// </summary>
        private class TestResultModel
        {
            public int Value { get; set; }
            public string Message { get; set; }
        }
    }
}
