DECLARE @object_id INT = OBJECT_ID(@object_name);

WITH parameters AS
(
  SELECT p.parameter_id
       , p.name
       , ' ' +
              CASE
                WHEN tt.user_type_id IS NOT NULL THEN QUOTENAME(SCHEMA_NAME(tt.schema_id)) + N'.' + QUOTENAME(tt.name) -- TVP
                WHEN t.is_user_defined = 1 THEN QUOTENAME(SCHEMA_NAME(t.schema_id)) + N'.' + QUOTENAME(t.name)   -- alias/UDT
                WHEN t.name IN (N'nchar', N'nvarchar') THEN UPPER(t.name + N'(' + IIF(p.max_length = -1, N'max', CONVERT(NVARCHAR(10), p.max_length / 2)) + N')')
                WHEN t.name IN (N'char', N'varchar', N'binary', N'varbinary') THEN UPPER(t.name + N'(' + IIF(p.max_length = -1, N'max', CONVERT(NVARCHAR(10), p.max_length)) + N')')
                WHEN t.name IN (N'decimal', N'numeric') THEN UPPER(t.name + N'(' + CONVERT(NVARCHAR(10), p.[precision]) + N',' + CONVERT(NVARCHAR(10), p.scale) + N')')
                WHEN t.name IN (N'datetime2', N'datetimeoffset', N'time') THEN UPPER(t.name + N'(' + CONVERT(NVARCHAR(10), p.scale) + N')')
                ELSE UPPER(t.name)
              END AS data_type
       , p.is_cursor_ref
       , p.is_readonly
       , p.is_output
       , CAST(IIF(tt.user_type_id IS NOT NULL, 1, 0) AS BIT) AS is_table_type
  FROM sys.parameters AS p
    INNER JOIN sys.types AS t ON t.user_type_id = p.user_type_id
    LEFT JOIN sys.table_types AS tt ON tt.user_type_id = p.user_type_id
  WHERE p.object_id = @object_id
)
SELECT STRING_AGG(CAST(p.name + IIF(p.is_cursor_ref = 1, ' CURSOR VARYING OUTPUT', p.data_type + IIF(p.is_readonly = 1, ' READONLY', ' = NULL') + IIF(p.is_output = 1, ' OUT', '')) AS NVARCHAR(MAX)), CHAR(10) + ',') WITHIN GROUP (ORDER BY p.parameter_id) AS parameters_list
     , '_id_ INT IDENTITY(1,1) PRIMARY KEY CLUSTERED' + CHAR(10) + ','
     + STRING_AGG(CAST(IIF(p.is_cursor_ref = 1, '', QUOTENAME(STUFF(p.name, 1, 1, '')) + IIF(p.is_table_type = 1, ' XML', p.data_type) + ' NULL') AS NVARCHAR(MAX)), CHAR(10) + ',') WITHIN GROUP (ORDER BY p.parameter_id) AS columns_list
     , STRING_AGG(CAST(IIF(p.is_cursor_ref = 1, '', QUOTENAME(STUFF(p.name, 1, 1, ''))) AS NVARCHAR(MAX)), ', ') WITHIN GROUP (ORDER BY p.parameter_id) AS insert_list
     , STRING_AGG(CAST(IIF(p.is_cursor_ref = 1, '', IIF(p.is_table_type = 1, '(SELECT * FROM ' + p.name + ' FOR XML PATH(''row''), TYPE, ROOT(''' + STUFF(p.name, 1, 1, '') + '''))', p.name)) AS NVARCHAR(MAX)), ', ') WITHIN GROUP (ORDER BY p.parameter_id) AS select_list
FROM parameters AS p;