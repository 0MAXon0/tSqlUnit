using System;
using System.Text.RegularExpressions;

namespace TSqlUnit
{
    /// <summary>
    /// Модифицирует SQL скрипты (замена имен объектов)
    /// </summary>
    public static class SqlScriptModifier
    {
        /// <summary>
        /// Заменяет имя объекта в SQL скрипте.
        /// Логика: для dbo заменяет с/без схемы, для других только с явной схемой.
        /// </summary>
        public static string ReplaceObjectName(string sqlScript, string oldName, string newName)
        {
            if (string.IsNullOrWhiteSpace(sqlScript))
                throw new ArgumentNullException(nameof(sqlScript));
            if (string.IsNullOrWhiteSpace(oldName))
                throw new ArgumentNullException(nameof(oldName));
            if (string.IsNullOrWhiteSpace(newName))
                throw new ArgumentNullException(nameof(newName));

            var (oldSchema, oldObjectName) = ParseName(oldName);
            var (newSchema, newObjectName) = ParseName(newName);

            var result = sqlScript;

            // Всегда заменяем с явной схемой: [schema].[name]
            var patternWithSchema = string.Format(
                @"(?<![.\[\]\w])\[?{0}\]?\.\[?{1}\]?(?![.\[\]\w])",
                Regex.Escape(oldSchema),
                Regex.Escape(oldObjectName)
            );
            var replacement = string.Format("[{0}].[{1}]", newSchema, newObjectName);
            result = Regex.Replace(result, patternWithSchema, replacement, RegexOptions.IgnoreCase);

            // Для dbo также заменяем без схемы (т.к. dbo - схема по умолчанию)
            if (oldSchema.Equals("dbo", StringComparison.OrdinalIgnoreCase))
            {
                var patternWithoutSchema = string.Format(
                    @"(?<![.\[\]\w])\[?{0}\]?(?![.\[\]\w])",
                    Regex.Escape(oldObjectName)
                );
                result = Regex.Replace(result, patternWithoutSchema, replacement, RegexOptions.IgnoreCase);
            }

            return result;
        }

        private static (string schema, string name) ParseName(string fullName)
        {
            var cleaned = fullName.Replace("[", "").Replace("]", "");
            var parts = cleaned.Split('.');

            if (parts.Length == 2)
                return (parts[0], parts[1]);
            
            if (parts.Length == 1)
                return ("dbo", parts[0]);
            
            throw new ArgumentException($"Invalid name format: '{fullName}'. Expected: [schema].[name] or name");
        }
    }
}
