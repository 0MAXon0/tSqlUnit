using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;

namespace TSqlUnit
{
    /// <summary>
    /// Контекст для настройки и выполнения unit-теста SQL процедуры с мокированием зависимостей
    /// </summary>
    public class SqlTestContext : IDisposable
    {
        private readonly string _connectionString;
        private readonly List<FakeDependency> _fakes = new List<FakeDependency>();
        private readonly List<string> _setUpSqlScripts = new List<string>();
        
        private string _targetProcedure;
        private string _canonicalProcedureName;
        private string _testProcedureName;
        private bool _isBuilt = false;
        
        public SqlTestContext(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }
        
        /// <summary>
        /// Указывает процедуру для тестирования
        /// </summary>
        /// <param name="procedureName">Имя процедуры (может быть в любом формате: MyProc, dbo.MyProc, [dbo].[MyProc])</param>
        /// <returns>Текущий контекст для цепочки вызовов</returns>
        public SqlTestContext ForProcedure(string procedureName)
        {
            if (_isBuilt)
                throw new InvalidOperationException("Cannot modify context after Build() was called");
            
            if (string.IsNullOrWhiteSpace(procedureName))
                throw new ArgumentNullException(nameof(procedureName));
            
            _targetProcedure = procedureName;
            return this;
        }
        
