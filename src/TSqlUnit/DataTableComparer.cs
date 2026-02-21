using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;

namespace TSqlUnit
{
    /// <summary>
    /// Утилиты для сравнения и проекции DataTable
    /// </summary>
    public static class DataTableComparer
    {
        /// <summary>
        /// Выбирает подмножество колонок из DataTable
        /// </summary>
        public static DataTable SelectColumns(DataTable source, params string[] requestedColumns)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (requestedColumns == null || requestedColumns.Length == 0)
                throw new ArgumentException("At least one column must be specified", nameof(requestedColumns));

            var result = new DataTable();
            var actualColumnNames = new string[requestedColumns.Length];

            for (var i = 0; i < requestedColumns.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(requestedColumns[i]))
                    throw new ArgumentException("Column name cannot be null or whitespace", nameof(requestedColumns));

                var actualName = GetRequiredColumnName(source, requestedColumns[i]);
                actualColumnNames[i] = actualName;
                result.Columns.Add(requestedColumns[i], source.Columns[actualName].DataType);
            }

            foreach (DataRow sourceRow in source.Rows)
            {
                var values = new object[requestedColumns.Length];
                for (var i = 0; i < requestedColumns.Length; i++)
                {
                    values[i] = sourceRow[actualColumnNames[i]];
                }

                result.Rows.Add(values);
            }

