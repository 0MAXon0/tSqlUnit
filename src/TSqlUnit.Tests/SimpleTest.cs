using System.Data;
using Microsoft.Data.SqlClient;
using TSqlUnit.Comparison;
using TSqlUnit.Contexts;
using TSqlUnit.Metadata;
using TSqlUnit.Models;
using Xunit;

namespace TSqlUnit.Tests
{
    public class SqlTestContextTests
    {
        private static readonly string _connectionString =
            Environment.GetEnvironmentVariable("TSQLUNIT_TEST_CONNECTION_STRING")
            ?? @"Server=MAXon;Database=TEST;Integrated Security=true;TrustServerCertificate=True;";

        [Fact]
        public void PlayTicTacToe_WithFakes_ReturnsExpectedResultSets_AndSpyProcedureLog()
        {
            ResetPlayTicTacToeGlobalTables();

            try
            {
                var suite = new SqlTestSuite(_connectionString)
                    .Setup(ctx => ctx
                        .MockFunction("dbo.GetFactorial", @"
                            CREATE FUNCTION [dbo].[GetFactorial](@number INT)
                            RETURNS BIGINT
                            AS BEGIN
                                RETURN 111;
                            END
                        ")
                        .MockView("dbo.View_1", @"
                            CREATE VIEW [dbo].[View_1]
                            AS
                                SELECT 6 AS res;
                        ")
                        .MockTable("dbo.Products", TableDefinitionOptions.Default)
                        .MockProcedure("dbo.GenerateRandomData", "SELECT N'тест' AS [text];")
                    );

                using var context = suite
                    .ForProcedure("dbo.play_tic_tac_toe")
                    // Ситуативный override (last fake wins)
                    .MockFunction("dbo.GetFactorial", @"
                        CREATE FUNCTION [dbo].[GetFactorial](@number INT)
                        RETURNS BIGINT
                        AS BEGIN
                            RETURN 999;
                        END
                    ")
                    .Build();

                var fakeProductsName = context.GetFakeName(ObjectType.Table, "dbo.Products");
                RegisterProductsSetupSql(context, fakeProductsName);

                var outParam = new SqlParameter("@test", SqlDbType.Int)
                {
                    Direction = ParameterDirection.Output
                };

                using var result = context.ExecuteWithResult(
                    new SqlParameter("@rowNumber", (byte)1),
                    new SqlParameter("@columnNumber", (byte)2),
                    outParam);

                AssertResultSetsForPlayTicTacToe(result);
                var spyLog = context.GetSpyProcedureLog("dbo.GenerateRandomData");
                AssertSpyProcedureLogForGenerateRandomData(spyLog);
            }
            finally
            {
                ResetPlayTicTacToeGlobalTables();
            }
        }

        [Fact]
        public void MetadataReader_ReturnsDefinitions_AndCanonicalName()
        {
            var procDef = SqlMetadataReader.GetObjectDefinition(_connectionString, "dbo.play_tic_tac_toe");
            Assert.False(string.IsNullOrWhiteSpace(procDef));

            var funcDef = SqlMetadataReader.GetObjectDefinition(_connectionString, "dbo.GetFactorial");
            Assert.False(string.IsNullOrWhiteSpace(funcDef));

            var canonical = SqlMetadataReader.GetCanonicalName(_connectionString, "play_tic_tac_toe");
            Assert.False(string.IsNullOrWhiteSpace(canonical));
            Assert.Equal("[dbo].[play_tic_tac_toe]", canonical, ignoreCase: true);

            var tableDef = SqlMetadataReader.GetTableDefinition(_connectionString, "dbo.Products", TableDefinitionOptions.Maximum);
            Assert.False(string.IsNullOrWhiteSpace(tableDef));
            Assert.Contains("CREATE TABLE", tableDef, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void DataTableComparer_WhenRowsDiffer_ReturnsTsqltLikeDiffTable()
        {
            var expected = new DataTable();
            expected.Columns.Add("Id", typeof(int));
            expected.Columns.Add("Name", typeof(string));
            expected.Rows.Add(1, "A");
            expected.Rows.Add(2, "B");

            var actual = new DataTable();
            actual.Columns.Add("Id", typeof(int));
            actual.Columns.Add("Name", typeof(string));
            actual.Rows.Add(1, "A");
            actual.Rows.Add(3, "C");

            var comparison = DataTableComparer.Compare(
                expected,
                actual,
                new DataTableComparisonOptions
                {
                    IgnoreRowOrder = true,
                    SortByColumns = ["Id"],
                    IncludeMatchedRowsInDiff = true
                });

            Assert.False(comparison.IsEqual);
            Assert.NotNull(comparison.DiffTable);
            Assert.Contains("_m_", comparison.DiffMessage);
            Assert.Contains("<", comparison.DiffMessage);
            Assert.Contains(">", comparison.DiffMessage);
            Assert.Contains("=", comparison.DiffMessage);

            Assert.Equal(3, comparison.DiffTable.Rows.Count);
            Assert.True(ContainsMarker(comparison.DiffTable, "<"));
            Assert.True(ContainsMarker(comparison.DiffTable, ">"));
            Assert.True(ContainsMarker(comparison.DiffTable, "="));
        }

        private static void AssertResultSetsForPlayTicTacToe(SqlTestResult result)
        {
            Assert.NotNull(result);
            Assert.True(result.ResultSets.Count >= 5);

            Assert.Equal(1, result.ResultSets[0].Rows.Count);
            Assert.Equal("тест", Convert.ToString(result.ResultSets[0].Rows[0]["text"]));

            Assert.Equal(2, result.ResultSets[1].Rows.Count);
            Assert.Equal(1, result.ResultSets[2].Rows.Count);
            Assert.Equal(1, result.ResultSets[3].Rows.Count);
            AssertProductsResultSet(result.ResultSets[1]);

            Assert.Equal(6, Convert.ToInt32(result.ResultSets[2].Rows[0]["res"]));
            Assert.Equal(999L, Convert.ToInt64(result.ResultSets[3].Rows[0]["res"]));

            Assert.Equal(2, result.GetOutParameter<int>("@test"));
            Assert.Equal(5, result.ReturnValue ?? 0);
        }

        private static void AssertProductsResultSet(DataTable products)
        {
            Assert.NotNull(products);

            var expected = CreateExpectedProductsResultSet();
            var actual = DataTableComparer.SelectColumns(products, "CategoryID", "Name", "Weight", "Price");

            AssertDataTableEquals(expected, actual, "Name");
        }

        private static void AssertSpyProcedureLogForGenerateRandomData(DataTable spyLog)
        {
            Assert.NotNull(spyLog);

            var expected = CreateExpectedGenerateRandomDataSpyLog();
            var actual = DataTableComparer.SelectColumns(spyLog, "DataType", "MinValue", "MaxValue");

            AssertDataTableEquals(expected, actual, "DataType");
        }

        private static DataTable CreateExpectedProductsResultSet()
        {
            var table = new DataTable();
            table.Columns.Add("CategoryID", typeof(int));
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("Weight", typeof(decimal));
            table.Columns.Add("Price", typeof(decimal));

            table.Rows.Add(1, "Milk", 1.00m, 120.50m);
            table.Rows.Add(2, "Bread", 0.40m, 45.00m);

            return table;
        }

        private static DataTable CreateExpectedGenerateRandomDataSpyLog()
        {
            var table = new DataTable();
            table.Columns.Add("DataType", typeof(string));
            table.Columns.Add("MinValue", typeof(long));
            table.Columns.Add("MaxValue", typeof(long));

            table.Rows.Add("BIGINT", 3123L, 5345634L);

            return table;
        }

        private static void AssertDataTableEquals(DataTable expected, DataTable actual, params string[] sortByColumns)
        {
            var comparison = DataTableComparer.Compare(
                expected,
                actual,
                new DataTableComparisonOptions
                {
                    IgnoreColumnNameCase = true,
                    IgnoreRowOrder = true,
                    SortByColumns = sortByColumns ?? []
                });

            Assert.True(comparison.IsEqual, comparison.DiffMessage);
        }

        private static bool ContainsMarker(DataTable diffTable, string marker)
        {
            foreach (DataRow row in diffTable.Rows)
            {
                if (Convert.ToString(row["_m_"]) == marker)
                    return true;
            }

            return false;
        }

        private static void RegisterProductsSetupSql(SqlTestContext context, string fakeTableName)
        {
            var sql = string.Format(
@"INSERT INTO [dbo].[{0}] ([CategoryID], [Name], [Weight], [Price])
VALUES (1, 'Milk', 1.00, 120.50),
       (2, 'Bread', 0.40, 45.00);",
                fakeTableName
            );

            context.SetupSql(sql);
        }

        private static void ResetPlayTicTacToeGlobalTables()
        {
            using var context = new SqlTestContext(_connectionString);
            const string sql = @"
DROP TABLE IF EXISTS ##tic_tac_toe_field;
DROP TABLE IF EXISTS ##tic_tac_toe_steps;";

            context.ExecuteNonQuery(sql);
        }
    }
}
