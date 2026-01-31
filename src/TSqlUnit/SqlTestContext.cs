using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace TSqlUnit
{
    /// <summary>
    /// Контекст для настройки и выполнения unit-теста SQL процедуры с мокированием зависимостей
    /// </summary>
    public class SqlTestContext : IDisposable
    {
        private readonly string _connectionString;
        private readonly SqlObjectHelper _helper;
        private readonly List<FakeDependency> _fakes = new List<FakeDependency>();
        
        private string _targetProcedure;
        private string _canonicalProcedureName;
        private string _testProcedureName;
        private bool _isBuilt = false;
        
        /// <summary>
        /// Создает новый контекст для тестирования SQL процедуры
        /// </summary>
        /// <param name="connectionString">Строка подключения к SQL Server</param>
        public SqlTestContext(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _helper = new SqlObjectHelper(connectionString);
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
            _canonicalProcedureName = _helper.GetCanonicalName(_targetProcedure);
            if (_canonicalProcedureName == null)
                throw new InvalidOperationException(
                    string.Format("Procedure '{0}' not found", _targetProcedure));
            
            // Шаг 2: Получаем определение процедуры
            var procedureDefinition = Core.GetObjectDefinition(_connectionString, _canonicalProcedureName);
            
            if (procedureDefinition == null)
                throw new InvalidOperationException(
                    string.Format("Cannot get definition for procedure '{0}'", _canonicalProcedureName));
            
            // Шаг 3: Создаем fake функции и заменяем их в определении процедуры
            var modifiedProcedureDefinition = procedureDefinition;
            
            foreach (var fake in _fakes)
            {
                // 3.1: Получаем каноническое имя функции
                fake.CanonicalName = _helper.GetCanonicalName(fake.OriginalName);
                if (fake.CanonicalName == null)
                    throw new InvalidOperationException(
                        string.Format("Function '{0}' not found", fake.OriginalName));
                
                // 3.2: Генерируем имя для fake функции
                fake.FakeName = TestObjectNameGenerator.Generate(fake.CanonicalName, fake.ObjectType);
                
                // 3.3: Заменяем имя в скрипте пользователя
                fake.FakeDefinitionRenamed = Core.ReplaceObjectName(
                    fake.FakeDefinition,
                    fake.OriginalName,
                    string.Format("[dbo].[{0}]", fake.FakeName)
                );
                
                // 3.4: Заменяем вызовы функции в процедуре на fake
                modifiedProcedureDefinition = Core.ReplaceObjectName(
                    modifiedProcedureDefinition,
                    fake.CanonicalName,
                    string.Format("[dbo].[{0}]", fake.FakeName)
                );
                
                // 3.5: Создаем fake функцию в БД
                _helper.ExecuteSql(fake.FakeDefinitionRenamed);
            }
            
            // Шаг 4: Генерируем имя для тестовой процедуры
            _testProcedureName = TestObjectNameGenerator.Generate(
                _canonicalProcedureName, 
                ObjectType.StoredProcedure
            );
            
            // Шаг 5: Заменяем имя процедуры в определении
            var testProcedureDefinition = Core.ReplaceObjectName(
                modifiedProcedureDefinition,
                _canonicalProcedureName,
                string.Format("[dbo].[{0}]", _testProcedureName)
            );
            
            // Шаг 6: Создаем тестовую процедуру
            _helper.ExecuteSql(testProcedureDefinition);
            
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
        /// Очищает все временные объекты
        /// </summary>
        public void Cleanup()
        {
            if (!_isBuilt)
                return;
            
            try
            {
                // Удаляем тестовую процедуру
                if (!string.IsNullOrEmpty(_testProcedureName))
                {
                    _helper.DropObject(_testProcedureName, "PROCEDURE");
                }
                
                // Удаляем fake функции
                foreach (var fake in _fakes)
                {
                    if (!string.IsNullOrEmpty(fake.FakeName))
                    {
                        _helper.DropObject(fake.FakeName, "FUNCTION");
                    }
                }
            }
            catch
            {
                // Игнорируем ошибки при очистке
            }
        }
        
        /// <summary>
        /// Освобождает ресурсы и удаляет временные объекты
        /// </summary>
        public void Dispose()
        {
            Cleanup();
        }
        
        /// <summary>
        /// Получает имя созданной тестовой процедуры (только имя, без схемы)
        /// </summary>
        public string TestProcedureName => _testProcedureName;
        
        /// <summary>
        /// Получает информацию о созданных fake объектах
        /// </summary>
        public IReadOnlyList<FakeDependency> Fakes
        {
            get { return _fakes.AsReadOnly(); }
        }
    }
}
