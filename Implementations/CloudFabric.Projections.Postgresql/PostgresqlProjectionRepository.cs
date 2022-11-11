using System.Text;
using System.Text.Json;
using CloudFabric.Projections.Queries;
using CloudFabric.Projections.Utils;
using Npgsql;

namespace CloudFabric.Projections.Postgresql;

public class QueryChunk
{
    public string WhereChunk { get; set; }
    public List<NpgsqlParameter> Parameters { get; init; } = new ();
    public List<string> AdditionalFromSelects { get; init; } = new();
}

public class PostgresqlProjectionRepository<TProjectionDocument> : PostgresqlProjectionRepository, IProjectionRepository<TProjectionDocument>
    where TProjectionDocument : ProjectionDocument
{
    public PostgresqlProjectionRepository(string connectionString)
        : base(connectionString, ProjectionDocumentSchemaFactory.FromTypeWithAttributes<TProjectionDocument>())
    {
    }

    public new async Task<TProjectionDocument?> Single(Guid id, string partitionKey, CancellationToken cancellationToken = default)
    {        
        if (id == Guid.Empty)
        {
            throw new ArgumentNullException(nameof(id));
        }

        if (string.IsNullOrEmpty(partitionKey))
        {
            throw new ArgumentNullException(nameof(partitionKey));
        }
        
        var document = await base.Single(id, partitionKey, cancellationToken);

        if (document == null) return null;

        return ProjectionDocumentSerializer.DeserializeFromDictionary<TProjectionDocument>(document);
    }

    public Task Upsert(TProjectionDocument document, string partitionKey, CancellationToken cancellationToken = default)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }
        
        if (string.IsNullOrEmpty(partitionKey))
        {
            throw new ArgumentNullException(nameof(partitionKey));
        }
        
        var documentDictionary = ProjectionDocumentSerializer.SerializeToDictionary(document);
        return Upsert(documentDictionary, partitionKey, cancellationToken);
    }

    public new async Task<IReadOnlyCollection<TProjectionDocument>> Query(
        ProjectionQuery projectionQuery,
        string? partitionKey = null,
        CancellationToken cancellationToken = default
    )
    {
        if (projectionQuery == null)
        {
            throw new ArgumentNullException(nameof(projectionQuery));
        }
        
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
        
        // for dynamic projection document schemas we need to ensure 'partitionKey' column is always there
        if (_projectionDocumentSchema.Properties.All(p => p.PropertyName != "PartitionKey"))
        {
            _projectionDocumentSchema.Properties.Add(new ProjectionDocumentPropertySchema()
            {
                PropertyName = "PartitionKey",
                PropertyType = TypeCode.String,
                IsFilterable = true
            });
        }
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

    public async Task<Dictionary<string, object?>?> Single(Guid id, string partitionKey, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentNullException(nameof(id));
        }

        if (string.IsNullOrEmpty(partitionKey))
        {
            throw new ArgumentNullException(nameof(partitionKey));
        }
        
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
            $" FROM \"{TableName}\" WHERE {KeyColumnName} = @id AND {nameof(ProjectionDocument.PartitionKey)} = @partitionKey", conn
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
                        if (values[i] is DBNull)
                        {
                            result[_projectionDocumentSchema.Properties[i].PropertyName] = null;
                        }
                        // try to check whether the property is a json object or array
                        else if (
                            (_projectionDocumentSchema.Properties[i].IsNestedObject || _projectionDocumentSchema.Properties[i].IsNestedArray)
                            && values[i] is string
                        )
                        {
                            result[_projectionDocumentSchema.Properties[i].PropertyName] = 
                                JsonToObjectConverter.Convert((string)values[i], _projectionDocumentSchema.Properties[i]);
                        }
                        else
                        {
                            result[_projectionDocumentSchema.Properties[i].PropertyName] = values[i];
                        }
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
                    $"Something went terribly wrong and table for index \"{TableName}\" does not have one of the columns. Please delete that table and wait, it will be created automatically.",
                    ex
                );
            }
        }

        return null;
    }

    public async Task Delete(Guid id, string partitionKey, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentNullException(nameof(id));
        }

        if (string.IsNullOrEmpty(partitionKey))
        {
            throw new ArgumentNullException(nameof(partitionKey));
        }
        
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand(
            $"DELETE " +
            $" FROM \"{TableName}\" WHERE {KeyColumnName} = @id AND {nameof(ProjectionDocument.PartitionKey)} = @partitionKey", conn
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
            $" FROM \"{TableName}\" " +
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
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }
        
        if (string.IsNullOrEmpty(partitionKey))
        {
            throw new ArgumentNullException(nameof(partitionKey));
        }
        
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
            $"INSERT INTO \"{TableName}\" ({string.Join(',', propertyNames)}) " +
            $"VALUES ({string.Join(',', propertyNames.Select(p => $"@{p}"))}) " +
            $"ON CONFLICT ({KeyColumnName}) " +
            $"DO UPDATE SET {string.Join(',', propertyNames.Select(p => $"{p} = @{p}"))} "
            , conn
        );

        foreach (var p in _projectionDocumentSchema.Properties)
        {
            var propertyType = document[p.PropertyName]?.GetType();
            if (p.IsNestedObject || p.IsNestedArray)
            {
                document[p.PropertyName] = JsonSerializer.SerializeToDocument(document[p.PropertyName]);
            }

            cmd.Parameters.Add(new(p.PropertyName, document[p.PropertyName] ?? DBNull.Value));
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
                    $"Something went terribly wrong and table for index \"{TableName}\" does not have one of the columns. Please delete that table and wait, it will be created automatically.",
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
        if (projectionQuery == null)
        {
            throw new ArgumentNullException(nameof(projectionQuery));
        }
        
        var properties = _projectionDocumentSchema.Properties;

        var queryChunk = ConstructConditionFilters(projectionQuery.Filters);
        var fromStatements = new List<string>()
        {
            "\"" + TableName + "\""
        };
        
        fromStatements.AddRange(queryChunk.AdditionalFromSelects);
        
        var sb = new StringBuilder();
        sb.Append("SELECT ");
        sb.AppendJoin(',', properties.Select(p => p.PropertyName));
        sb.Append(" FROM ");
        sb.Append(string.Join(", ", fromStatements));
        sb.Append(" WHERE ");

        if (!string.IsNullOrEmpty(partitionKey))
        {
            queryChunk.WhereChunk += $" AND {nameof(ProjectionDocument.PartitionKey)} = @partitionKey";
            queryChunk.Parameters.Add(new("partitionKey", partitionKey));
        }

        if (!string.IsNullOrWhiteSpace(projectionQuery.SearchText) && projectionQuery.SearchText != "*")
        {
            (string searchQuery, NpgsqlParameter param) = ConstructSearchQuery(projectionQuery.SearchText);
            queryChunk.WhereChunk += (string.IsNullOrWhiteSpace(queryChunk.WhereChunk) ? $" {searchQuery}" : $" AND {searchQuery}");
            queryChunk.Parameters.Add(param);
        }

        sb.Append(queryChunk.WhereChunk);

        sb.Append(" GROUP BY id");

        sb.Append(" LIMIT @limit");
        queryChunk.Parameters.Add(new NpgsqlParameter("limit", projectionQuery.Limit));
        sb.Append(" OFFSET @offset");
        queryChunk.Parameters.Add(new NpgsqlParameter("offset", projectionQuery.Offset));
        
        if (projectionQuery.OrderBy.Count > 0)
        {
            sb.Append(" ORDER BY ");
            sb.AppendJoin(',', projectionQuery.OrderBy.Select(kv => $"{kv.Key} {kv.Value}"));
        }

        

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        var cmd = new NpgsqlCommand(sb.ToString(), conn);
        cmd.Parameters.AddRange(queryChunk.Parameters.ToArray());

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
                    if (values[i] is DBNull)
                    {
                        document[_projectionDocumentSchema.Properties[i].PropertyName] = null;
                    }
                    // try to check whether the property is a json object or array
                    else if (
                        (_projectionDocumentSchema.Properties[i].IsNestedObject || _projectionDocumentSchema.Properties[i].IsNestedArray) 
                        && values[i] is string
                    )
                    {
                        document[properties[i].PropertyName] = JsonToObjectConverter.Convert((string)values[i], properties[i]);
                    }
                    else
                    {
                        document[properties[i].PropertyName] = values[i];
                    }
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
                    $"Something went terribly wrong while querying \"{TableName}\".",
                    ex
                );
            }
        }

        return new List<Dictionary<string, object?>>();
    }

    private QueryChunk ConstructOneConditionFilter(Filter filter)
    {
        var queryChunk = new QueryChunk();
        
        var filterOperator = "";
        var propertyName = filter.PropertyName;
        var propertyParameterName = filter.PropertyName;

        if (string.IsNullOrEmpty(propertyName))
        {
            return queryChunk;
        }

        var nestedPath = propertyName.Split('.');
        if (nestedPath.Length > 1)
        {
            // Nested array check.
            // From query perspective both nested object and nested array item lookup look same: user.id = 1 or users.id = 1
            // so we need to use schema definition to find out whether it's an array. Because for arrays the query will be completely different. 
            var isArray = _projectionDocumentSchema.Properties
                .FirstOrDefault(p => p.PropertyName == nestedPath.First())?.IsNestedArray;

            if (isArray == true)
            {
                queryChunk.AdditionalFromSelects.Add(
                    $"jsonb_array_elements({nestedPath.First()}) with ordinality " +
                    $"{nestedPath.First()}_array({nestedPath.First()}_array_item, position)"
                );
                propertyName = $"{nestedPath.First()}_array.{nestedPath.First()}_array_item->>{string.Join("->>", nestedPath.Skip(1).Select(n => $"'{n}'"))}";
            }
            else
            {
                propertyName = $"{nestedPath.First()}->>{string.Join("->>", nestedPath.Skip(1).Select(n => $"'{n}'"))}";
            }

            propertyParameterName = string.Join("_", nestedPath);
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

        if (filter.Value is Guid)
        {
            propertyName = $"({propertyName})::uuid";
        } 
        else if (filter.Value is DateTime)
        {
            propertyName = $"({propertyName})::timestamp";
        }
        else if (filter.Value is int)
        {
            propertyName = $"({propertyName})::int";
        }
        else if (filter.Value is decimal)
        {
            propertyName = $"({propertyName})::decimal";
        }
        else if (filter.Value is float)
        {
            propertyName = $"({propertyName})::float";
        }

        queryChunk.WhereChunk = $"{propertyName} {filterOperator} @{propertyParameterName}";
        queryChunk.Parameters.Add(new NpgsqlParameter(propertyParameterName, filter.Value));

        return queryChunk;
    }

    private QueryChunk ConstructConditionFilter(Filter filter)
    {
        var queryChunk = new QueryChunk();

        var q = ConstructOneConditionFilter(filter);

        queryChunk.WhereChunk += q.WhereChunk;
        queryChunk.Parameters.AddRange(q.Parameters);
        queryChunk.AdditionalFromSelects.AddRange(q.AdditionalFromSelects);

        foreach (var f in filter.Filters)
        {
            if (!string.IsNullOrEmpty(q.WhereChunk))
            {
                queryChunk.WhereChunk += $" {f.Logic} ";
            }

            var wrapWithParentheses = f.Filter.Filters.Count > 0;

            if (wrapWithParentheses)
            {
                queryChunk.WhereChunk += "(";
            }

            var innerFilterQueryChunk = ConstructConditionFilter(f.Filter);
            queryChunk.WhereChunk += innerFilterQueryChunk.WhereChunk;

            if (wrapWithParentheses)
            {
                queryChunk.WhereChunk += ")";
            }
        }

        return queryChunk;
    }

    private QueryChunk ConstructConditionFilters(List<Filter> filters)
    {
        var queryChunk = new QueryChunk();
        
        var whereClauses = new List<string>();
        
        foreach (var f in filters)
        {
            var filterQueryChunk = ConstructConditionFilter(f);
            whereClauses.Add(filterQueryChunk.WhereChunk);
            queryChunk.Parameters.AddRange(filterQueryChunk.Parameters);
            queryChunk.AdditionalFromSelects.AddRange(filterQueryChunk.AdditionalFromSelects);
        }

        queryChunk.WhereChunk = string.Join(" AND ", whereClauses);
        return queryChunk;
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
            var exception = new Exception($"Failed to create a table for projection \"{TableName}\"", createTableException);
            exception.Data.Add("commandText", commandText);
            throw exception;
        }
    }

    private string ConstructCreateTableCommandText()
    {
        var commandText = new StringBuilder();
        commandText.AppendFormat("CREATE TABLE \"{0}\" (", TableName);

        var columnsSql = _projectionDocumentSchema.Properties
            .Select(ConstructColumnCreateStatementForProperty);

        commandText.Append(string.Join(',', columnsSql));

        commandText.AppendFormat(")");

        return commandText.ToString();
    }

    private static string ConstructColumnCreateStatementForProperty(ProjectionDocumentPropertySchema property)
    {
        string? column;

        if (property.IsNestedObject || property.IsNestedArray)
        {
            column = $"{property.PropertyName} jsonb";
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
                TypeCode.Object => "uuid",
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
                    $"Postgresql Projection Repository provider doesn't support type {property.PropertyType} for property {property.PropertyName}."
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
