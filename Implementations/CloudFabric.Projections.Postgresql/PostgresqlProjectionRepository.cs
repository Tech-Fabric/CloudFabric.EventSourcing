using System.Text;
using CloudFabric.Projections.Queries;
using Npgsql;

namespace CloudFabric.Projections.Postgresql;

public class PostgresqlProjectionRepository<TProjectionDocument> : PostgresqlProjectionRepository, IProjectionRepository<TProjectionDocument>
    where TProjectionDocument : ProjectionDocument
{
    public PostgresqlProjectionRepository(string connectionString)
        : base(connectionString, ProjectionDocumentSchemaFactory.FromTypeWithAttributes<TProjectionDocument>())
    {
    }

    public new async Task<TProjectionDocument?> Single(string id, string partitionKey, CancellationToken cancellationToken = default)
    {
        var document = await base.Single(id, partitionKey, cancellationToken);

        if (document == null) return null;

        return ProjectionDocumentSerializer.DeserializeFromDictionary<TProjectionDocument>(document);
    }

    public Task Upsert(TProjectionDocument document, string partitionKey, CancellationToken cancellationToken = default)
    {
        var documentDictionary = ProjectionDocumentSerializer.SerializeToDictionary(document);
        return Upsert(documentDictionary, partitionKey, cancellationToken);
    }

    public new async Task<IReadOnlyCollection<TProjectionDocument>> Query(
        ProjectionQuery projectionQuery,
        string? partitionKey = null,
        CancellationToken cancellationToken = default
    )
    {
        var recordsDictionary = await base.Query(projectionQuery, partitionKey, cancellationToken);

        var records = new List<TProjectionDocument>();

        foreach (var dict in recordsDictionary)
        {
            records.Add(
                ProjectionDocumentSerializer.DeserializeFromDictionary<TProjectionDocument>(dict)
            );
        }

        return records;
    }
}

public class PostgresqlProjectionRepository : IProjectionRepository
{
    private readonly string _connectionString;

    private readonly ProjectionDocumentSchema _projectionDocumentSchema;

    private string? _keyPropertyName;

    private string? _tableName;

    public PostgresqlProjectionRepository(
        string connectionString,
        ProjectionDocumentSchema projectionDocumentSchema
    )
    {
        _connectionString = connectionString;
        _projectionDocumentSchema = projectionDocumentSchema;
    }

    public string TableName
    {
        get
        {
            if (string.IsNullOrEmpty(_tableName))
            {
                _tableName = _projectionDocumentSchema.SchemaName;
            }

            return _tableName;
        }
    }

    public string? KeyColumnName
    {
        get
        {
            if (string.IsNullOrEmpty(_keyPropertyName))
            {
                _keyPropertyName = _projectionDocumentSchema.KeyColumnName;
            }

            return _keyPropertyName;
        }
    }

    public async Task<Dictionary<string, object?>?> Single(string id, string partitionKey, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        if (_projectionDocumentSchema.Properties.Count <= 0)
        {
            throw new ArgumentException(
                "Projection document schema has no properties",
                _projectionDocumentSchema.SchemaName
            );
        }

        await using var cmd = new NpgsqlCommand(
            $"SELECT " +
            string.Join(',', _projectionDocumentSchema.Properties.Select(p => p.PropertyName)) +
            $" FROM {TableName} WHERE {KeyColumnName} = @id AND {nameof(ProjectionDocument.PartitionKey)} = @partitionKey", conn
        )
        {
            Parameters =
            {
                new(KeyColumnName, id),
                new("partitionKey", partitionKey)
            }
        };

        try
        {
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            if (reader.HasRows)
            {
                var result = new Dictionary<string, object?>();

                while (await reader.ReadAsync(cancellationToken))
                {
                    var values = new object[_projectionDocumentSchema.Properties.Count];
                    reader.GetValues(values);

                    if (values.Length <= 0)
                    {
                        return null;
                    }

                    for (var i = 0; i < _projectionDocumentSchema.Properties.Count; i++)
                    {
                        result[_projectionDocumentSchema.Properties[i].PropertyName] =
                            values[i] is DBNull ? null : values[i];
                    }
                }

                return result;
            }

            return null;
        }
        catch (NpgsqlException ex)
        {
            if (ex.SqlState == PostgresErrorCodes.UndefinedTable)
            {
                await HandleUndefinedTableException(conn, cancellationToken);

                return await Single(id, partitionKey, cancellationToken);
            }
            else if (ex.SqlState == PostgresErrorCodes.UndefinedColumn)
            {
                throw new Exception(
                    $"Something went terribly wrong and table for index {TableName} does not have one of the columns. Please delete that table and wait, it will be created automatically.",
                    ex
                );
            }
        }

        return null;
    }

