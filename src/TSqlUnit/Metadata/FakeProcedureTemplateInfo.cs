namespace TSqlUnit.Metadata;

/// <summary>
/// Шаблонные части для генерации fake процедуры и spy-таблицы
/// </summary>
internal class FakeProcedureTemplateInfo
{
    /// <summary>
    /// Список параметров процедуры для секции <c>CREATE PROCEDURE</c>.
    /// </summary>
    public string ParametersList { get; set; }

    /// <summary>
    /// Список колонок spy-таблицы.
    /// </summary>
    public string ColumnsList { get; set; }

    /// <summary>
    /// Список колонок для <c>INSERT</c> в spy-таблицу.
    /// </summary>
    public string InsertList { get; set; }

    /// <summary>
    /// Список выражений для <c>SELECT</c> при вставке в spy-таблицу.
    /// </summary>
    public string SelectList { get; set; }
}
