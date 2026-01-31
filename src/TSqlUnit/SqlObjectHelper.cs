using System;
using Microsoft.Data.SqlClient;

namespace TSqlUnit
{
    /// <summary>
    /// Вспомогательный класс для работы с SQL объектами
    /// </summary>
    public class SqlObjectHelper
    {
        private readonly string _connectionString;
        
        public SqlObjectHelper(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }
        
        /// <summary>
        /// Получает каноническое имя объекта в формате [schema].[name]
        /// </summary>
        public string GetCanonicalName(string objectName)
        {
            return Core.GetCanonicalObjectName(_connectionString, objectName);
        }
        
        /// <summary>
        /// Выполняет SQL команду
        /// </summary>
        public void ExecuteSql(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
                throw new ArgumentNullException(nameof(sql));
            
            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                connection.Open();
                command.ExecuteNonQuery();
            }
        }
        
        /// <summary>
        /// Удаляет объект из базы данных
        /// </summary>
        /// <param name="objectName">Имя объекта (только имя, без схемы)</param>
        /// <param name="objectType">Тип объекта (TABLE, VIEW, PROCEDURE, FUNCTION)</param>
        public void DropObject(string objectName, string objectType)
        {
            if (string.IsNullOrWhiteSpace(objectName))
                throw new ArgumentNullException(nameof(objectName));
            
            if (string.IsNullOrWhiteSpace(objectType))
                throw new ArgumentNullException(nameof(objectType));
            
            var sql = string.Format(
                "DROP {0} IF EXISTS [dbo].[{1}]", 
                objectType, 
                objectName
            );
            
            try
            {
                ExecuteSql(sql);
            }
            catch
            {
                // Игнорируем ошибки при удалении (объект может не существовать)
            }
        }
    }
}
