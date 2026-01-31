using System;
using System.IO;
using Microsoft.Data.SqlClient;

namespace TSqlUnit
{
    /// <summary>
    /// Читает метаданные SQL объектов из базы данных
    /// </summary>
    public static class SqlMetadataReader
    {
        private static readonly Lazy<string> _getTableDefinitionSql = 
            new Lazy<string>(() => GetEmbeddedSql("GetTableDefinition.sql"));

        /// <summary>
        /// Получает определение VIEW/PROCEDURE/FUNCTION/TRIGGER
        /// </summary>
        public static string GetObjectDefinition(string connectionString, string objectName)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));
            if (string.IsNullOrWhiteSpace(objectName))
                throw new ArgumentNullException(nameof(objectName));

            var sql = "SELECT OBJECT_DEFINITION(OBJECT_ID(@object_name))";

            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@object_name", objectName);
                connection.Open();
                return command.ExecuteScalar() as string;
            }
        }

        /// <summary>
        /// Генерирует CREATE TABLE скрипт с constraints
        /// </summary>
        public static string GetTableDefinition(string connectionString, string tableName, TableDefinitionOptions options = null)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentNullException(nameof(tableName));

            options = options ?? TableDefinitionOptions.Default;
            var sql = _getTableDefinitionSql.Value;

            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddRange(new[]
                {
                    new SqlParameter("@table_name", tableName),
                    new SqlParameter("@include_computed_columns", options.IncludeComputedColumns),
                    new SqlParameter("@include_not_null", options.IncludeNotNull),
                    new SqlParameter("@include_identity", options.IncludeIdentity),
                    new SqlParameter("@include_defaults", options.IncludeDefaults),
                    new SqlParameter("@include_primary_key", options.IncludePrimaryKey),
                    new SqlParameter("@include_foreign_keys", options.IncludeForeignKeys),
                    new SqlParameter("@include_check_constraints", options.IncludeCheckConstraints),
                    new SqlParameter("@include_unique_constraints", options.IncludeUniqueConstraints)
                });

                connection.Open();
                return command.ExecuteScalar() as string;
            }
        }

        /// <summary>
        /// Получает каноническое имя [schema].[name] из БД
        /// </summary>
        public static string GetCanonicalName(string connectionString, string objectName)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));
            if (string.IsNullOrWhiteSpace(objectName))
                throw new ArgumentNullException(nameof(objectName));

            var sql = @"SELECT QUOTENAME(OBJECT_SCHEMA_NAME(OBJECT_ID(@objectName))) + '.' + 
                               QUOTENAME(OBJECT_NAME(OBJECT_ID(@objectName)))";

            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@objectName", objectName);
                connection.Open();
                return command.ExecuteScalar() as string;
            }
        }

        private static string GetEmbeddedSql(string fileName)
        {
            var assembly = typeof(SqlMetadataReader).Assembly;
            var resourceName = $"TSqlUnit.SqlQueries.{fileName}";

            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new InvalidOperationException($"Embedded resource not found: {resourceName}");

                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }
    }
}
