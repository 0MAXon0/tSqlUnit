namespace TSqlUnit
{
    /// <summary>
    /// Информация о подмененной зависимости (fake объекте)
    /// </summary>
    public class FakeDependency
    {
        /// <summary>
        /// Оригинальное имя объекта (как передал пользователь)
        /// </summary>
        public string OriginalName { get; set; }
        
        /// <summary>
        /// Каноническое имя объекта в формате [schema].[name]
        /// </summary>
        public string CanonicalName { get; set; }
        
        /// <summary>
        /// Сгенерированное имя для fake объекта (только имя, без схемы)
        /// </summary>
        public string FakeName { get; set; }
        
        /// <summary>
        /// Тип объекта
        /// </summary>
        public ObjectType ObjectType { get; set; }
        
        /// <summary>
        /// Определение fake объекта (скрипт пользователя)
        /// </summary>
        public string FakeDefinition { get; set; }
        
        /// <summary>
        /// Определение fake объекта с замененным именем (готовый к выполнению)
        /// </summary>
        public string FakeDefinitionRenamed { get; set; }
    }
}
