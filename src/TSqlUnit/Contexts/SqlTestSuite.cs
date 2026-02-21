namespace TSqlUnit.Contexts;

/// <summary>
/// Набор тестов с общим setup для каждого создаваемого контекста.
/// </summary>
public class SqlTestSuite
{
    private readonly string _connectionString;
    private readonly List<Action<SqlTestContext>> _setupActions = new();

    /// <summary>
    /// Создает набор тестов с общей строкой подключения.
    /// </summary>
    /// <param name="connectionString">Строка подключения к SQL Server.</param>
    public SqlTestSuite(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    /// <summary>
    /// Регистрирует общее setup-действие, которое применяется к каждому новому контексту.
    /// </summary>
    /// <param name="setupAction">Действие инициализации контекста.</param>
    /// <returns>Текущий экземпляр набора тестов.</returns>
    public SqlTestSuite Setup(Action<SqlTestContext> setupAction)
    {
        if (setupAction == null)
            throw new ArgumentNullException(nameof(setupAction));

        _setupActions.Add(setupAction);
        return this;
    }

    /// <summary>
    /// Создает новый тестовый контекст для процедуры и применяет все зарегистрированные setup-действия.
    /// </summary>
    /// <param name="procedureName">Имя тестируемой процедуры.</param>
    /// <returns>Подготовленный контекст теста.</returns>
    public SqlTestContext ForProcedure(string procedureName)
    {
        var context = new SqlTestContext(_connectionString).ForProcedure(procedureName);
        foreach (var setupAction in _setupActions)
        {
            setupAction(context);
        }

        return context;
    }
}