    public async Task Delete(string id, string partitionKey, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand(
            $"DELETE " +
            $" FROM {TableName} WHERE {KeyColumnName} = @id AND {nameof(ProjectionDocument.PartitionKey)} = @partitionKey", conn
        )
        {
            Parameters =
            {
                new("id", id),
                new("partitionKey", partitionKey)
            }
        };

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DeleteAll(string? partitionKey = null, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand(
            $"DELETE " +
            $" FROM {TableName} " +
            (!string.IsNullOrEmpty(partitionKey) ? $" WHERE {nameof(ProjectionDocument.PartitionKey)} = @partitionKey" : ""), conn
        );

        if (!string.IsNullOrEmpty(partitionKey))
        {
            cmd.Parameters.Add(new("partitionKey", partitionKey));
        }

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task Upsert(Dictionary<string, object?> document, string partitionKey, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        if (_projectionDocumentSchema.Properties.Count <= 0)
        {
            throw new ArgumentException(
                "Projection document schema has no properties",
                _projectionDocumentSchema.SchemaName
            );
        }

        document[nameof(ProjectionDocument.PartitionKey)] = partitionKey;

        var propertyNames = _projectionDocumentSchema.Properties.Select(p => p.PropertyName)
            .ToArray();

        await using var cmd = new NpgsqlCommand(
            $"INSERT INTO {TableName} ({string.Join(',', propertyNames)}) " +
            $"VALUES ({string.Join(',', propertyNames.Select(p => $"@{p}"))}) " +
            $"ON CONFLICT ({KeyColumnName}) " +
            $"DO UPDATE SET {string.Join(',', propertyNames.Select(p => $"{p} = @{p}"))} "
            , conn
        );

        foreach (var p in propertyNames)
        {
            cmd.Parameters.Add(new(p, document[p] ?? DBNull.Value));
        }

        try
        {
            var updatedRows = await cmd.ExecuteNonQueryAsync(cancellationToken);

            if (updatedRows != 1)
            {
                throw new Exception("Something happened with upsert operation: no rows were affected");
            }
        }
        catch (NpgsqlException ex)
        {
            if (ex.SqlState == PostgresErrorCodes.UndefinedTable)
            {
                await HandleUndefinedTableException(conn, cancellationToken);

                await Upsert(document, partitionKey, cancellationToken);
            }
            else if (ex.SqlState == PostgresErrorCodes.UndefinedColumn)
            {
                throw new Exception(
                    $"Something went terribly wrong and table for index {TableName} does not have one of the columns. Please delete that table and wait, it will be created automatically.",
                    ex
                );
            }
        }
    }

    public async Task<IReadOnlyCollection<Dictionary<string, object?>>> Query(
        ProjectionQuery projectionQuery,
        string? partitionKey = null,
        CancellationToken cancellationToken = default
    )
    {
        var properties = _projectionDocumentSchema.Properties;

        var sb = new StringBuilder();
        sb.Append("SELECT ");
        sb.AppendJoin(',', properties.Select(p => p.PropertyName));
        sb.Append(" FROM ");
        sb.Append(TableName);
        sb.Append(" WHERE ");

        var (whereClause, parameters) = ConstructConditionFilters(projectionQuery.Filters);

        if (!string.IsNullOrEmpty(partitionKey))
        {
            whereClause += $" AND {nameof(ProjectionDocument.PartitionKey)} = @partitionKey";
            parameters.Add(new("partitionKey", partitionKey));
        }

        if (!string.IsNullOrWhiteSpace(projectionQuery.SearchText) && projectionQuery.SearchText != "*")
        {
            (string searchQuery, NpgsqlParameter param) = ConstructSearchQuery(projectionQuery.SearchText);
            whereClause += (string.IsNullOrWhiteSpace(whereClause) ? $" {searchQuery}" : $" AND {searchQuery}");
            parameters.Add(param);
        }

        sb.Append(whereClause);

        sb.Append(" LIMIT @limit");
        parameters.Add(new NpgsqlParameter("limit", projectionQuery.Limit));
        sb.Append(" OFFSET @offset");
        parameters.Add(new NpgsqlParameter("offset", projectionQuery.Offset));

        if (projectionQuery.OrderBy.Count > 0)
        {
            sb.Append(" ORDER BY ");
            sb.AppendJoin(',', projectionQuery.OrderBy.Select(kv => $"{kv.Key} {kv.Value}"));
        }

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        var cmd = new NpgsqlCommand(sb.ToString(), conn);
        cmd.Parameters.AddRange(parameters.ToArray());

        try
        {
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            var records = new List<Dictionary<string, object?>>();
            while (await reader.ReadAsync(cancellationToken))
            {
                var document = new Dictionary<string, object?>();

                var values = new object[properties.Count];
                reader.GetValues(values);

                if (values.Length <= 0)
                {
                    continue;
                }

                for (var i = 0; i < properties.Count; i++)
                {
                    document[properties[i].PropertyName] = values[i] is DBNull ? null : values[i];
                }

                records.Add(document);
            }

            return records;
        }
        catch (NpgsqlException ex)
        {
            if (ex.SqlState == PostgresErrorCodes.UndefinedTable)
            {
                await HandleUndefinedTableException(conn, cancellationToken);

                await Query(projectionQuery, partitionKey, cancellationToken);
            }
            else if (ex.SqlState == PostgresErrorCodes.UndefinedColumn)
            {
                throw new Exception(
                    $"Something went terribly wrong while querying {TableName}.",
                    ex
                );
            }
        }

        return new List<Dictionary<string, object?>>();
    }

    private static (string, NpgsqlParameter?) ConstructOneConditionFilter(Filter filter)
    {
        var filterOperator = "";
        var propertyName = filter.PropertyName;

        if (string.IsNullOrEmpty(propertyName))
        {
            return (string.Empty, null);
        }

        switch (filter.Operator)
        {
            case FilterOperator.Equal:
                filterOperator = "=";
                break;
            case FilterOperator.Greater:
                filterOperator = ">";
                break;
            case FilterOperator.GreaterOrEqual:
                filterOperator = ">=";
                break;
            case FilterOperator.Lower:
                filterOperator = "<";
                break;
            case FilterOperator.LowerOrEqual:
                filterOperator = "<=";
                break;
            case FilterOperator.NotEqual:
                filterOperator = "!=";
                break;
        }

        return ($"{propertyName} {filterOperator} @{propertyName}", new NpgsqlParameter(propertyName, filter.Value));
    }

    private (string, List<NpgsqlParameter>) ConstructConditionFilter(Filter filter)
    {
        var parameters = new List<NpgsqlParameter>();

        var (q, param) = ConstructOneConditionFilter(filter);

        if (param != null)
        {
            parameters.Add(param);
        }

        foreach (var f in filter.Filters)
        {
            if (!string.IsNullOrEmpty(q))
            {
                q += $" {f.Logic} ";
            }

            var wrapWithParentheses = f.Filter.Filters.Count > 0;

            if (wrapWithParentheses)
            {
                q += "(";
            }

            q += ConstructConditionFilter(f.Filter);

            if (wrapWithParentheses)
            {
                q += ")";
            }
        }

        return (q, parameters);
    }

    private (string, List<NpgsqlParameter>) ConstructConditionFilters(List<Filter> filters)
    {
        var allParameters = new List<NpgsqlParameter>();
        var whereClauses = new List<string>();
        foreach (var f in filters)
        {
            var (whereClause, parameters) = ConstructConditionFilter(f);
            whereClauses.Add(whereClause);
            allParameters.AddRange(parameters);
        }

        return (string.Join(" AND ", whereClauses), allParameters);
    }

    private (string, NpgsqlParameter) ConstructSearchQuery(string searchText)
    {
        var searchableProperties = _projectionDocumentSchema.Properties.Where(x => x.IsSearchable);

        List<string> query = new List<string>();

        foreach (var property in searchableProperties)
        {
            query.Add($"{property.PropertyName} ILIKE @searchText");
        }

        return (
            $"({string.Join(" OR ", query)})",
            new NpgsqlParameter("searchText", $"%{searchText}%")
        );
    }

    private async Task HandleUndefinedTableException(NpgsqlConnection? conn, CancellationToken cancellationToken)
    {
        var commandText = ConstructCreateTableCommandText();

        await using var createTableCommand = new NpgsqlCommand(commandText, conn);
        try
        {
            await createTableCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception createTableException)
        {
            var exception = new Exception($"Failed to create a table for projection {TableName}", createTableException);
            exception.Data.Add("commandText", commandText);
            throw exception;
        }
    }

    private string ConstructCreateTableCommandText()
    {
        var commandText = new StringBuilder();
        commandText.AppendFormat("CREATE TABLE {0} (", TableName);

        var columnsSql = _projectionDocumentSchema.Properties
            .Select(ConstructColumnCreateStatementForProperty);

        commandText.Append(string.Join(',', columnsSql));

        commandText.AppendFormat(")");

        return commandText.ToString();
    }

    private static string ConstructColumnCreateStatementForProperty(ProjectionDocumentPropertySchema property)
    {
        string? column;

        if (property.IsNested)
        {
            throw new NotImplementedException();
            // var nestedFields = new List<PostgresCompositeType.Field>();
            //
            // var isList = propertyType.GetTypeInfo().IsGenericType &&
            //              propertyType.GetGenericTypeDefinition() == typeof(List<>);
            //
            // if (isList)
            // {
            //     propertyType = propertyType.GetGenericArguments()[0];
            // }
            //
            // PropertyInfo[] nestedProps = propertyType.GetProperties();
            // foreach (PropertyInfo nestedProp in nestedProps)
            // {
            //     object[] attrs = nestedProp.GetCustomAttributes(true);
            //     foreach (object attr in attrs)
            //     {
            //         ProjectionDocumentPropertyAttribute nestedPropertyAttribute = attr as ProjectionDocumentPropertyAttribute;
            //
            //         if (propertyAttribute != null)
            //         {
            //             var nestedField = ConstructFieldCreateStatementForProperty(nestedProp, nestedPropertyAttribute);
            //
            //             nestedFields.Add(nestedField);
            //         }
            //     }
            // }
            //
            // if (isList)
            // {
            //     field = new PostgresCompositeType.Field(fieldName, DataType.Collection(DataType.Complex), nestedFields);
            // }
            // else
            // {
            //     field = new PostgresCompositeType.Field(fieldName, DataType.Complex, nestedFields);
            // }
        }
        else
        {
            string? columnType;

            columnType = property.PropertyType switch
            {
                TypeCode.Int32 => "integer",
                TypeCode.Int64 => "bigint",
                TypeCode.Single or TypeCode.Decimal => "decimal",
                TypeCode.Double => "double precision",
                TypeCode.Boolean => "boolean",
                TypeCode.String => "text",
                TypeCode.Object => throw new Exception("Unsupported array element type!"),
                // var elementType = propertyType.GetElementType();
                // if (Type.GetTypeCode(elementType) != TypeCode.String)
                // {
                //     throw new Exception("Unsupported array element type!");
                // }
                //
                // fieldType = "jsonb";
                //
                // break;
                TypeCode.DateTime => "timestamp",
                _ => throw new Exception(
                    $"Postgresql Projection Repository provider doesn't support {property.PropertyName} type."
                ),
            };
            column = $"{property.PropertyName} {columnType}";

            if (property.IsKey)
            {
                column += " NOT NULL PRIMARY KEY";
            }
        }

        return column;
    }
}