            return result;
        }

        /// <summary>
        /// Возвращает фактическое имя колонки в таблице или выбрасывает исключение
        /// </summary>
        public static string GetRequiredColumnName(DataTable table, string requestedName, bool ignoreCase = true)
        {
            if (table == null)
                throw new ArgumentNullException(nameof(table));
            if (string.IsNullOrWhiteSpace(requestedName))
                throw new ArgumentNullException(nameof(requestedName));

            var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            foreach (DataColumn column in table.Columns)
            {
                if (column.ColumnName.Equals(requestedName, comparison))
                {
                    return column.ColumnName;
                }
            }

            throw new InvalidOperationException(
                string.Format("Column '{0}' not found in result set.", requestedName));
        }

        /// <summary>
        /// Сравнивает две DataTable
        /// </summary>
        public static DataTableComparisonResult Compare(
            DataTable expected,
            DataTable actual,
            DataTableComparisonOptions options = null)
        {
            options = options ?? new DataTableComparisonOptions();

            if (expected == null)
            {
                return new DataTableComparisonResult
                {
                    IsEqual = false,
                    DiffMessage = "Expected DataTable is null."
                };
            }

            if (actual == null)
            {
                return new DataTableComparisonResult
                {
                    IsEqual = false,
                    DiffMessage = "Actual DataTable is null."
                };
            }

            if (expected.Columns.Count != actual.Columns.Count)
            {
                return new DataTableComparisonResult
                {
                    IsEqual = false,
                    DiffMessage = string.Format(
                        "Column count mismatch. Expected: {0}, Actual: {1}.",
                        expected.Columns.Count,
                        actual.Columns.Count)
                };
            }

            var logicalColumns = new List<string>(expected.Columns.Count);
            var columnNameComparison = options.IgnoreColumnNameCase
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            if (options.IgnoreColumnOrder)
            {
                foreach (DataColumn expectedColumn in expected.Columns)
                {
                    var exists = false;
                    foreach (DataColumn actualColumn in actual.Columns)
                    {
                        if (actualColumn.ColumnName.Equals(expectedColumn.ColumnName, columnNameComparison))
                        {
                            exists = true;
                            break;
                        }
                    }

                    if (!exists)
                    {
                        return new DataTableComparisonResult
                        {
                            IsEqual = false,
                            DiffMessage = string.Format("Column '{0}' not found in actual table.", expectedColumn.ColumnName)
                        };
                    }

                    logicalColumns.Add(expectedColumn.ColumnName);
                }
            }
            else
            {
                for (var i = 0; i < expected.Columns.Count; i++)
                {
                    var expectedName = expected.Columns[i].ColumnName;
                    var actualName = actual.Columns[i].ColumnName;
                    if (!actualName.Equals(expectedName, columnNameComparison))
                    {
                        return new DataTableComparisonResult
                        {
                            IsEqual = false,
                            DiffMessage = string.Format(
                                "Column mismatch at index {0}. Expected: '{1}', Actual: '{2}'.",
                                i,
                                expectedName,
                                actualName)
                        };
                    }

                    logicalColumns.Add(expectedName);
                }
            }

            var sortByColumns = options.SortByColumns ?? new string[0];
            foreach (var sortColumn in sortByColumns)
            {
                GetRequiredColumnName(expected, sortColumn, options.IgnoreColumnNameCase);
                GetRequiredColumnName(actual, sortColumn, options.IgnoreColumnNameCase);
            }

            var expectedSignatures = BuildRowSignatures(expected, logicalColumns, sortByColumns, options);
            var actualSignatures = BuildRowSignatures(actual, logicalColumns, sortByColumns, options);

            if (expectedSignatures.Length != actualSignatures.Length)
            {
                return new DataTableComparisonResult
                {
                    IsEqual = false,
                    DiffMessage = string.Format(
                        "Row count mismatch. Expected: {0}, Actual: {1}.",
                        expectedSignatures.Length,
                        actualSignatures.Length)
                };
            }

            for (var i = 0; i < expectedSignatures.Length; i++)
            {
                if (!expectedSignatures[i].Equals(actualSignatures[i], StringComparison.Ordinal))
                {
                    return new DataTableComparisonResult
                    {
                        IsEqual = false,
                        DiffMessage = string.Format(
                            "Row mismatch at index {0}. Expected: '{1}', Actual: '{2}'.",
                            i,
                            expectedSignatures[i],
                            actualSignatures[i])
                    };
                }
            }

            return new DataTableComparisonResult
            {
                IsEqual = true,
                DiffMessage = string.Empty
            };
        }

        private static string[] BuildRowSignatures(
            DataTable table,
            List<string> logicalColumns,
            string[] sortByColumns,
            DataTableComparisonOptions options)
        {
            var signatures = new List<RowSignature>(table.Rows.Count);
            var ignoreCase = options.IgnoreColumnNameCase;

            foreach (DataRow row in table.Rows)
            {
                var valueParts = new string[logicalColumns.Count];
                for (var i = 0; i < logicalColumns.Count; i++)
                {
                    var actualColumnName = GetRequiredColumnName(table, logicalColumns[i], ignoreCase);
                    valueParts[i] = NormalizeValue(row[actualColumnName]);
                }

                var sortKey = string.Empty;
                if (sortByColumns.Length > 0)
                {
                    var sortParts = new string[sortByColumns.Length];
                    for (var i = 0; i < sortByColumns.Length; i++)
                    {
                        var actualSortColumnName = GetRequiredColumnName(table, sortByColumns[i], ignoreCase);
                        sortParts[i] = NormalizeValue(row[actualSortColumnName]);
                    }

                    sortKey = string.Join("|", sortParts);
                }

                signatures.Add(new RowSignature
                {
                    Signature = string.Join("|", valueParts),
                    SortKey = sortKey
                });
            }

            if (sortByColumns.Length > 0)
            {
                signatures.Sort((x, y) => StringComparer.Ordinal.Compare(x.SortKey, y.SortKey));
            }
            else if (options.IgnoreRowOrder)
            {
                signatures.Sort((x, y) => StringComparer.Ordinal.Compare(x.Signature, y.Signature));
            }

            var result = new string[signatures.Count];
            for (var i = 0; i < signatures.Count; i++)
            {
                result[i] = signatures[i].Signature;
            }

            return result;
        }

        private static string NormalizeValue(object value)
        {
            if (value == null || value == DBNull.Value)
                return "<NULL>";

            switch (value)
            {
                case decimal decimalValue:
                    return decimalValue.ToString("0.############################", CultureInfo.InvariantCulture);
                case float floatValue:
                    return floatValue.ToString("R", CultureInfo.InvariantCulture);
                case double doubleValue:
                    return doubleValue.ToString("R", CultureInfo.InvariantCulture);
                case IFormattable formattable:
                    return formattable.ToString(null, CultureInfo.InvariantCulture);
                default:
                    return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            }
        }

        private class RowSignature
        {
            public string Signature { get; set; }

            public string SortKey { get; set; }
        }
    }
}
