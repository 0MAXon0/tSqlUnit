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

        /// <summary>
        /// Включать совпавшие строки (маркер '=') в diff-таблицу
        /// </summary>
        public bool IncludeMatchedRowsInDiff { get; set; } = true;

        /// <summary>
        /// Максимальное количество строк в diff-таблице
        /// </summary>
        public int MaxDiffRows { get; set; } = 200;

        /// <summary>
        /// Максимальная длина значения ячейки в текстовом представлении
        /// </summary>
        public int MaxCellLength { get; set; } = 120;
    }
}
