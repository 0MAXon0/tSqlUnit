namespace TSqlUnit
{
    /// <summary>
    /// Шаблонные части для генерации fake процедуры и spy-таблицы
    /// </summary>
    public class FakeProcedureTemplateInfo
    {
        public string ParametersList { get; set; }

        public string ColumnsList { get; set; }

        public string InsertList { get; set; }

        public string SelectList { get; set; }
    }
}
