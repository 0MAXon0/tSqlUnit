namespace TSqlUnit.Comparison;

/// <summary>
/// Результат сравнения таблиц <see cref="DataTable"/>.
/// </summary>
public class DataTableComparisonResult
{
    /// <summary>
    /// Таблицы идентичны.
    /// </summary>
    public bool IsEqual { get; internal set; }

    /// <summary>
    /// Сообщение о различиях (или причина ошибки).
    /// </summary>
    public string DiffMessage { get; internal set; }

    /// <summary>
    /// Табличное представление различий (колонка <c>_m_</c>: <c>&lt;</c>, <c>&gt;</c>, <c>=</c>).
    /// </summary>
    public DataTable DiffTable { get; internal set; }
}

