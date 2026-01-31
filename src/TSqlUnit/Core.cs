using Microsoft.Data.SqlClient;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

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


        /// <summary>
        /// Заменяет все вхождения имени объекта в SQL определении.
        /// Умная логика: для dbo заменяет с/без схемы, для других только с явной схемой.
        /// Защищает от частичных совпадений (test не заменит test2 или mytest).
        /// </summary>
        public static string ReplaceObjectName(string definition, string oldFullName, string newFullName)
        {
            if (string.IsNullOrWhiteSpace(definition))
                throw new ArgumentNullException(nameof(definition));

            if (string.IsNullOrWhiteSpace(oldFullName))
                throw new ArgumentNullException(nameof(oldFullName));

            if (string.IsNullOrWhiteSpace(newFullName))
                throw new ArgumentNullException(nameof(newFullName));

            var (oldSchema, oldName) = ParseName(oldFullName);
            var (newSchema, newName) = ParseName(newFullName);

            var result = definition;

            // Шаг 1: ВСЕГДА заменяем с явной схемой
            // Паттерн: [oldSchema].[oldName] (в любых вариациях скобок)
            var patternWithSchema = string.Format(
                @"(?<![.\[\]\w])\[?{0}\]?\.\[?{1}\]?(?![.\[\]\w])",
                Regex.Escape(oldSchema),
                Regex.Escape(oldName)
            );

            var replacement = string.Format("[{0}].[{1}]", newSchema, newName);

            result = Regex.Replace(result, patternWithSchema, replacement, RegexOptions.IgnoreCase);

            // Шаг 2: Если старая схема = dbo, заменяем также БЕЗ схемы
            // (потому что объект без схемы в SQL Server = dbo)
            if (oldSchema.Equals("dbo", StringComparison.OrdinalIgnoreCase))
            {
                var patternWithoutSchema = string.Format(
                    @"(?<![.\[\]\w])\[?{0}\]?(?![.\[\]\w])",
                    Regex.Escape(oldName)
                );

                result = Regex.Replace(result, patternWithoutSchema, replacement, RegexOptions.IgnoreCase);
            }

            return result;
        }

        private static (string schema, string name) ParseName(string fullName)
        {
            // Убираем квадратные скобки
            var cleaned = fullName.Replace("[", "").Replace("]", "");

            // Разделяем по точке
            var parts = cleaned.Split('.');

            if (parts.Length == 2)
            {
                return (parts[0], parts[1]);
            }
            else if (parts.Length == 1)
            {
                // Если схема не указана, по умолчанию dbo
                return ("dbo", parts[0]);
            }
            else
            {
                throw new ArgumentException(
                    string.Format("Invalid object name format: '{0}'. Expected: [schema].[name] or name", fullName),
                    nameof(fullName));
            }
        }

        /// <summary>
        /// Получает каноническое имя объекта в формате [schema].[name] из SQL Server
        /// </summary>
        /// <param name="connectionString">Строка подключения</param>
        /// <param name="objectName">Имя объекта (может быть: MyView, dbo.MyView, [dbo].[MyView])</param>
        /// <returns>Каноническое имя [schema].[name] или null если объект не найден</returns>
        public static string GetCanonicalObjectName(string connectionString, string objectName)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString));

            if (string.IsNullOrWhiteSpace(objectName))
                throw new ArgumentNullException(nameof(objectName));

            var sql = @"
        SELECT QUOTENAME(OBJECT_SCHEMA_NAME(OBJECT_ID(@objectName))) + '.' + 
               QUOTENAME(OBJECT_NAME(OBJECT_ID(@objectName))) AS object_fullname";

            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@objectName", objectName);
                connection.Open();

                var result = command.ExecuteScalar();

                return result as string;
            }
        }
    }
}