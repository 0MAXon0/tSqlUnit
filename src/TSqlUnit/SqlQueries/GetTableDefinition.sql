DECLARE @object_id AS INT = OBJECT_ID(@table_name);

IF @object_id IS NULL
    SELECT NULL AS table_definition;
ELSE
BEGIN
   ;WITH table_info AS (
        SELECT 
            c.object_id,
            c.column_id,
            c.name COLLATE DATABASE_DEFAULT AS column_name,
            TYPE_NAME(c.user_type_id) COLLATE DATABASE_DEFAULT AS data_type,
            c.max_length,
            c.precision,
            c.scale,
            c.is_nullable,
            c.is_identity,
            ic.seed_value,
            ic.increment_value,
            c.is_computed,
            cc.definition AS computed_definition,
            dc.definition AS default_definition,
            dc.name COLLATE DATABASE_DEFAULT AS default_constraint_name
        FROM sys.columns c
        LEFT JOIN sys.identity_columns ic ON c.object_id = ic.object_id AND c.column_id = ic.column_id
        LEFT JOIN sys.computed_columns cc ON c.object_id = cc.object_id AND c.column_id = cc.column_id
        LEFT JOIN sys.default_constraints dc ON c.default_object_id = dc.object_id
        WHERE c.object_id = @object_id
          AND (@include_computed_columns = 1 OR c.is_computed = 0)
    ),
    pk_info AS (
        SELECT 
            STUFF((
                SELECT ', ' + QUOTENAME(COL_NAME(ic.object_id, ic.column_id) COLLATE DATABASE_DEFAULT)
                FROM sys.index_columns ic
                WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id
                ORDER BY ic.key_ordinal
                FOR XML PATH(''), TYPE
            ).value('.', 'NVARCHAR(MAX)'), 1, 2, '') AS pk_columns,
            i.name COLLATE DATABASE_DEFAULT AS pk_name,
            i.type_desc COLLATE DATABASE_DEFAULT AS index_type
        FROM sys.indexes i
        WHERE i.is_primary_key = 1 
          AND i.object_id = @object_id
          AND @include_primary_key = 1
    ),
    uq_info AS (
        SELECT 
            i.name COLLATE DATABASE_DEFAULT AS constraint_name,
            STUFF((
                SELECT ', ' + QUOTENAME(COL_NAME(ic.object_id, ic.column_id) COLLATE DATABASE_DEFAULT)
                FROM sys.index_columns ic
                WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id
                ORDER BY ic.key_ordinal
                FOR XML PATH(''), TYPE
            ).value('.', 'NVARCHAR(MAX)'), 1, 2, '') AS uq_columns,
            i.type_desc COLLATE DATABASE_DEFAULT AS index_type
        FROM sys.indexes i
        WHERE i.is_unique_constraint = 1 
          AND i.object_id = @object_id
          AND @include_unique_constraints = 1
    ),
    fk_info AS (
        SELECT 
            fk.name COLLATE DATABASE_DEFAULT AS constraint_name,
            STUFF((
                SELECT ', ' + QUOTENAME(COL_NAME(fkc.parent_object_id, fkc.parent_column_id) COLLATE DATABASE_DEFAULT)
                FROM sys.foreign_key_columns fkc
                WHERE fkc.constraint_object_id = fk.object_id
                ORDER BY fkc.constraint_column_id
                FOR XML PATH(''), TYPE
            ).value('.', 'NVARCHAR(MAX)'), 1, 2, '') AS fk_columns,
            QUOTENAME(OBJECT_SCHEMA_NAME(fk.referenced_object_id) COLLATE DATABASE_DEFAULT) + '.' + 
            QUOTENAME(OBJECT_NAME(fk.referenced_object_id) COLLATE DATABASE_DEFAULT) AS ref_table,
            STUFF((
                SELECT ', ' + QUOTENAME(COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) COLLATE DATABASE_DEFAULT)
                FROM sys.foreign_key_columns fkc
                WHERE fkc.constraint_object_id = fk.object_id
                ORDER BY fkc.constraint_column_id
                FOR XML PATH(''), TYPE
            ).value('.', 'NVARCHAR(MAX)'), 1, 2, '') AS ref_columns,
            fk.delete_referential_action_desc COLLATE DATABASE_DEFAULT AS on_delete_action,
            fk.update_referential_action_desc COLLATE DATABASE_DEFAULT AS on_update_action
        FROM sys.foreign_keys fk
        WHERE fk.parent_object_id = @object_id
          AND @include_foreign_keys = 1
    ),
    chk_info AS (
        SELECT 
            cc.name COLLATE DATABASE_DEFAULT AS constraint_name,
            cc.definition AS definition
        FROM sys.check_constraints cc
        WHERE cc.parent_object_id = @object_id
          AND @include_check_constraints = 1
    )
    SELECT 
        'CREATE TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(@object_id) COLLATE DATABASE_DEFAULT) + '.' + 
        QUOTENAME(OBJECT_NAME(@object_id) COLLATE DATABASE_DEFAULT) + ' (' +
        
        -- Columns
        STUFF((
            SELECT ',' + CHAR(13) + CHAR(10) + '    ' + 
                QUOTENAME(t.column_name) + ' ' +
                
                -- Computed column
                CASE 
                    WHEN t.is_computed = 1 THEN 'AS ' + t.computed_definition
                    ELSE
                        -- Data type
                        t.data_type + 
                        CASE 
                            WHEN t.data_type IN ('varchar', 'char', 'nvarchar', 'nchar', 'binary', 'varbinary') THEN
                                CASE WHEN t.max_length = -1 THEN '(MAX)' 
                                     ELSE '(' + CAST(
                                         CASE WHEN t.data_type IN ('nvarchar', 'nchar') 
                                              THEN t.max_length / 2 
                                              ELSE t.max_length 
                                         END AS VARCHAR) + ')' 
                                END
                            WHEN t.data_type IN ('decimal', 'numeric') THEN
                                '(' + CAST(t.precision AS VARCHAR) + ',' + CAST(t.scale AS VARCHAR) + ')'
                            WHEN t.data_type IN ('datetime2', 'time', 'datetimeoffset') THEN
                                '(' + CAST(t.scale AS VARCHAR) + ')'
                            ELSE ''
                        END +
                        
                        -- IDENTITY
                        CASE 
                            WHEN t.is_identity = 1 AND @include_identity = 1
                            THEN ' IDENTITY(' + CAST(t.seed_value AS VARCHAR) + ',' + CAST(t.increment_value AS VARCHAR) + ')' 
                            ELSE '' 
                        END +
                        
                        -- NULL/NOT NULL
                        CASE 
                            WHEN @include_not_null = 1 AND t.is_nullable = 0 THEN ' NOT NULL'
                            ELSE ' NULL' 
                        END +
                        
                        -- DEFAULT
                        CASE 
                            WHEN t.default_definition IS NOT NULL AND @include_defaults = 1
                            THEN ' CONSTRAINT ' + QUOTENAME(t.default_constraint_name) + ' DEFAULT ' + t.default_definition
                            ELSE '' 
                        END
                END
            FROM table_info t
            ORDER BY t.column_id
            FOR XML PATH(''), TYPE
        ).value('.', 'NVARCHAR(MAX)'), 1, 1, '') +
        
        -- Primary Key
        ISNULL((
            SELECT ',' + CHAR(13) + CHAR(10) + '    CONSTRAINT ' + QUOTENAME(pk.pk_name) + 
                   ' PRIMARY KEY ' + pk.index_type + ' (' + pk.pk_columns + ')'
            FROM pk_info pk
        ), '') +
        
        -- Unique Constraints
        ISNULL((
            SELECT ',' + CHAR(13) + CHAR(10) + '    CONSTRAINT ' + QUOTENAME(uq.constraint_name) + 
                   ' UNIQUE ' + uq.index_type + ' (' + uq.uq_columns + ')'
            FROM uq_info uq
            FOR XML PATH(''), TYPE
        ).value('.', 'NVARCHAR(MAX)'), '') +
        
        -- Foreign Keys
        ISNULL((
            SELECT ',' + CHAR(13) + CHAR(10) + '    CONSTRAINT ' + QUOTENAME(fk.constraint_name) + 
                   ' FOREIGN KEY (' + fk.fk_columns + ') REFERENCES ' + fk.ref_table + ' (' + fk.ref_columns + ')' +
                   CASE WHEN fk.on_update_action <> 'NO_ACTION' 
                        THEN ' ON UPDATE ' + REPLACE(fk.on_update_action, '_', ' ')
                        ELSE '' 
                   END +
                   CASE WHEN fk.on_delete_action <> 'NO_ACTION' 
                        THEN ' ON DELETE ' + REPLACE(fk.on_delete_action, '_', ' ')
                        ELSE '' 
                   END
            FROM fk_info fk
            FOR XML PATH(''), TYPE
        ).value('.', 'NVARCHAR(MAX)'), '') +
        
        -- Check Constraints
        ISNULL((
            SELECT ',' + CHAR(13) + CHAR(10) + '    CONSTRAINT ' + QUOTENAME(chk.constraint_name) + ' CHECK ' + chk.definition
            FROM chk_info chk
            FOR XML PATH(''), TYPE
        ).value('.', 'NVARCHAR(MAX)'), '') +
        
        CHAR(13) + CHAR(10) + ')' AS table_definition;
END