        /// <summary>
        /// Добавляет мок функции
        /// </summary>
        /// <param name="functionName">Имя функции для мокирования</param>
        /// <param name="fakeDefinition">Скрипт CREATE FUNCTION для подмены</param>
        /// <returns>Текущий контекст для цепочки вызовов</returns>
        public SqlTestContext MockFunction(string functionName, string fakeDefinition)
        {
            if (_isBuilt)
                throw new InvalidOperationException("Cannot modify context after Build() was called");
            
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
        /// Добавляет мок представления
        /// </summary>
        /// <param name="viewName">Имя представления для мокирования</param>
        /// <param name="fakeDefinition">Скрипт CREATE VIEW для подмены</param>
        /// <returns>Текущий контекст для цепочки вызовов</returns>
        public SqlTestContext MockView(string viewName, string fakeDefinition)
        {
            if (_isBuilt)
                throw new InvalidOperationException("Cannot modify context after Build() was called");

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
        /// Добавляет fake процедуру с логированием входных параметров в spy-таблицу
        /// </summary>
        /// <param name="procedureName">Имя процедуры для фейкования</param>
        /// <param name="customSqlAfterSpyInsert">
        /// Дополнительный SQL, который будет выполнен сразу после INSERT в spy-таблицу
        /// </param>
        /// <returns>Текущий контекст для цепочки вызовов</returns>
        public SqlTestContext MockProcedure(string procedureName, string customSqlAfterSpyInsert = null)
        {
            if (_isBuilt)
                throw new InvalidOperationException("Cannot modify context after Build() was called");

            if (string.IsNullOrWhiteSpace(procedureName))
                throw new ArgumentNullException(nameof(procedureName));

            var canonicalProcedureName = SqlMetadataReader.GetCanonicalName(_connectionString, procedureName);
            if (canonicalProcedureName == null)
                throw new InvalidOperationException(
                    string.Format("Procedure '{0}' not found", procedureName));

            var templateInfo = SqlMetadataReader.GetFakeProcedureTemplateInfo(_connectionString, canonicalProcedureName);
            if (templateInfo == null)
                throw new InvalidOperationException(
                    string.Format("Cannot get fake template info for procedure '{0}'", canonicalProcedureName));

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
        /// Добавляет fake таблицу на основе metadata исходной таблицы
        /// </summary>
        /// <param name="tableName">Имя таблицы для подмены</param>
        /// <param name="options">Опции генерации CREATE TABLE скрипта</param>
        /// <returns>Текущий контекст для цепочки вызовов</returns>
        public SqlTestContext MockTable(string tableName, TableDefinitionOptions options = null)
        {
            if (_isBuilt)
                throw new InvalidOperationException("Cannot modify context after Build() was called");

            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentNullException(nameof(tableName));

            var tableDefinition = SqlMetadataReader.GetTableDefinition(_connectionString, tableName, options);
            if (string.IsNullOrWhiteSpace(tableDefinition))
                throw new InvalidOperationException(
                    string.Format("Cannot get definition for table '{0}'", tableName));

            _fakes.Add(new FakeDependency
            {
                OriginalName = tableName,
                ObjectType = ObjectType.Table,
                FakeDefinition = tableDefinition
            });

            return this;
        }

        /// <summary>
        /// Добавляет SQL, который будет выполнен перед каждым Execute/ExecuteWithResult
        /// </summary>
        public SqlTestContext SetUpSql(string setUpSql)
        {
            if (string.IsNullOrWhiteSpace(setUpSql))
                throw new ArgumentNullException(nameof(setUpSql));

            _setUpSqlScripts.Add(setUpSql);
            return this;
        }

        /// <summary>
        /// Добавляет вызов процедуры set up, который будет выполнен перед каждым Execute/ExecuteWithResult
        /// </summary>
        public SqlTestContext SetUpProcedure(string procedureName)
        {
            if (string.IsNullOrWhiteSpace(procedureName))
                throw new ArgumentNullException(nameof(procedureName));

            var canonicalProcedureName = SqlMetadataReader.GetCanonicalName(_connectionString, procedureName);
            if (canonicalProcedureName == null)
                throw new InvalidOperationException(
                    string.Format("Procedure '{0}' not found", procedureName));

            _setUpSqlScripts.Add(string.Format("EXEC {0};", canonicalProcedureName));
            return this;
        }
        
        /// <summary>
        /// Создает все временные объекты в БД
        /// </summary>
        /// <returns>Текущий контекст для цепочки вызовов</returns>
        public SqlTestContext Build()
        {
            if (_isBuilt)
                throw new InvalidOperationException("Build() already called");
            
            if (string.IsNullOrWhiteSpace(_targetProcedure))
                throw new InvalidOperationException("Target procedure not specified. Call ForProcedure() first.");
            
            // Шаг 1: Получаем каноническое имя процедуры
            _canonicalProcedureName = SqlMetadataReader.GetCanonicalName(_connectionString, _targetProcedure);
            if (_canonicalProcedureName == null)
                throw new InvalidOperationException(
                    string.Format("Procedure '{0}' not found", _targetProcedure));
            
            // Шаг 2: Получаем определение процедуры
            var procedureDefinition = SqlMetadataReader.GetObjectDefinition(_connectionString, _canonicalProcedureName);
            
            if (procedureDefinition == null)
                throw new InvalidOperationException(
                    string.Format("Cannot get definition for procedure '{0}'", _canonicalProcedureName));
            
            // Шаг 3: Создаем fake объекты и заменяем их в определении процедуры
            var modifiedProcedureDefinition = procedureDefinition;

            // 3.0: Сначала вычисляем canonical names для всех fake-объектов.
            // Это нужно для корректной логики override (last fake wins).
            foreach (var fake in _fakes)
            {
                // 3.1: Получаем каноническое имя объекта
                fake.CanonicalName = SqlMetadataReader.GetCanonicalName(_connectionString, fake.OriginalName);
                if (fake.CanonicalName == null)
                    throw new InvalidOperationException(
                        string.Format("{0} '{1}' not found", GetObjectDisplayName(fake.ObjectType), fake.OriginalName));
            }

            foreach (var fake in _fakes)
            {
                // Если для того же объекта есть более поздний fake, текущий пропускаем.
                // Семантика как в tSQLt: последняя подмена выигрывает.
                if (IsOverriddenByLaterFake(fake))
                    continue;

                // 3.2: Генерируем имя для fake объекта
                fake.FakeName = TestObjectNameGenerator.Generate(fake.CanonicalName, fake.ObjectType);
                
                // 3.3: Заменяем имя в fake-скрипте
                var fakeFullName = string.Format("[dbo].[{0}]", fake.FakeName);
                fake.FakeDefinitionRenamed = SqlScriptModifier.ReplaceObjectName(
                    fake.FakeDefinition,
                    fake.CanonicalName,
                    fakeFullName
                );
                
                // 3.4: Заменяем обращения к объекту в процедуре на fake
                modifiedProcedureDefinition = SqlScriptModifier.ReplaceObjectName(
                    modifiedProcedureDefinition,
                    fake.CanonicalName,
                    fakeFullName
                );
                
                // 3.5: Создаем fake объект в БД
                if (fake.ObjectType == ObjectType.StoredProcedure &&
                    !string.IsNullOrWhiteSpace(fake.SpyLogTableDefinition))
                {
                    ExecuteSql(fake.SpyLogTableDefinition);
                }

                ExecuteSql(fake.FakeDefinitionRenamed);
            }
            
            // Шаг 4: Генерируем имя для тестовой процедуры
            _testProcedureName = TestObjectNameGenerator.Generate(
                _canonicalProcedureName, 
                ObjectType.StoredProcedure
            );
            
            // Шаг 5: Заменяем имя процедуры в определении
            var testProcedureDefinition = SqlScriptModifier.ReplaceObjectName(
                modifiedProcedureDefinition,
                _canonicalProcedureName,
                string.Format("[dbo].[{0}]", _testProcedureName)
            );
            
            // Шаг 6: Создаем тестовую процедуру
            ExecuteSql(testProcedureDefinition);
            
            _isBuilt = true;
            return this;
        }
        
        /// <summary>
        /// Выполняет тестовую процедуру с параметрами (без получения результатов)
        /// </summary>
        /// <param name="parameters">Параметры для процедуры</param>
        /// <returns>Текущий контекст для цепочки вызовов</returns>
        public SqlTestContext Execute(params SqlParameter[] parameters)
        {
            if (!_isBuilt)
                throw new InvalidOperationException("Call Build() before Execute()");

            RunSetUpScripts();
            
            var sql = string.Format("EXEC [dbo].[{0}]", _testProcedureName);
            
            if (parameters != null && parameters.Length > 0)
            {
                var paramNames = new List<string>();
                foreach (var param in parameters)
                {
                    // Передача по имени: @paramName = @paramName
                    if (param.Direction == System.Data.ParameterDirection.Output || 
                        param.Direction == System.Data.ParameterDirection.InputOutput)
                    {
                        paramNames.Add(string.Format("{0} = {0} OUTPUT", param.ParameterName));
                    }
                    else
                    {
                        paramNames.Add(string.Format("{0} = {0}", param.ParameterName));
                    }
                }
                sql += " " + string.Join(", ", paramNames);
            }
            
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
        /// Выполняет тестовую процедуру с параметрами и возвращает результаты
        /// (SELECT-ы, OUT параметры, RETURN значение)
        /// </summary>
        /// <param name="parameters">Параметры для процедуры</param>
        /// <returns>Результат выполнения процедуры</returns>
        public SqlTestResult ExecuteWithResult(params SqlParameter[] parameters)
        {
            if (!_isBuilt)
                throw new InvalidOperationException("Call Build() before ExecuteWithResult()");

            RunSetUpScripts();
            
            var connection = new SqlConnection(_connectionString);
            connection.Open();
            
            var sql = string.Format("EXEC @RETURN_VALUE = [dbo].[{0}]", _testProcedureName);
            
            if (parameters != null && parameters.Length > 0)
            {
                var paramNames = new List<string>();
                foreach (var param in parameters)
                {
                    // Передача по имени: @paramName = @paramName
                    if (param.Direction == System.Data.ParameterDirection.Output || 
                        param.Direction == System.Data.ParameterDirection.InputOutput)
                    {
                        paramNames.Add(string.Format("{0} = {0} OUTPUT", param.ParameterName));
                    }
                    else
                    {
                        paramNames.Add(string.Format("{0} = {0}", param.ParameterName));
                    }
                }
                sql += " " + string.Join(", ", paramNames);
            }
            
            var command = new SqlCommand(sql, connection);
            command.CommandType = System.Data.CommandType.Text;
            
            // Добавляем параметр для RETURN значения
            var returnParam = new SqlParameter("@RETURN_VALUE", System.Data.SqlDbType.Int)
            {
                Direction = System.Data.ParameterDirection.Output
            };
            command.Parameters.Add(returnParam);
            
            // Добавляем пользовательские параметры
            if (parameters != null)
            {
                command.Parameters.AddRange(parameters);
            }
            
            // Создаем и возвращаем результат
            // ВАЖНО: connection НЕ закрываем здесь, это сделает SqlTestResult при Dispose
            var result = new SqlTestResult(command);
            
            // Закрываем подключение после чтения всех результатов
            connection.Close();
            connection.Dispose();
            
            return result;
        }

        /// <summary>
        /// Выполняет произвольный SQL без возврата наборов данных
        /// </summary>
        public int ExecuteNonQuery(string sql, params SqlParameter[] parameters)
        {
            if (string.IsNullOrWhiteSpace(sql))
                throw new ArgumentNullException(nameof(sql));

            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                if (parameters != null && parameters.Length > 0)
                {
                    command.Parameters.AddRange(parameters);
                }

                connection.Open();
                return command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Возвращает логи вызовов fake процедуры из spy-таблицы
        /// </summary>
        /// <param name="procedureName">Имя процедуры, переданной в MockProcedure()</param>
        /// <returns>Таблица с логами вызовов</returns>
        public DataTable GetSpyProcedureLog(string procedureName)
        {
            if (!_isBuilt)
                throw new InvalidOperationException("Call Build() before GetSpyProcedureLog()");

            if (string.IsNullOrWhiteSpace(procedureName))
                throw new ArgumentNullException(nameof(procedureName));

            var canonicalProcedureName = SqlMetadataReader.GetCanonicalName(_connectionString, procedureName);
            if (canonicalProcedureName == null)
                throw new InvalidOperationException(
                    string.Format("Procedure '{0}' not found", procedureName));

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
                    string.Format("Spy log for procedure '{0}' not found. Call MockProcedure() before Build().", procedureName));

            var sql = string.Format(
                "SELECT * FROM [dbo].[{0}] ORDER BY [_id_];",
                procedureFake.SpyLogTableName
            );

            return ExecuteQuery(sql);
        }
        
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
        
        public void Dispose()
        {
            Cleanup();
        }
        
        public string TestProcedureName => _testProcedureName;
        
        public IReadOnlyList<FakeDependency> Fakes
        {
            get { return _fakes.AsReadOnly(); }
        }

        /// <summary>
        /// Пытается найти fake объект по типу и имени (original/canonical)
        /// </summary>
        public bool TryGetFake(ObjectType objectType, string objectName, out FakeDependency fake)
        {
            if (string.IsNullOrWhiteSpace(objectName))
                throw new ArgumentNullException(nameof(objectName));

            for (var i = _fakes.Count - 1; i >= 0; i--)
            {
                var item = _fakes[i];
                if (item.ObjectType != objectType)
                    continue;

                if (item.OriginalName.Equals(objectName, StringComparison.OrdinalIgnoreCase))
                {
                    fake = item;
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(item.CanonicalName) &&
                    item.CanonicalName.Equals(objectName, StringComparison.OrdinalIgnoreCase))
                {
                    fake = item;
                    return true;
                }
            }

            fake = null;
            return false;
        }

        /// <summary>
        /// Возвращает fake объект по типу и имени (original/canonical)
        /// </summary>
        public FakeDependency GetFake(ObjectType objectType, string objectName)
        {
            FakeDependency fake;
            if (!TryGetFake(objectType, objectName, out fake))
            {
                throw new InvalidOperationException(
                    string.Format("Fake object not found: type={0}, name={1}", objectType, objectName));
            }

            return fake;
        }

        /// <summary>
        /// Возвращает сгенерированное имя fake объекта
        /// </summary>
        public string GetFakeName(ObjectType objectType, string objectName)
        {
            var fake = GetFake(objectType, objectName);
            if (string.IsNullOrWhiteSpace(fake.FakeName))
            {
                throw new InvalidOperationException(
                    "Fake name is not generated yet. Call Build() before requesting fake names.");
            }

            return fake.FakeName;
        }

        private void ExecuteSql(string sql)
        {
            ExecuteNonQuery(sql);
        }

        private void RunSetUpScripts()
        {
            foreach (var setUpSql in _setUpSqlScripts)
            {
                ExecuteNonQuery(setUpSql);
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
        /// Выполняет произвольный SQL и возвращает первый результирующий набор
        /// </summary>
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

        private void DropObject(string objectName, string objectType)
        {
            var sql = string.Format("DROP {0} IF EXISTS [dbo].[{1}]", objectType, objectName);
            try
            {
                ExecuteSql(sql);
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
            switch (objectType)
            {
                case ObjectType.StoredProcedure:
                    return "Procedure";
                case ObjectType.Function:
                    return "Function";
                case ObjectType.Table:
                    return "Table";
                case ObjectType.View:
                    return "View";
                case ObjectType.Trigger:
                    return "Trigger";
                default:
                    throw new ArgumentOutOfRangeException(nameof(objectType), objectType, null);
            }
        }

        private static string GetDropObjectType(ObjectType objectType)
        {
            switch (objectType)
            {
                case ObjectType.StoredProcedure:
                    return "PROCEDURE";
                case ObjectType.Function:
                    return "FUNCTION";
                case ObjectType.Table:
                    return "TABLE";
                case ObjectType.View:
                    return "VIEW";
                case ObjectType.Trigger:
                    return "TRIGGER";
                default:
                    throw new ArgumentOutOfRangeException(nameof(objectType), objectType, null);
            }
        }
    }
}
