using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;

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
        /// Выбрасывает исключение, если таблицы не равны
        /// </summary>
        public static void EnsureEqual(
            DataTable expected,
            DataTable actual,
            DataTableComparisonOptions options = null)
        {
            var comparison = Compare(expected, actual, options);
            if (!comparison.IsEqual)
                throw new InvalidOperationException(comparison.DiffMessage);
        }

        /// <summary>
        /// Форматирует DataTable в текстовую таблицу
        /// </summary>
        public static string FormatAsTextTable(DataTable table, int maxRows = 200, int maxCellLength = 120)
        {
            if (table == null)
                return "<null>";

            if (maxRows <= 0)
                maxRows = 200;
            if (maxCellLength <= 0)
                maxCellLength = 120;

            var columnCount = table.Columns.Count;
            if (columnCount == 0)
                return "<empty table>";

            var rowCountToRender = table.Rows.Count < maxRows ? table.Rows.Count : maxRows;

            var widths = new int[columnCount];
            var renderedValues = new string[rowCountToRender][];
            for (var c = 0; c < columnCount; c++)
            {
                widths[c] = table.Columns[c].ColumnName.Length;
            }

            for (var r = 0; r < rowCountToRender; r++)
            {
                var row = table.Rows[r];
                var renderedRow = new string[columnCount];

                for (var c = 0; c < columnCount; c++)
                {
                    var value = NormalizeValue(row[c]);
                    value = value.Replace("\r", "\\r").Replace("\n", "\\n");
                    if (value.Length > maxCellLength)
                        value = value.Substring(0, maxCellLength - 1) + "…";

                    renderedRow[c] = value;
                    if (value.Length > widths[c])
                        widths[c] = value.Length;
                }

                renderedValues[r] = renderedRow;
            }

            var sb = new StringBuilder();
            sb.AppendLine(BuildTableSeparator(widths));
            sb.AppendLine(BuildTableRow(GetHeaderValues(table), widths));
            sb.AppendLine(BuildTableSeparator(widths));

            for (var r = 0; r < rowCountToRender; r++)
            {
                sb.AppendLine(BuildTableRow(renderedValues[r], widths));
            }

            sb.AppendLine(BuildTableSeparator(widths));
            if (table.Rows.Count > rowCountToRender)
            {
                sb.AppendLine(string.Format(
                    "... ({0} rows omitted, total {1})",
                    table.Rows.Count - rowCountToRender,
                    table.Rows.Count));
            }

            return sb.ToString();
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
            if (options.MaxDiffRows <= 0)
                options.MaxDiffRows = 200;
            if (options.MaxCellLength <= 0)
                options.MaxCellLength = 120;

            if (expected == null)
            {
                return new DataTableComparisonResult
                {
                    IsEqual = false,
                    DiffMessage = "Expected DataTable is null.",
                    DiffTable = null
                };
            }

            if (actual == null)
            {
                return new DataTableComparisonResult
                {
                    IsEqual = false,
                    DiffMessage = "Actual DataTable is null.",
                    DiffTable = null
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
                        actual.Columns.Count),
                    DiffTable = null
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
                            DiffMessage = string.Format("Column '{0}' not found in actual table.", expectedColumn.ColumnName),
                            DiffTable = null
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
                                actualName),
                            DiffTable = null
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

            var expectedRows = BuildRows(expected, logicalColumns, sortByColumns, options);
            var actualRows = BuildRows(actual, logicalColumns, sortByColumns, options);
            var expectedSignatures = BuildComparisonSignatures(expectedRows, sortByColumns, options);
            var actualSignatures = BuildComparisonSignatures(actualRows, sortByColumns, options);

            var sameMultiset = HaveSameMultiset(expectedRows, actualRows);

            var isEqual = AreSignaturesEqual(expectedSignatures, actualSignatures);
            if (isEqual)
            {
                return new DataTableComparisonResult
                {
                    IsEqual = true,
                    DiffMessage = string.Empty,
                    DiffTable = null
                };
            }

            // Если порядок строк важен и отличается только порядок — покажем это явно.
            var orderSensitive = !options.IgnoreRowOrder && sortByColumns.Length == 0;
            if (orderSensitive && sameMultiset)
            {
                var message = new StringBuilder();
                message.AppendLine("DataTable mismatch: row order differs.");
                message.AppendLine("Expected order:");
                message.AppendLine(FormatAsTextTable(expected, options.MaxDiffRows, options.MaxCellLength));
                message.AppendLine("Actual order:");
                message.AppendLine(FormatAsTextTable(actual, options.MaxDiffRows, options.MaxCellLength));

                return new DataTableComparisonResult
                {
                    IsEqual = false,
                    DiffMessage = message.ToString(),
                    DiffTable = null
                };
            }

            var diffTable = BuildDiffTable(expectedRows, actualRows, logicalColumns, options);
            var diffText = FormatAsTextTable(diffTable, options.MaxDiffRows, options.MaxCellLength);

            var diffMessage = new StringBuilder();
            diffMessage.AppendLine("DataTable mismatch.");
            diffMessage.AppendLine("Legend for column '_m_':");
            diffMessage.AppendLine("< - row exists only in expected");
            diffMessage.AppendLine("> - row exists only in actual");
            diffMessage.AppendLine("= - row exists in both");
            diffMessage.AppendLine();
            diffMessage.AppendLine(diffText);

            return new DataTableComparisonResult
            {
                IsEqual = false,
                DiffMessage = diffMessage.ToString(),
                DiffTable = diffTable
            };
        }

        private static string[] BuildComparisonSignatures(
            List<NormalizedRow> rows,
            string[] sortByColumns,
            DataTableComparisonOptions options)
        {
            var signatures = new string[rows.Count];
            for (var i = 0; i < rows.Count; i++)
            {
                signatures[i] = rows[i].Signature;
            }

            if (sortByColumns.Length > 0)
            {
                Array.Sort(signatures, StringComparer.Ordinal);
            }
            else if (options.IgnoreRowOrder)
            {
                Array.Sort(signatures, StringComparer.Ordinal);
            }

            return signatures;
        }

        private static bool HaveSameMultiset(List<NormalizedRow> expectedRows, List<NormalizedRow> actualRows)
        {
            var expectedCounts = BuildSignatureCounts(expectedRows);
            var actualCounts = BuildSignatureCounts(actualRows);
            if (expectedCounts.Count != actualCounts.Count)
                return false;

            foreach (var pair in expectedCounts)
            {
                int actualCount;
                if (!actualCounts.TryGetValue(pair.Key, out actualCount))
                    return false;
                if (actualCount != pair.Value)
                    return false;
            }

            return true;
        }

        private static bool AreSignaturesEqual(string[] expectedSignatures, string[] actualSignatures)
        {
            if (expectedSignatures.Length != actualSignatures.Length)
                return false;

            for (var i = 0; i < expectedSignatures.Length; i++)
            {
                if (!expectedSignatures[i].Equals(actualSignatures[i], StringComparison.Ordinal))
                    return false;
            }

            return true;
        }

        private static List<NormalizedRow> BuildRows(
            DataTable table,
            List<string> logicalColumns,
            string[] sortByColumns,
            DataTableComparisonOptions options)
        {
            var rows = new List<NormalizedRow>(table.Rows.Count);
            var ignoreCase = options.IgnoreColumnNameCase;

            var actualColumnNames = new string[logicalColumns.Count];
            for (var i = 0; i < logicalColumns.Count; i++)
            {
                actualColumnNames[i] = GetRequiredColumnName(table, logicalColumns[i], ignoreCase);
            }

            var sortColumnNames = new string[sortByColumns.Length];
            for (var i = 0; i < sortByColumns.Length; i++)
            {
                sortColumnNames[i] = GetRequiredColumnName(table, sortByColumns[i], ignoreCase);
            }

            foreach (DataRow row in table.Rows)
            {
                var values = new string[logicalColumns.Count];
                for (var i = 0; i < logicalColumns.Count; i++)
                {
                    values[i] = NormalizeValue(row[actualColumnNames[i]]);
                }

                var sortKey = string.Empty;
                if (sortColumnNames.Length > 0)
                {
                    var sortParts = new string[sortColumnNames.Length];
                    for (var i = 0; i < sortColumnNames.Length; i++)
                    {
                        sortParts[i] = NormalizeValue(row[sortColumnNames[i]]);
                    }

                    sortKey = string.Join("|", sortParts);
                }

                rows.Add(new NormalizedRow
                {
                    Values = values,
                    Signature = string.Join("|", values),
                    SortKey = sortKey
                });
            }

            return rows;
        }

        private static Dictionary<string, int> BuildSignatureCounts(List<NormalizedRow> rows)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                int current;
                counts.TryGetValue(row.Signature, out current);
                counts[row.Signature] = current + 1;
            }

            return counts;
        }

        private static DataTable BuildDiffTable(
            List<NormalizedRow> expectedRows,
            List<NormalizedRow> actualRows,
            List<string> logicalColumns,
            DataTableComparisonOptions options)
        {
            var table = new DataTable();
            table.Columns.Add("_m_", typeof(string));
            foreach (var column in logicalColumns)
            {
                table.Columns.Add(column, typeof(string));
            }

            var expectedBuckets = BuildBuckets(expectedRows);
            var actualBuckets = BuildBuckets(actualRows);
            var allKeys = new List<string>();
            foreach (var key in expectedBuckets.Keys)
            {
                if (!allKeys.Contains(key))
                    allKeys.Add(key);
            }

            foreach (var key in actualBuckets.Keys)
            {
                if (!allKeys.Contains(key))
                    allKeys.Add(key);
            }

            allKeys.Sort(StringComparer.Ordinal);

            var remainingRows = options.MaxDiffRows;
            foreach (var key in allKeys)
            {
                Queue<NormalizedRow> expectedQueue;
                Queue<NormalizedRow> actualQueue;

                if (!expectedBuckets.TryGetValue(key, out expectedQueue))
                    expectedQueue = new Queue<NormalizedRow>();
                if (!actualBuckets.TryGetValue(key, out actualQueue))
                    actualQueue = new Queue<NormalizedRow>();

                var matchedCount = expectedQueue.Count < actualQueue.Count ? expectedQueue.Count : actualQueue.Count;

                if (options.IncludeMatchedRowsInDiff)
                {
                    for (var i = 0; i < matchedCount && remainingRows > 0; i++)
                    {
                        var row = expectedQueue.Dequeue();
                        actualQueue.Dequeue();
                        AddDiffRow(table, "=", row.Values);
                        remainingRows--;
                    }
                }
                else
                {
                    for (var i = 0; i < matchedCount; i++)
                    {
                        expectedQueue.Dequeue();
                        actualQueue.Dequeue();
                    }
                }

                while (expectedQueue.Count > 0 && remainingRows > 0)
                {
                    var row = expectedQueue.Dequeue();
                    AddDiffRow(table, "<", row.Values);
                    remainingRows--;
                }

                while (actualQueue.Count > 0 && remainingRows > 0)
                {
                    var row = actualQueue.Dequeue();
                    AddDiffRow(table, ">", row.Values);
                    remainingRows--;
                }

                if (remainingRows <= 0)
                    break;
            }

            return table;
        }

        private static Dictionary<string, Queue<NormalizedRow>> BuildBuckets(List<NormalizedRow> rows)
        {
            var buckets = new Dictionary<string, Queue<NormalizedRow>>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                Queue<NormalizedRow> queue;
                if (!buckets.TryGetValue(row.Signature, out queue))
                {
                    queue = new Queue<NormalizedRow>();
                    buckets.Add(row.Signature, queue);
                }

                queue.Enqueue(row);
            }

            return buckets;
        }

        private static void AddDiffRow(DataTable table, string marker, string[] values)
        {
            var row = table.NewRow();
            row[0] = marker;
            for (var i = 0; i < values.Length; i++)
            {
                row[i + 1] = values[i];
            }

            table.Rows.Add(row);
        }

        private static string[] GetHeaderValues(DataTable table)
        {
            var headers = new string[table.Columns.Count];
            for (var i = 0; i < table.Columns.Count; i++)
            {
                headers[i] = table.Columns[i].ColumnName;
            }

            return headers;
        }

        private static string BuildTableSeparator(int[] widths)
        {
            var sb = new StringBuilder();
            sb.Append('+');
            for (var i = 0; i < widths.Length; i++)
            {
                sb.Append(new string('-', widths[i] + 2));
                sb.Append('+');
            }

            return sb.ToString();
        }

        private static string BuildTableRow(string[] values, int[] widths)
        {
            var sb = new StringBuilder();
            sb.Append('|');
            for (var i = 0; i < widths.Length; i++)
            {
                var value = i < values.Length ? values[i] : string.Empty;
                sb.Append(' ');
                sb.Append(value.PadRight(widths[i]));
                sb.Append(' ');
                sb.Append('|');
            }

            return sb.ToString();
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

        private class NormalizedRow
        {
            public string[] Values { get; set; }

            public string Signature { get; set; }

            public string SortKey { get; set; }
        }
    }
}
