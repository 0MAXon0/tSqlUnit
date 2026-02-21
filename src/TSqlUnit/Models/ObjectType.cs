namespace TSqlUnit.Models;

/// <summary>
/// Тип SQL-объекта.
/// </summary>
public enum ObjectType : byte
{
    /// <summary>
    /// Хранимая процедура
    /// </summary>
    StoredProcedure,

    /// <summary>
    /// Функция
    /// </summary>
    Function,

    /// <summary>
    /// Таблица
    /// </summary>
    Table,

    /// <summary>
    /// Представление
    /// </summary>
    View,

    /// <summary>
    /// Триггер
    /// </summary>
    Trigger
}
