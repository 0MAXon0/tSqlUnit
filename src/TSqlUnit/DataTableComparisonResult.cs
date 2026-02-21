namespace TSqlUnit
{
    /// <summary>
    /// Результат сравнения DataTable
    /// </summary>
    public class DataTableComparisonResult
    {
        /// <summary>
        /// Таблицы идентичны
        /// </summary>
        public bool IsEqual { get; set; }

        /// <summary>
        /// Сообщение о различиях (или причина ошибки)
        /// </summary>
        public string DiffMessage { get; set; }
    }
}
