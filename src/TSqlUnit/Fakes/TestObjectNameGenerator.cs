namespace TSqlUnit.Fakes;

/// <summary>
/// Генератор уникальных имен для тестовых SQL объектов
/// </summary>
internal static class TestObjectNameGenerator
{
    /// <summary>
    /// Генерирует уникальное имя временного SQL-объекта.
    /// </summary>
    /// <param name="originalName">Оригинальное имя объекта</param>
    /// <param name="objectType">Тип SQL объекта</param>
    /// <returns>Уникальное имя для временного объекта (только имя, без схемы)</returns>
    public static string Generate(string originalName, ObjectType objectType)
    {
        if (string.IsNullOrWhiteSpace(originalName))
            throw new ArgumentNullException(nameof(originalName));
        
        var parts = originalName.Split('.');
        var objectName = parts[parts.Length - 1].Replace("[", "").Replace("]", "");

        var prefix = GetPrefix(objectType);

        var guid = Guid.NewGuid().ToString("N");
        var id = guid.Substring(0, 6);

        return string.Format("{0}_{1}_{2}", prefix, objectName, id);
    }
    
    private static string GetPrefix(ObjectType objectType)
    {
        return objectType switch
        {
            ObjectType.Table => "TestTable",
            ObjectType.View => "TestView",
            ObjectType.StoredProcedure => "TestProc",
            ObjectType.Function => "TestFunc",
            ObjectType.Trigger => "TestTrigger",
            _ => "TestObj",
        };
    }
}

