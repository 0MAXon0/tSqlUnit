namespace TSqlUnit
{
    /// <summary>
    /// Опции для формирования скрипта создания таблицы
    /// </summary>
    public class TableDefinitionOptions
    {
        /// <summary>
        /// Включать вычисляемые столбцы
        /// </summary>
        public bool IncludeComputedColumns { get; set; }

        /// <summary>
        /// Включать ограничение NOT NULL
        /// </summary>
        public bool IncludeNotNull { get; set; }

        /// <summary>
        /// Включать IDENTITY
        /// </summary>
        public bool IncludeIdentity { get; set; }

        /// <summary>
        /// Включать DEFAULT значения
        /// </summary>
        public bool IncludeDefaults { get; set; }

        /// <summary>
        /// Включать PRIMARY KEY
        /// </summary>
        public bool IncludePrimaryKey { get; set; }

        /// <summary>
        /// Включать FOREIGN KEY
        /// </summary>
        public bool IncludeForeignKeys { get; set; }

        /// <summary>
        /// Включать CHECK constraints
        /// </summary>
        public bool IncludeCheckConstraints { get; set; }

        /// <summary>
        /// Включать UNIQUE constraints
        /// </summary>
        public bool IncludeUniqueConstraints { get; set; }

        /// <summary>
        /// Опции по умолчанию (только структура колонок)
        /// </summary>
        public static TableDefinitionOptions Default => new TableDefinitionOptions();

        /// <summary>
        /// Максимальные опции (всё включено)
        /// </summary>
        public static TableDefinitionOptions Maximum => new TableDefinitionOptions
        {
            IncludeComputedColumns = true,
            IncludeNotNull = true,
            IncludeIdentity = true,
            IncludeDefaults = true,
            IncludePrimaryKey = true,
            IncludeForeignKeys = true,
            IncludeCheckConstraints = true,
            IncludeUniqueConstraints = true
        };
    }
}
