using System;
using System.Data;
using Microsoft.Data.SqlClient;
using TSqlUnit;

namespace TSqlUnit.Tests
{
    public class SimpleTest
    {
        private readonly string _connectionString;

        public SimpleTest(string connectionString)
        {
            _connectionString = connectionString;
        }

        public void Run()
        {
            Console.WriteLine("=== TSqlUnit Test ===\n");

            TestMockingFunction();
            TestMetadataReader();
        }

        private void TestMockingFunction()
        {
            Console.WriteLine("1. Mocking test:");

            using (var context = new SqlTestContext(_connectionString))
            {
                context
                    .ForProcedure("dbo.play_tic_tac_toe")
                    .MockFunction("dbo.GetFactorial", @"
                        CREATE FUNCTION [dbo].[GetFactorial](@number INT)
                        RETURNS BIGINT
                        AS BEGIN
                            RETURN 999;
                        END
                    ")
                    .Build();

                Console.WriteLine($"   Test procedure: {context.TestProcedureName}");
                Console.WriteLine($"   Fake function: {context.Fakes[0].FakeName}");

                var outParam = new SqlParameter("@test", SqlDbType.Int)
                {
                    Direction = ParameterDirection.Output
                };

                try
                {
                    using (var result = context.ExecuteWithResult(
                        new SqlParameter("@rowNumber", (byte)1),
                        new SqlParameter("@columnNumber", (byte)2),
                        outParam))
                    {
                        Console.WriteLine($"   RETURN: {result.ReturnValue}");
                        Console.WriteLine($"   OUT @test: {result.GetOutParameter<int>("@test")}");
                        Console.WriteLine($"   Result sets: {result.ResultSets.Count}");
                        Console.WriteLine("   ✓ Success\n");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   ✗ Error: {ex.Message}\n");
                }
            }
        }

        private void TestMetadataReader()
        {
            Console.WriteLine("2. Metadata reader test:");

            var procDef = SqlMetadataReader.GetObjectDefinition(_connectionString, "dbo.play_tic_tac_toe");
            Console.WriteLine($"   Procedure length: {procDef?.Length ?? 0} chars");

            var funcDef = SqlMetadataReader.GetObjectDefinition(_connectionString, "dbo.GetFactorial");
            Console.WriteLine($"   Function length: {funcDef?.Length ?? 0} chars");

            var canonical = SqlMetadataReader.GetCanonicalName(_connectionString, "play_tic_tac_toe");
            Console.WriteLine($"   Canonical name: {canonical}");

            var tableDef = SqlMetadataReader.GetTableDefinition(_connectionString, "dbo.Products", TableDefinitionOptions.Maximum);
            Console.WriteLine($"   Table script length: {tableDef?.Length ?? 0} chars");

            Console.WriteLine("   ✓ Success\n");
        }
    }
}
