using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;

namespace TSqlUnit
{
    /// <summary>
    /// Результат выполнения тестовой процедуры
    /// </summary>
    public class SqlTestResult : IDisposable
    {
        private readonly SqlDataReader _reader;
        private bool _disposed = false;

        internal SqlTestResult(SqlCommand command)
        {
            Command = command ?? throw new ArgumentNullException(nameof(command));
            
            // Выполняем команду
            _reader = command.ExecuteReader();
            
            // Читаем все результирующие наборы в память
            ResultSets = new List<DataTable>();
            do
            {
                var table = new DataTable();
                table.Load(_reader);
                ResultSets.Add(table);
            }
            while (!_reader.IsClosed);
        }

        /// <summary>
        /// SqlCommand для доступа к параметрам
        /// </summary>
        internal SqlCommand Command { get; }

        /// <summary>
        /// Все результирующие наборы данных (SELECT-ы)
        /// </summary>
        public List<DataTable> ResultSets { get; }

        /// <summary>
        /// Возвращаемое значение процедуры (RETURN)
        /// </summary>
        public int? ReturnValue
        {
            get
            {
                var returnParam = Command.Parameters["@RETURN_VALUE"];
                if (returnParam != null && returnParam.Value != DBNull.Value)
                {
                    return (int)returnParam.Value;
                }
                return null;
            }
        }

        /// <summary>
        /// Получает значение OUT параметра по имени
        /// </summary>
        public T GetOutParameter<T>(string parameterName)
        {
            if (string.IsNullOrWhiteSpace(parameterName))
                throw new ArgumentNullException(nameof(parameterName));

            if (!parameterName.StartsWith("@"))
                parameterName = "@" + parameterName;

            var param = Command.Parameters[parameterName];
            if (param == null)
                throw new InvalidOperationException(
                    string.Format("Parameter '{0}' not found", parameterName));

            if (param.Direction != ParameterDirection.Output && 
                param.Direction != ParameterDirection.InputOutput)
                throw new InvalidOperationException(
                    string.Format("Parameter '{0}' is not an OUTPUT parameter", parameterName));

            if (param.Value == DBNull.Value)
                return default(T);

            return (T)param.Value;
        }

        /// <summary>
        /// Получает первый результирующий набор данных (первый SELECT)
        /// </summary>
        public DataTable GetFirstResultSet()
        {
            if (ResultSets.Count == 0)
                return null;
            return ResultSets[0];
        }

        /// <summary>
        /// Получает результирующий набор данных по индексу
        /// </summary>
        public DataTable GetResultSet(int index)
        {
            if (index < 0 || index >= ResultSets.Count)
                throw new ArgumentOutOfRangeException(nameof(index), 
                    string.Format("Result set index {0} is out of range (0-{1})", index, ResultSets.Count - 1));
            
            return ResultSets[index];
        }

        /// <summary>
        /// Получает скалярное значение из первого результирующего набора
        /// </summary>
        public T GetScalar<T>()
        {
            return GetScalar<T>(0);
        }

        /// <summary>
        /// Получает скалярное значение из результирующего набора по индексу
        /// </summary>
        public T GetScalar<T>(int resultSetIndex)
        {
            var table = GetResultSet(resultSetIndex);
            
            if (table == null || table.Rows.Count == 0 || table.Columns.Count == 0)
                return default(T);

            var value = table.Rows[0][0];
            
            if (value == DBNull.Value)
                return default(T);

            return (T)value;
        }

        /// <summary>
        /// Получает скалярное значение из результирующего набора по индексу и имени колонки
        /// </summary>
        public T GetScalar<T>(int resultSetIndex, string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName))
                throw new ArgumentNullException(nameof(columnName));

            var table = GetResultSet(resultSetIndex);
            
            if (table == null || table.Rows.Count == 0)
                return default(T);

            if (!table.Columns.Contains(columnName))
                throw new InvalidOperationException(
                    string.Format("Column '{0}' not found in result set", columnName));

            var value = table.Rows[0][columnName];
            
            if (value == DBNull.Value)
                return default(T);

            return (T)value;
        }

        /// <summary>
        /// Маппит первый результирующий набор в список объектов
        /// </summary>
        public List<T> MapToList<T>(Func<DataRow, T> mapper)
        {
            return MapToList<T>(0, mapper);
        }

        /// <summary>
        /// Маппит результирующий набор по индексу в список объектов
        /// </summary>
        public List<T> MapToList<T>(int resultSetIndex, Func<DataRow, T> mapper)
        {
            if (mapper == null)
                throw new ArgumentNullException(nameof(mapper));

            var table = GetResultSet(resultSetIndex);
            var result = new List<T>();

            if (table == null)
                return result;

            foreach (DataRow row in table.Rows)
            {
                result.Add(mapper(row));
            }

            return result;
        }

        /// <summary>
        /// Маппит первую строку первого результирующего набора в объект
        /// </summary>
        public T MapToObject<T>(Func<DataRow, T> mapper)
        {
            return MapToObject<T>(0, mapper);
        }

        /// <summary>
        /// Маппит первую строку результирующего набора по индексу в объект
        /// </summary>
        public T MapToObject<T>(int resultSetIndex, Func<DataRow, T> mapper)
        {
            if (mapper == null)
                throw new ArgumentNullException(nameof(mapper));

            var table = GetResultSet(resultSetIndex);
            
            if (table == null || table.Rows.Count == 0)
                return default(T);

            return mapper(table.Rows[0]);
        }

        /// <summary>
        /// Форматирует первый результирующий набор в текстовую таблицу
        /// </summary>
        public string GetFirstResultSetAsText(int maxRows = 200, int maxCellLength = 120)
        {
            var table = GetFirstResultSet();
            return DataTableComparer.FormatAsTextTable(table, maxRows, maxCellLength);
        }

        /// <summary>
        /// Форматирует результирующий набор по индексу в текстовую таблицу
        /// </summary>
        public string GetResultSetAsText(int resultSetIndex, int maxRows = 200, int maxCellLength = 120)
        {
            var table = GetResultSet(resultSetIndex);
            return DataTableComparer.FormatAsTextTable(table, maxRows, maxCellLength);
        }

        /// <summary>
        /// Освобождает ресурсы
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            if (_reader != null && !_reader.IsClosed)
            {
                _reader.Close();
                _reader.Dispose();
            }

            _disposed = true;
        }
    }
}
