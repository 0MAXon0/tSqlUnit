namespace TSqlUnit.Contexts;

/// <summary>
/// Результат выполнения тестовой процедуры.
/// </summary>
public class SqlTestResult : IDisposable
{
    private readonly List<DataTable> _resultSets = new();
    private bool _disposed = false;

    /// <summary>
    /// Инициализирует результат и считывает все результирующие наборы в память.
    /// </summary>
    /// <param name="command">Команда, которой выполнялась тестовая процедура.</param>
    internal SqlTestResult(SqlCommand command)
    {
        Command = command ?? throw new ArgumentNullException(nameof(command));

        using var reader = command.ExecuteReader();
        do
        {
            var table = new DataTable();
            table.Load(reader);
            _resultSets.Add(table);
        }
        while (!reader.IsClosed);
    }

    /// <summary>
    /// Команда, содержащая параметры вызова (включая OUT и RETURN).
    /// </summary>
    internal SqlCommand Command { get; }

    /// <summary>
    /// Все результирующие наборы данных, возвращенные процедурой.
    /// </summary>
    public IReadOnlyList<DataTable> ResultSets => _resultSets;

    /// <summary>
    /// Возвращаемое значение процедуры (оператор <c>RETURN</c>).
    /// </summary>
    public int? ReturnValue
    {
        get
        {
            if (!Command.Parameters.Contains("@RETURN_VALUE"))
                return null;

            var returnParam = Command.Parameters["@RETURN_VALUE"];
            if (returnParam != null && returnParam.Value != DBNull.Value)
            {
                return (int)returnParam.Value;
            }
            return null;
        }
    }

    /// <summary>
    /// Возвращает значение OUT-параметра по имени.
    /// </summary>
    /// <typeparam name="T">Ожидаемый тип значения.</typeparam>
    /// <param name="parameterName">Имя параметра с или без символа <c>@</c>.</param>
    /// <returns>Значение параметра либо <c>default(T)</c>, если значение равно <see cref="DBNull"/>.</returns>
    public T GetOutParameter<T>(string parameterName)
    {
        if (string.IsNullOrWhiteSpace(parameterName))
            throw new ArgumentNullException(nameof(parameterName));

        if (!parameterName.StartsWith("@"))
            parameterName = "@" + parameterName;

        if (!Command.Parameters.Contains(parameterName))
            throw new InvalidOperationException(
                string.Format("Параметр '{0}' не найден.", parameterName));

        var param = Command.Parameters[parameterName];

        if (param.Direction != ParameterDirection.Output && 
            param.Direction != ParameterDirection.InputOutput)
            throw new InvalidOperationException(
                string.Format("Параметр '{0}' не является OUTPUT-параметром.", parameterName));

        if (param.Value == DBNull.Value)
            return default;

        return (T)param.Value;
    }

    /// <summary>
    /// Возвращает первый результирующий набор данных.
    /// </summary>
    /// <returns>Первая таблица результатов или <see langword="null"/>, если результат пуст.</returns>
    public DataTable GetFirstResultSet()
    {
        if (_resultSets.Count == 0)
            return null;
        return _resultSets[0];
    }

    /// <summary>
    /// Возвращает результирующий набор данных по индексу.
    /// </summary>
    /// <param name="index">Индекс результирующего набора, начиная с 0.</param>
    /// <returns>Таблица результатов по указанному индексу.</returns>
    public DataTable GetResultSet(int index)
    {
        if (index < 0 || index >= _resultSets.Count)
            throw new ArgumentOutOfRangeException(nameof(index), 
                string.Format("Индекс результирующего набора {0} вне диапазона (0-{1}).", index, _resultSets.Count - 1));
        
        return _resultSets[index];
    }

    /// <summary>
    /// Возвращает скалярное значение из первого результирующего набора и первой колонки.
    /// </summary>
    /// <typeparam name="T">Ожидаемый тип значения.</typeparam>
    /// <returns>Скалярное значение либо <c>default(T)</c>, если данных нет.</returns>
    public T GetScalar<T>()
    {
        return GetScalar<T>(0);
    }

    /// <summary>
    /// Возвращает скалярное значение из указанного результирующего набора и первой колонки.
    /// </summary>
    /// <typeparam name="T">Ожидаемый тип значения.</typeparam>
    /// <param name="resultSetIndex">Индекс результирующего набора.</param>
    /// <returns>Скалярное значение либо <c>default(T)</c>, если данных нет.</returns>
    public T GetScalar<T>(int resultSetIndex)
    {
        var table = GetResultSet(resultSetIndex);
        
        if (table == null || table.Rows.Count == 0 || table.Columns.Count == 0)
            return default;

        var value = table.Rows[0][0];
        
        if (value == DBNull.Value)
            return default;

        return (T)value;
    }

