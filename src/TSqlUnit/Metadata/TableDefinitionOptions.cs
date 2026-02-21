namespace TSqlUnit.Metadata;

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
    /// Включать ограничения CHECK
    /// </summary>
    public bool IncludeCheckConstraints { get; set; }

    /// <summary>
    /// Включать ограничения UNIQUE
    /// </summary>
    public bool IncludeUniqueConstraints { get; set; }

    /// <summary>
    /// Опции по умолчанию (только структура колонок)
    /// </summary>
    public static TableDefinitionOptions Default => new();

    /// <summary>
    /// Максимальные опции (всё включено)
    /// </summary>
    public static TableDefinitionOptions Maximum => new()
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
