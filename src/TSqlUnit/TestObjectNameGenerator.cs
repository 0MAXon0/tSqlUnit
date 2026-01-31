using System;
using System.Linq;

namespace TSqlUnit
{
    /// <summary>
    /// Генератор уникальных имен для тестовых SQL объектов
    /// </summary>
    public static class TestObjectNameGenerator
    {
        /// <summary>
        /// Генерирует уникальное имя для тестового SQL объекта
        /// </summary>
        /// <param name="originalName">Оригинальное имя объекта</param>
        /// <param name="objectType">Тип SQL объекта</param>
        /// <returns>Уникальное имя для временного объекта (только имя, без схемы)</returns>
        public static string Generate(string originalName, ObjectType objectType)
        {
            if (string.IsNullOrWhiteSpace(originalName))
                throw new ArgumentNullException(nameof(originalName));
            
            // Убираем схему, если есть (dbo.Users -> Users)
            var parts = originalName.Split('.');
            var objectName = parts[parts.Length - 1].Replace("[", "").Replace("]", "");
            
            // Получаем префикс в зависимости от типа
            var prefix = GetPrefix(objectType);
            
            // Генерируем короткий GUID (6 символов)
            var guid = Guid.NewGuid().ToString("N");
            var id = guid.Substring(0, 6);
            
            // Формат: Test{Type}_{OriginalName}_{id}
            return string.Format("{0}_{1}_{2}", prefix, objectName, id);
        }
        
        private static string GetPrefix(ObjectType objectType)
        {
            switch (objectType)
            {
                case ObjectType.Table:
                    return "TestTable";
                case ObjectType.View:
                    return "TestView";
                case ObjectType.StoredProcedure:
                    return "TestProc";
                case ObjectType.Function:
                    return "TestFunc";
                case ObjectType.Trigger:
                    return "TestTrigger";
                default:
                    return "TestObj";
            }
        }
    }
}
