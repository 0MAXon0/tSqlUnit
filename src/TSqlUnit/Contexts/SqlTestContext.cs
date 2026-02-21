namespace TSqlUnit.Contexts;

/// <summary>
/// Контекст для настройки и выполнения unit-тестов SQL-процедуры с подменой зависимостей.
/// </summary>
public class SqlTestContext : IDisposable
{
    private readonly string _connectionString;
    private readonly List<FakeDependency> _fakes = new();
    private readonly List<string> _setupSqlScripts = new();
    
    private string _targetProcedure;
    private string _canonicalProcedureName;
    private string _testProcedureName;
    private bool _isBuilt = false;

    /// <summary>
    /// Создает тестовый контекст для указанной базы данных.
    /// </summary>
    /// <param name="connectionString">Строка подключения к SQL Server.</param>
    public SqlTestContext(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    /// <summary>
    /// Указывает процедуру для тестирования.
    /// </summary>
    /// <param name="procedureName">Имя процедуры (может быть в любом формате: MyProc, dbo.MyProc, [dbo].[MyProc])</param>
    /// <returns>Текущий контекст для цепочки вызовов</returns>
    public SqlTestContext ForProcedure(string procedureName)
    {
        if (_isBuilt)
            throw new InvalidOperationException("Нельзя изменять контекст после вызова Build().");
        
        if (string.IsNullOrWhiteSpace(procedureName))
            throw new ArgumentNullException(nameof(procedureName));
        
        _targetProcedure = procedureName;
        return this;
    }
    
    /// <summary>
    /// Добавляет fake-функцию.
    /// </summary>
    /// <param name="functionName">Имя функции для подмены.</param>
    /// <param name="fakeDefinition">Скрипт <c>CREATE FUNCTION</c> для fake-функции.</param>
    /// <returns>Текущий контекст для цепочки вызовов.</returns>
    public SqlTestContext MockFunction(string functionName, string fakeDefinition)
    {
        if (_isBuilt)
            throw new InvalidOperationException("Нельзя изменять контекст после вызова Build().");
        
        if (string.IsNullOrWhiteSpace(functionName))
            throw new ArgumentNullException(nameof(functionName));
        
        if (string.IsNullOrWhiteSpace(fakeDefinition))
            throw new ArgumentNullException(nameof(fakeDefinition));
        
        _fakes.Add(new FakeDependency
        {
            OriginalName = functionName,
            ObjectType = ObjectType.Function,
            FakeDefinition = fakeDefinition
        });
        
        return this;
    }

    /// <summary>
    /// Добавляет fake-представление.
    /// </summary>
    /// <param name="viewName">Имя представления для подмены.</param>
    /// <param name="fakeDefinition">Скрипт <c>CREATE VIEW</c> для fake-представления.</param>
    /// <returns>Текущий контекст для цепочки вызовов.</returns>
    public SqlTestContext MockView(string viewName, string fakeDefinition)
    {
        if (_isBuilt)
            throw new InvalidOperationException("Нельзя изменять контекст после вызова Build().");

        if (string.IsNullOrWhiteSpace(viewName))
            throw new ArgumentNullException(nameof(viewName));

        if (string.IsNullOrWhiteSpace(fakeDefinition))
            throw new ArgumentNullException(nameof(fakeDefinition));

        _fakes.Add(new FakeDependency
        {
            OriginalName = viewName,
            ObjectType = ObjectType.View,
            FakeDefinition = fakeDefinition
        });

        return this;
    }

    /// <summary>
    /// Добавляет fake-процедуру с логированием входных параметров в spy-таблицу.
    /// </summary>
    /// <param name="procedureName">Имя процедуры для подмены.</param>
    /// <param name="customSqlAfterSpyInsert">
    /// Дополнительный SQL, который выполняется сразу после <c>INSERT</c> в spy-таблицу.
    /// </param>
    /// <returns>Текущий контекст для цепочки вызовов.</returns>
    public SqlTestContext MockProcedure(string procedureName, string customSqlAfterSpyInsert = null)
    {
        if (_isBuilt)
            throw new InvalidOperationException("Нельзя изменять контекст после вызова Build().");

        if (string.IsNullOrWhiteSpace(procedureName))
            throw new ArgumentNullException(nameof(procedureName));

        var canonicalProcedureName = SqlMetadataReader.GetCanonicalName(_connectionString, procedureName) ?? throw new InvalidOperationException(
                string.Format("Процедура '{0}' не найдена.", procedureName));
        var templateInfo = SqlMetadataReader.GetFakeProcedureTemplateInfo(_connectionString, canonicalProcedureName) ?? throw new InvalidOperationException(
                string.Format("Не удалось получить шаблон fake-процедуры для '{0}'.", canonicalProcedureName));
        var spyLogTableName = TestObjectNameGenerator.Generate(
            canonicalProcedureName + "_SpyProcedureLog",
            ObjectType.Table
        );

        var fakeProcedureDefinition = BuildFakeProcedureDefinition(
            canonicalProcedureName,
            spyLogTableName,
            templateInfo,
            customSqlAfterSpyInsert
        );

        _fakes.Add(new FakeDependency
        {
            OriginalName = procedureName,
            ObjectType = ObjectType.StoredProcedure,
            FakeDefinition = fakeProcedureDefinition,
            SpyLogTableName = spyLogTableName,
            SpyLogTableDefinition = BuildSpyLogTableDefinition(spyLogTableName, templateInfo.ColumnsList)
        });

        return this;
    }

    /// <summary>
    /// Добавляет fake-таблицу на основе метаданных исходной таблицы.
    /// </summary>
    /// <param name="tableName">Имя таблицы для подмены.</param>
    /// <param name="options">Опции генерации скрипта <c>CREATE TABLE</c>.</param>
    /// <returns>Текущий контекст для цепочки вызовов.</returns>
    public SqlTestContext MockTable(string tableName, TableDefinitionOptions options = null)
    {
        if (_isBuilt)
            throw new InvalidOperationException("Нельзя изменять контекст после вызова Build().");

        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentNullException(nameof(tableName));

        var tableDefinition = SqlMetadataReader.GetTableDefinition(_connectionString, tableName, options);
        if (string.IsNullOrWhiteSpace(tableDefinition))
            throw new InvalidOperationException(
                string.Format("Не удалось получить определение таблицы '{0}'.", tableName));

        _fakes.Add(new FakeDependency
        {
            OriginalName = tableName,
            ObjectType = ObjectType.Table,
            FakeDefinition = tableDefinition
        });

        return this;
    }

    /// <summary>
    /// Добавляет SQL, который выполняется перед каждым <see cref="Execute"/> и <see cref="ExecuteWithResult"/>.
    /// </summary>
    /// <param name="setupSql">SQL-скрипт подготовки данных.</param>
    /// <returns>Текущий контекст для цепочки вызовов.</returns>
    public SqlTestContext SetupSql(string setupSql)
    {
        if (string.IsNullOrWhiteSpace(setupSql))
            throw new ArgumentNullException(nameof(setupSql));

        _setupSqlScripts.Add(setupSql);
        return this;
    }

    /// <summary>
    /// Добавляет вызов setup-процедуры, который выполняется перед каждым <see cref="Execute"/> и <see cref="ExecuteWithResult"/>.
    /// </summary>
    /// <param name="procedureName">Имя процедуры подготовки.</param>
    /// <returns>Текущий контекст для цепочки вызовов.</returns>
    public SqlTestContext SetupProcedure(string procedureName)
    {
        if (string.IsNullOrWhiteSpace(procedureName))
            throw new ArgumentNullException(nameof(procedureName));

        var canonicalProcedureName = SqlMetadataReader.GetCanonicalName(_connectionString, procedureName) ?? throw new InvalidOperationException(
                string.Format("Процедура '{0}' не найдена.", procedureName));
        _setupSqlScripts.Add(string.Format("EXEC {0};", canonicalProcedureName));
        return this;
    }

    /// <summary>
    /// Создает все временные объекты в базе данных.
    /// </summary>
    /// <returns>Текущий контекст для цепочки вызовов.</returns>
    public SqlTestContext Build()
    {
        if (_isBuilt)
            throw new InvalidOperationException("Build() уже был вызван.");
        
        if (string.IsNullOrWhiteSpace(_targetProcedure))
            throw new InvalidOperationException("Целевая процедура не указана. Сначала вызовите ForProcedure().");

        _canonicalProcedureName = SqlMetadataReader.GetCanonicalName(_connectionString, _targetProcedure);
        if (_canonicalProcedureName == null)
            throw new InvalidOperationException(
                string.Format("Процедура '{0}' не найдена.", _targetProcedure));

        var procedureDefinition = SqlMetadataReader.GetObjectDefinition(_connectionString, _canonicalProcedureName) ?? throw new InvalidOperationException(
                string.Format("Не удалось получить определение процедуры '{0}'.", _canonicalProcedureName));
        var modifiedProcedureDefinition = procedureDefinition;

        foreach (var fake in _fakes)
        {
            fake.CanonicalName = SqlMetadataReader.GetCanonicalName(_connectionString, fake.OriginalName);
            if (fake.CanonicalName == null)
                throw new InvalidOperationException(
                    string.Format("{0} '{1}' не найден(а).", GetObjectDisplayName(fake.ObjectType), fake.OriginalName));
        }

        foreach (var fake in _fakes)
        {
            // Last fake wins: поздняя подмена для того же объекта приоритетнее.
            if (IsOverriddenByLaterFake(fake))
                continue;

            fake.FakeName = TestObjectNameGenerator.Generate(fake.CanonicalName, fake.ObjectType);

            var fakeFullName = string.Format("[dbo].[{0}]", fake.FakeName);
            fake.FakeDefinitionRenamed = SqlScriptModifier.ReplaceObjectName(
                fake.FakeDefinition,
                fake.CanonicalName,
                fakeFullName
            );

            modifiedProcedureDefinition = SqlScriptModifier.ReplaceObjectName(
                modifiedProcedureDefinition,
                fake.CanonicalName,
                fakeFullName
            );

            if (fake.ObjectType == ObjectType.StoredProcedure &&
                !string.IsNullOrWhiteSpace(fake.SpyLogTableDefinition))
            {
                ExecuteNonQuery(fake.SpyLogTableDefinition);
            }

            ExecuteNonQuery(fake.FakeDefinitionRenamed);
        }

        _testProcedureName = TestObjectNameGenerator.Generate(
            _canonicalProcedureName, 
            ObjectType.StoredProcedure
        );

        var testProcedureDefinition = SqlScriptModifier.ReplaceObjectName(
            modifiedProcedureDefinition,
            _canonicalProcedureName,
            string.Format("[dbo].[{0}]", _testProcedureName)
        );

        ExecuteNonQuery(testProcedureDefinition);

        _isBuilt = true;
        return this;
    }

    /// <summary>
    /// Выполняет тестовую процедуру без чтения результирующих наборов.
    /// </summary>
    /// <param name="parameters">Параметры для процедуры.</param>
    /// <returns>Текущий контекст для цепочки вызовов.</returns>
    public SqlTestContext Execute(params SqlParameter[] parameters)
    {
        if (!_isBuilt)
            throw new InvalidOperationException("Вызовите Build() перед Execute().");

        RunSetupScripts();

        var sql = BuildProcedureExecutionSql(parameters, includeReturnValue: false);

        using (var connection = new SqlConnection(_connectionString))
        using (var command = new SqlCommand(sql, connection))
        {
            if (parameters != null)
            {
                command.Parameters.AddRange(parameters);
            }
            
            connection.Open();
            command.ExecuteNonQuery();
        }

        return this;
    }

    /// <summary>
    /// Выполняет тестовую процедуру и возвращает результирующие наборы, OUT-параметры и RETURN-значение.
    /// </summary>
    /// <param name="parameters">Параметры для процедуры.</param>
    /// <returns>Результат выполнения процедуры.</returns>
    public SqlTestResult ExecuteWithResult(params SqlParameter[] parameters)
    {
        if (!_isBuilt)
            throw new InvalidOperationException("Вызовите Build() перед ExecuteWithResult().");

        RunSetupScripts();

        var connection = new SqlConnection(_connectionString);
        connection.Open();

        var sql = BuildProcedureExecutionSql(parameters, includeReturnValue: true);

        var command = new SqlCommand(sql, connection)
        {
            CommandType = System.Data.CommandType.Text
        };

        var returnParam = new SqlParameter("@RETURN_VALUE", System.Data.SqlDbType.Int)
        {
            Direction = System.Data.ParameterDirection.Output
        };
        command.Parameters.Add(returnParam);

        if (parameters != null)
        {
            command.Parameters.AddRange(parameters);
        }

        var result = new SqlTestResult(command);

        connection.Close();
        connection.Dispose();

        return result;
    }

    /// <summary>
    /// Выполняет произвольный SQL без возврата результирующих наборов.
    /// </summary>
    /// <param name="sql">SQL-скрипт или команда.</param>
    /// <param name="parameters">Параметры SQL-команды.</param>
    /// <returns>Количество затронутых строк.</returns>
    public int ExecuteNonQuery(string sql, params SqlParameter[] parameters)
    {
        if (string.IsNullOrWhiteSpace(sql))
            throw new ArgumentNullException(nameof(sql));

        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand(sql, connection);
        if (parameters != null && parameters.Length > 0)
        {
            command.Parameters.AddRange(parameters);
        }

        connection.Open();
        return command.ExecuteNonQuery();
    }

    /// <summary>
    /// Возвращает логи вызовов fake-процедуры из spy-таблицы.
    /// </summary>
    /// <param name="procedureName">Имя процедуры, переданной в <see cref="MockProcedure"/>.</param>
    /// <returns>Таблица с логами вызовов.</returns>
    public DataTable GetSpyProcedureLog(string procedureName)
    {
        if (!_isBuilt)
            throw new InvalidOperationException("Вызовите Build() перед GetSpyProcedureLog().");

        if (string.IsNullOrWhiteSpace(procedureName))
            throw new ArgumentNullException(nameof(procedureName));

        var canonicalProcedureName = SqlMetadataReader.GetCanonicalName(_connectionString, procedureName) ?? throw new InvalidOperationException(
                string.Format("Процедура '{0}' не найдена.", procedureName));
        FakeDependency procedureFake = null;
        for (var i = _fakes.Count - 1; i >= 0; i--)
        {
            var fake = _fakes[i];
            if (fake.ObjectType != ObjectType.StoredProcedure)
                continue;

            var matchesCanonical = !string.IsNullOrWhiteSpace(fake.CanonicalName) &&
                                   fake.CanonicalName.Equals(canonicalProcedureName, StringComparison.OrdinalIgnoreCase);
            var matchesOriginal = fake.OriginalName.Equals(procedureName, StringComparison.OrdinalIgnoreCase);

            if (matchesCanonical || matchesOriginal)
            {
                procedureFake = fake;
                break;
            }
        }

        if (procedureFake == null || string.IsNullOrWhiteSpace(procedureFake.SpyLogTableName))
            throw new InvalidOperationException(
                string.Format("Лог вызовов для fake-процедуры '{0}' не найден. Вызовите MockProcedure() перед Build().", procedureName));

        var sql = string.Format(
            "SELECT * FROM [dbo].[{0}] ORDER BY [_id_];",
            procedureFake.SpyLogTableName
        );

        return ExecuteQuery(sql);
    }

    /// <summary>
    /// Удаляет созданные временные объекты.
    /// </summary>
    public void Cleanup()
    {
        if (!_isBuilt)
            return;
        
        try
        {
            if (!string.IsNullOrEmpty(_testProcedureName))
            {
                DropObject(_testProcedureName, "PROCEDURE");
            }
            
            foreach (var fake in _fakes)
            {
                if (!string.IsNullOrEmpty(fake.FakeName))
                {
                    DropObject(fake.FakeName, GetDropObjectType(fake.ObjectType));
                }

                if (fake.ObjectType == ObjectType.StoredProcedure &&
                    !string.IsNullOrEmpty(fake.SpyLogTableName))
                {
                    DropObject(fake.SpyLogTableName, GetDropObjectType(ObjectType.Table));
                }
            }
        }
        catch
        {
        }
    }

    /// <summary>
    /// Освобождает ресурсы контекста и выполняет <see cref="Cleanup"/>.
    /// </summary>
    public void Dispose()
    {
        Cleanup();
    }

    /// <summary>
    /// Возвращает имя созданного fake-объекта.
    /// </summary>
    /// <param name="objectType">Тип SQL-объекта.</param>
    /// <param name="objectName">Original или canonical имя объекта.</param>
    /// <returns>Имя fake-объекта без схемы.</returns>
    public string GetFakeName(ObjectType objectType, string objectName)
    {
        var fake = FindFake(objectType, objectName);
        if (string.IsNullOrWhiteSpace(fake.FakeName))
        {
            throw new InvalidOperationException(
                "Имя fake-объекта еще не сгенерировано. Вызовите Build() перед запросом имен.");
        }

        return fake.FakeName;
    }

    private string BuildProcedureExecutionSql(SqlParameter[] parameters, bool includeReturnValue)
    {
        var sql = includeReturnValue
            ? string.Format("EXEC @RETURN_VALUE = [dbo].[{0}]", _testProcedureName)
            : string.Format("EXEC [dbo].[{0}]", _testProcedureName);

        if (parameters == null || parameters.Length == 0)
            return sql;

        var assignments = new string[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            if (parameter == null || string.IsNullOrWhiteSpace(parameter.ParameterName))
                throw new ArgumentException("Каждый параметр должен иметь непустое имя.", nameof(parameters));

            var parameterName = parameter.ParameterName;
            if (!parameterName.StartsWith("@", StringComparison.Ordinal))
                parameterName = "@" + parameterName;

            var isOutput = parameter.Direction == ParameterDirection.Output ||
                           parameter.Direction == ParameterDirection.InputOutput;
            assignments[i] = isOutput
                ? string.Format("{0} = {0} OUTPUT", parameterName)
                : string.Format("{0} = {0}", parameterName);
        }

        return sql + " " + string.Join(", ", assignments);
    }

    private void RunSetupScripts()
    {
        foreach (var setupSql in _setupSqlScripts)
        {
            ExecuteNonQuery(setupSql);
        }
    }

    private bool IsOverriddenByLaterFake(FakeDependency currentFake)
    {
        if (currentFake == null || string.IsNullOrWhiteSpace(currentFake.CanonicalName))
            return false;

        for (var i = _fakes.Count - 1; i >= 0; i--)
        {
            var candidate = _fakes[i];
            if (ReferenceEquals(candidate, currentFake))
                return false;

            if (candidate.ObjectType == currentFake.ObjectType &&
                !string.IsNullOrWhiteSpace(candidate.CanonicalName) &&
                candidate.CanonicalName.Equals(currentFake.CanonicalName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Выполняет произвольный SQL и возвращает первый результирующий набор.
    /// </summary>
    /// <param name="sql">SQL-скрипт или команда.</param>
    /// <param name="parameters">Параметры SQL-команды.</param>
    /// <returns>Первый результирующий набор.</returns>
    public DataTable ExecuteQuery(string sql, params SqlParameter[] parameters)
    {
        if (string.IsNullOrWhiteSpace(sql))
            throw new ArgumentNullException(nameof(sql));

        var result = new DataTable();
        using (var connection = new SqlConnection(_connectionString))
        using (var command = new SqlCommand(sql, connection))
        using (var adapter = new SqlDataAdapter(command))
        {
            if (parameters != null && parameters.Length > 0)
            {
                command.Parameters.AddRange(parameters);
            }

            connection.Open();
            adapter.Fill(result);
        }

        return result;
    }

    private FakeDependency FindFake(ObjectType objectType, string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            throw new ArgumentNullException(nameof(objectName));

        for (var i = _fakes.Count - 1; i >= 0; i--)
        {
            var item = _fakes[i];
            if (item.ObjectType != objectType)
                continue;

            if (item.OriginalName.Equals(objectName, StringComparison.OrdinalIgnoreCase))
                return item;

            if (!string.IsNullOrWhiteSpace(item.CanonicalName) &&
                item.CanonicalName.Equals(objectName, StringComparison.OrdinalIgnoreCase))
                return item;
        }

        throw new InvalidOperationException(
            string.Format("Fake-объект не найден: тип={0}, имя={1}.", objectType, objectName));
    }

    private void DropObject(string objectName, string objectType)
    {
        var sql = string.Format("DROP {0} IF EXISTS [dbo].[{1}]", objectType, objectName);
        try
        {
            ExecuteNonQuery(sql);
        }
        catch
        {
        }
    }

    private static string BuildSpyLogTableDefinition(string spyLogTableName, string columnsList)
    {
        if (string.IsNullOrWhiteSpace(spyLogTableName))
            throw new ArgumentNullException(nameof(spyLogTableName));

        var effectiveColumns = string.IsNullOrWhiteSpace(columnsList)
            ? "_id_ INT IDENTITY(1,1) PRIMARY KEY CLUSTERED"
            : columnsList;

        return string.Format(
@"CREATE TABLE [dbo].[{0}]
(
{1}
);",
            spyLogTableName,
            effectiveColumns
        );
    }

    private static string BuildFakeProcedureDefinition(
        string canonicalProcedureName,
        string spyLogTableName,
        FakeProcedureTemplateInfo templateInfo,
        string customSqlAfterSpyInsert)
    {
        if (string.IsNullOrWhiteSpace(canonicalProcedureName))
            throw new ArgumentNullException(nameof(canonicalProcedureName));
        if (string.IsNullOrWhiteSpace(spyLogTableName))
            throw new ArgumentNullException(nameof(spyLogTableName));
        if (templateInfo == null)
            throw new ArgumentNullException(nameof(templateInfo));

        var parametersClause = string.IsNullOrWhiteSpace(templateInfo.ParametersList)
            ? string.Empty
            : "\n" + templateInfo.ParametersList;

        var insertStatement = string.IsNullOrWhiteSpace(templateInfo.InsertList) ||
                              string.IsNullOrWhiteSpace(templateInfo.SelectList)
            ? string.Format("    INSERT INTO [dbo].[{0}] DEFAULT VALUES;", spyLogTableName)
            : string.Format(
@"    INSERT INTO [dbo].[{0}] ({1})
    SELECT {2};",
                spyLogTableName,
                templateInfo.InsertList,
                templateInfo.SelectList
            );

        var procedureBody = insertStatement;
        if (!string.IsNullOrWhiteSpace(customSqlAfterSpyInsert))
        {
            procedureBody = procedureBody + "\n\n" + customSqlAfterSpyInsert;
        }

        return string.Format(
@"CREATE PROCEDURE {0}{1}
AS
BEGIN
    SET NOCOUNT ON;

{2}
END;",
            canonicalProcedureName,
            parametersClause,
            procedureBody
        );
    }

    private static string GetObjectDisplayName(ObjectType objectType)
    {
        return objectType switch
        {
            ObjectType.StoredProcedure => "Процедура",
            ObjectType.Function => "Функция",
            ObjectType.Table => "Таблица",
            ObjectType.View => "Представление",
            ObjectType.Trigger => "Триггер",
            _ => throw new ArgumentOutOfRangeException(nameof(objectType), objectType, "Неизвестный тип SQL-объекта."),
        };
    }

    private static string GetDropObjectType(ObjectType objectType)
    {
        return objectType switch
        {
            ObjectType.StoredProcedure => "PROCEDURE",
            ObjectType.Function => "FUNCTION",
            ObjectType.Table => "TABLE",
            ObjectType.View => "VIEW",
            ObjectType.Trigger => "TRIGGER",
            _ => throw new ArgumentOutOfRangeException(nameof(objectType), objectType, "Неизвестный тип SQL-объекта."),
        };
    }
}