    /// <summary>
    /// Возвращает скалярное значение из указанного результирующего набора и колонки.
    /// </summary>
    /// <typeparam name="T">Ожидаемый тип значения.</typeparam>
    /// <param name="resultSetIndex">Индекс результирующего набора.</param>
    /// <param name="columnName">Имя колонки.</param>
    /// <returns>Скалярное значение либо <c>default(T)</c>, если данных нет.</returns>
    public T GetScalar<T>(int resultSetIndex, string columnName)
    {
        if (string.IsNullOrWhiteSpace(columnName))
            throw new ArgumentNullException(nameof(columnName));

        var table = GetResultSet(resultSetIndex);
        
        if (table == null || table.Rows.Count == 0)
            return default;

        if (!table.Columns.Contains(columnName))
            throw new InvalidOperationException(
                string.Format("Колонка '{0}' не найдена в результирующем наборе.", columnName));

        var value = table.Rows[0][columnName];
        
        if (value == DBNull.Value)
            return default;

        return (T)value;
    }

    /// <summary>
    /// Преобразует первый результирующий набор в список объектов.
    /// </summary>
    /// <typeparam name="T">Тип элементов результирующего списка.</typeparam>
    /// <param name="mapper">Функция преобразования <see cref="DataRow"/> в <typeparamref name="T"/>.</param>
    /// <returns>Список объектов.</returns>
    public List<T> MapToList<T>(Func<DataRow, T> mapper)
    {
        return MapToList<T>(0, mapper);
    }

    /// <summary>
    /// Преобразует указанный результирующий набор в список объектов.
    /// </summary>
    /// <typeparam name="T">Тип элементов результирующего списка.</typeparam>
    /// <param name="resultSetIndex">Индекс результирующего набора.</param>
    /// <param name="mapper">Функция преобразования <see cref="DataRow"/> в <typeparamref name="T"/>.</param>
    /// <returns>Список объектов.</returns>
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
    /// Преобразует первую строку первого результирующего набора в объект.
    /// </summary>
    /// <typeparam name="T">Тип результата.</typeparam>
    /// <param name="mapper">Функция преобразования <see cref="DataRow"/> в <typeparamref name="T"/>.</param>
    /// <returns>Объект результата либо <c>default(T)</c>, если строк нет.</returns>
    public T MapToObject<T>(Func<DataRow, T> mapper)
    {
        return MapToObject<T>(0, mapper);
    }

    /// <summary>
    /// Преобразует первую строку указанного результирующего набора в объект.
    /// </summary>
    /// <typeparam name="T">Тип результата.</typeparam>
    /// <param name="resultSetIndex">Индекс результирующего набора.</param>
    /// <param name="mapper">Функция преобразования <see cref="DataRow"/> в <typeparamref name="T"/>.</param>
    /// <returns>Объект результата либо <c>default(T)</c>, если строк нет.</returns>
    public T MapToObject<T>(int resultSetIndex, Func<DataRow, T> mapper)
    {
        if (mapper == null)
            throw new ArgumentNullException(nameof(mapper));

        var table = GetResultSet(resultSetIndex);
        
        if (table == null || table.Rows.Count == 0)
            return default;

        return mapper(table.Rows[0]);
    }

    /// <summary>
    /// Возвращает первый результирующий набор в виде текстовой таблицы.
    /// </summary>
    /// <param name="maxRows">Максимум выводимых строк.</param>
    /// <param name="maxCellLength">Максимальная длина значения ячейки.</param>
    /// <returns>Текстовое представление таблицы.</returns>
    public string GetFirstResultSetAsText(int maxRows = 200, int maxCellLength = 120)
    {
        var table = GetFirstResultSet();
        return DataTableComparer.FormatAsTextTable(table, maxRows, maxCellLength);
    }

    /// <summary>
    /// Возвращает результирующий набор по индексу в виде текстовой таблицы.
    /// </summary>
    /// <param name="resultSetIndex">Индекс результирующего набора.</param>
    /// <param name="maxRows">Максимум выводимых строк.</param>
    /// <param name="maxCellLength">Максимальная длина значения ячейки.</param>
    /// <returns>Текстовое представление таблицы.</returns>
    public string GetResultSetAsText(int resultSetIndex, int maxRows = 200, int maxCellLength = 120)
    {
        var table = GetResultSet(resultSetIndex);
        return DataTableComparer.FormatAsTextTable(table, maxRows, maxCellLength);
    }

    /// <summary>
    /// Освобождает ресурсы, связанные с SQL-командой.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        Command.Dispose();
        _disposed = true;
    }
}

