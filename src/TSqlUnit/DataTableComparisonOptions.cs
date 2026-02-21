namespace TSqlUnit
{
    /// <summary>
    /// Опции сравнения DataTable
    /// </summary>
    public class DataTableComparisonOptions
    {
        /// <summary>
        /// Игнорировать порядок колонок
        /// </summary>
        public bool IgnoreColumnOrder { get; set; }

        /// <summary>
        /// Игнорировать порядок строк
        /// </summary>
        public bool IgnoreRowOrder { get; set; }

        /// <summary>
        /// Игнорировать регистр имен колонок
        /// </summary>
        public bool IgnoreColumnNameCase { get; set; } = true;

        /// <summary>
        /// Колонки для сортировки перед сравнением
        /// </summary>
        public string[] SortByColumns { get; set; } = new string[0];
    }
}
