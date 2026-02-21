using System;
using System.Collections.Generic;

namespace TSqlUnit
{
    /// <summary>
    /// Набор тестов с общим SetUp для каждого создаваемого контекста
    /// </summary>
    public class SqlTestSuite
    {
        private readonly string _connectionString;
        private readonly List<Action<SqlTestContext>> _setUpActions = new List<Action<SqlTestContext>>();

        public SqlTestSuite(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        /// <summary>
        /// Регистрирует общее set up действие, которое будет применяться к каждому новому контексту
        /// </summary>
        public SqlTestSuite SetUp(Action<SqlTestContext> setUpAction)
        {
            if (setUpAction == null)
                throw new ArgumentNullException(nameof(setUpAction));

            _setUpActions.Add(setUpAction);
            return this;
        }

        /// <summary>
        /// Создает новый тестовый контекст для указанной процедуры и применяет общий SetUp
        /// </summary>
        public SqlTestContext ForProcedure(string procedureName)
        {
            var context = new SqlTestContext(_connectionString).ForProcedure(procedureName);
            foreach (var setUpAction in _setUpActions)
            {
                setUpAction(context);
            }

            return context;
        }
    }
}
