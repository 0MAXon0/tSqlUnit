using Microsoft.Data.SqlClient;
using System;
using System.IO;

namespace TSqlUnit
{
    /// <summary>
    /// Основной класс библиотеки для работы с определениями SQL объектов
    /// </summary>
    public static class Core
    {
        // Кеширование SQL запросов из embedded resources
        private static readonly Lazy<string> _getTableDefinitionSql = 
            new Lazy<string>(() => GetEmbeddedSql("GetTableDefinition.sql"));

        /// <summary>
        /// Читает SQL скрипт из embedded resource
        /// </summary>
        /// <param name="fileName">Имя файла в папке SqlQueries</param>
        /// <returns>Содержимое SQL файла</returns>
        /// <exception cref="InvalidOperationException">Если файл не найден в ресурсах</exception>
        private static string GetEmbeddedSql(string fileName)
        {
            var assembly = typeof(Core).Assembly;
            var resourceName = $"TSqlUnit.SqlQueries.{fileName}";

            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new InvalidOperationException($"Не удалось найти embedded resource: {resourceName}");

                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        /// <summary>
        /// Получает определение SQL объекта (представление, процедура, функция, триггер)
        /// </summary>
        /// <param name="connectionString">Строка подключения к SQL Server</param>
        /// <param name="objectName">Имя объекта (например: dbo.MyProc или MyProc)</param>
        /// <returns>Определение объекта или null, если объект не найден</returns>
        /// <exception cref="ArgumentNullException">Если connectionString или objectName равны null или пусты</exception>
        /// <exception cref="SqlException">При ошибке подключения или выполнения запроса</exception>
        public static string GetObjectDefinition(string connectionString, string objectName)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));
            
            if (string.IsNullOrWhiteSpace(objectName))
                throw new ArgumentNullException(nameof(objectName));

            var sql = "SELECT OBJECT_DEFINITION(OBJECT_ID(@object_name)) AS object_definition";

            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@object_name", objectName);
                connection.Open();
                return command.ExecuteScalar() as string;
            }
        }

        /// <summary>
        /// Получает определение таблицы в виде CREATE TABLE скрипта
        /// </summary>
        /// <param name="connectionString">Строка подключения к SQL Server</param>
        /// <param name="tableName">Имя таблицы (например: dbo.Users или Users)</param>
        /// <param name="options">Опции формирования скрипта. Если null, используются настройки по умолчанию</param>
        /// <returns>CREATE TABLE скрипт со всеми constraints, или null если таблица не найдена</returns>
        /// <exception cref="ArgumentNullException">Если connectionString или tableName равны null или пусты</exception>
        /// <exception cref="SqlException">При ошибке подключения или выполнения запроса</exception>
        /// <remarks>
        /// Поддерживает:
        /// - IDENTITY (seed, increment)
        /// - Вычисляемые столбцы (computed columns)
        /// - DEFAULT constraints
        /// - PRIMARY KEY (CLUSTERED/NONCLUSTERED)
        /// - UNIQUE constraints
        /// - FOREIGN KEY с ON DELETE/UPDATE
        /// - CHECK constraints
        /// </remarks>
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
    }
}