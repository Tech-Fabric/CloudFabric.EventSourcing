using System.Text;
using System.Text.Json;
using CloudFabric.Projections.Exceptions;
using CloudFabric.Projections.Queries;
using CloudFabric.Projections.Utils;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace CloudFabric.Projections.Postgresql;

public class QueryChunk
{
    public string WhereChunk { get; set; } = "";
    public List<NpgsqlParameter> Parameters { get; init; } = new ();
    public List<string> AdditionalFromSelects { get; init; } = new();
}

public class PostgresqlProjectionRepository<TProjectionDocument> : PostgresqlProjectionRepository, IProjectionRepository<TProjectionDocument>
    where TProjectionDocument : ProjectionDocument
{
    public PostgresqlProjectionRepository(
        string connectionString, 
        ILoggerFactory loggerFactory,
        bool includeDebugInformation = false)
        : base(connectionString, ProjectionDocumentSchemaFactory.FromTypeWithAttributes<TProjectionDocument>(), loggerFactory, includeDebugInformation)
    {
    }

    public new async Task<TProjectionDocument?> Single(
        Guid id,
        string partitionKey, 
        CancellationToken cancellationToken = default,
        ProjectionOperationIndexSelector indexSelector = ProjectionOperationIndexSelector.ReadOnly
    ) {        
        if (id == Guid.Empty)
        {
            throw new ArgumentNullException(nameof(id));
        }

        if (string.IsNullOrEmpty(partitionKey))
        {
            throw new ArgumentNullException(nameof(partitionKey));
        }
        
        var document = await base.Single(id, partitionKey, cancellationToken, indexSelector);

        if (document == null) return null;

        return ProjectionDocumentSerializer.DeserializeFromDictionary<TProjectionDocument>(document);
    }

    public Task Upsert(
        TProjectionDocument document, 
        string partitionKey, 
        DateTime updatedAt, 
        CancellationToken cancellationToken = default,
        ProjectionOperationIndexSelector indexSelector = ProjectionOperationIndexSelector.Write
    ) {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }
        
        if (string.IsNullOrEmpty(partitionKey))
        {
            throw new ArgumentNullException(nameof(partitionKey));
        }
        
        var documentDictionary = ProjectionDocumentSerializer.SerializeToDictionary(document);
        return Upsert(documentDictionary, partitionKey, updatedAt, cancellationToken, indexSelector);
    }

    public new async Task<ProjectionQueryResult<TProjectionDocument>> Query(
        ProjectionQuery projectionQuery,
        string? partitionKey = null,
        CancellationToken cancellationToken = default,
        ProjectionOperationIndexSelector indexSelector = ProjectionOperationIndexSelector.ReadOnly
    )
    {
        if (projectionQuery == null)
        {
            throw new ArgumentNullException(nameof(projectionQuery));
        }
        
        var recordsDictionary = await base.Query(
            projectionQuery, partitionKey, cancellationToken, indexSelector
        );

        var records = new List<QueryResultDocument<TProjectionDocument>>();

        foreach (var doc in recordsDictionary.Records)
        {
            records.Add(
                new QueryResultDocument<TProjectionDocument>
                {
                    Document = ProjectionDocumentSerializer.DeserializeFromDictionary<TProjectionDocument>(doc.Document)
                }
            );
        }

        return new ProjectionQueryResult<TProjectionDocument>
        {
            DebugInformation = recordsDictionary.DebugInformation,
            IndexName = recordsDictionary.IndexName,
            TotalRecordsFound = recordsDictionary.TotalRecordsFound,
            Records = records
        };
    }
}

public class PostgresqlProjectionRepository : ProjectionRepository
{
    private bool _includeDebugInformation = false;

    private readonly string _connectionString;

    private string? _keyPropertyName;
    private string? _tableName;

    public PostgresqlProjectionRepository(
        string connectionString,
        ProjectionDocumentSchema projectionDocumentSchema,
        ILoggerFactory loggerFactory,
        bool includeDebugInformation = false
    ) : base(projectionDocumentSchema, loggerFactory.CreateLogger<ProjectionRepository>())
    {
        _connectionString = connectionString;
        _includeDebugInformation = includeDebugInformation;

        // for dynamic projection document schemas we need to ensure 'partitionKey' column is always there
        if (ProjectionDocumentSchema.Properties.All(p => p.PropertyName != "PartitionKey"))
        {
            ProjectionDocumentSchema.Properties.Add(new ProjectionDocumentPropertySchema()
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
                _tableName = ProjectionDocumentSchema.SchemaName;
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
                _keyPropertyName = ProjectionDocumentSchema.KeyColumnName;
            }

            return _keyPropertyName;
        }
    }
    
    protected override async Task CreateIndex(string indexName, ProjectionDocumentSchema projectionDocumentSchema)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        
        var commandText = ConstructCreateTableCommandText(indexName, projectionDocumentSchema);

        await using var createTableCommand = new NpgsqlCommand(commandText, conn);
        try
        {
            await createTableCommand.ExecuteNonQueryAsync();
        }
        catch (NpgsqlException ex)
        {
            if (ex.SqlState != PostgresErrorCodes.DuplicateTable) // table already created, can be ignored
            {
                throw;
            }
        }
        catch (Exception createTableException)
        {
            var exception = new Exception($"Failed to create a table for projection \"{TableName}\"", createTableException);
            exception.Data.Add("commandText", commandText);
            throw exception;
        }
    }

    public override async Task<Dictionary<string, object?>?> Single(
        Guid id, 
        string partitionKey, 
        CancellationToken cancellationToken = default,
        ProjectionOperationIndexSelector indexSelector = ProjectionOperationIndexSelector.ReadOnly
    ) {
        if (id == Guid.Empty)
        {
            throw new ArgumentNullException(nameof(id));
        }

        if (string.IsNullOrEmpty(partitionKey))
        {
            throw new ArgumentNullException(nameof(partitionKey));
        }
        
        var indexDescriptor = await GetIndexDescriptorForOperation(indexSelector, cancellationToken);
        
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        if (indexDescriptor.ProjectionDocumentSchema.Properties.Count <= 0)
        {
            throw new ArgumentException(
                "Projection document schema has no properties",
                indexDescriptor.ProjectionDocumentSchema.SchemaName
            );
        }

        await using var cmd = new NpgsqlCommand(
            $"SELECT " +
            string.Join(',', indexDescriptor.ProjectionDocumentSchema.Properties.Select(p => p.PropertyName)) + " " +
            $"FROM \"{indexDescriptor.IndexName}\" " +
            $"WHERE {KeyColumnName} = @id AND {nameof(ProjectionDocument.PartitionKey)} = @partitionKey " +
            $"LIMIT 1", conn
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
                    var values = new object[indexDescriptor.ProjectionDocumentSchema.Properties.Count];
                    reader.GetValues(values);

                    if (values.Length <= 0)
                    {
                        return null;
                    }

                    for (var i = 0; i < indexDescriptor.ProjectionDocumentSchema.Properties.Count; i++)
                    {
                        if (values[i] is DBNull)
                        {
                            result[indexDescriptor.ProjectionDocumentSchema.Properties[i].PropertyName] = null;
                        }
                        // try to check whether the property is a json object or array
                        else if (
                            (indexDescriptor.ProjectionDocumentSchema.Properties[i].IsNestedObject || indexDescriptor.ProjectionDocumentSchema.Properties[i].IsNestedArray)
                            && values[i] is string
                        )
                        {
                            result[indexDescriptor.ProjectionDocumentSchema.Properties[i].PropertyName] = 
                                JsonToObjectConverter.Convert((string)values[i], indexDescriptor.ProjectionDocumentSchema.Properties[i]);
                        }
                        else
                        {
                            result[indexDescriptor.ProjectionDocumentSchema.Properties[i].PropertyName] = values[i];
                        }
                    }
                }

                return result;
            }

            return null;
        }
        catch (NpgsqlException ex)
        {
            if (ex.SqlState == PostgresErrorCodes.UndefinedTable || ex.SqlState == PostgresErrorCodes.UndefinedColumn)
            {
                throw new InvalidProjectionSchemaException(ex);
            }
            else 
            {
                throw new Exception(
                    $"Something went terribly wrong while updating/inserting document in \"{TableName}\".",
                    ex
                );
            }
        }

        return null;
    }

    public override async Task Delete(
        Guid id, 
        string partitionKey, 
        CancellationToken cancellationToken = default,
        ProjectionOperationIndexSelector indexSelector = ProjectionOperationIndexSelector.Write
    ) {
        if (id == Guid.Empty)
        {
            throw new ArgumentNullException(nameof(id));
        }

        if (string.IsNullOrEmpty(partitionKey))
        {
            throw new ArgumentNullException(nameof(partitionKey));
        }
        
        var indexDescriptor = await GetIndexDescriptorForOperation(indexSelector, cancellationToken);
        
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand(
            $"DELETE " +
            $" FROM \"{indexDescriptor.IndexName}\" WHERE {KeyColumnName} = @id AND {nameof(ProjectionDocument.PartitionKey)} = @partitionKey", conn
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

    public override async Task DeleteAll(
        string? partitionKey = null, 
        CancellationToken cancellationToken = default,
        ProjectionOperationIndexSelector indexSelector = ProjectionOperationIndexSelector.Write
    ) {
        var indexState = await GetProjectionIndexState(cancellationToken);

        if (indexState == null)
        {
            return;
        }

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        
        foreach (var indexStatus in indexState.IndexesStatuses)
        {
            if (partitionKey == null)
            {
                await using var dropTableCmd = new NpgsqlCommand(
                    $"DROP TABLE IF EXISTS \"{indexStatus.IndexName}\" ", conn
                );
                await dropTableCmd.ExecuteNonQueryAsync(cancellationToken);
            }
            else
            {
                await using var cmd = new NpgsqlCommand(
                    $"DELETE " +
                    $" FROM \"{indexStatus.IndexName}\" " +
                    (!string.IsNullOrEmpty(partitionKey) ? $" WHERE {nameof(ProjectionDocument.PartitionKey)} = @partitionKey" : ""), conn
                );

                if (!string.IsNullOrEmpty(partitionKey))
                {
                    cmd.Parameters.Add(new("partitionKey", partitionKey));
                }

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        indexState.IndexesStatuses.Clear();
        await SaveProjectionIndexState(indexState);
        //
        // await using var dropIndexTableCmd = new NpgsqlCommand(
        //     $"DROP TABLE \"{PROJECTION_INDEX_STATE_INDEX_NAME}\" ", conn
        // );
        // await dropIndexTableCmd.ExecuteNonQueryAsync(cancellationToken);
    }

    protected override async Task UpsertInternal(
        ProjectionOperationIndexDescriptor indexDescriptor,
        Dictionary<string, object?> document, 
        string partitionKey, 
        DateTime updatedAt,
        CancellationToken cancellationToken = default
    ) {
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

        if (indexDescriptor.ProjectionDocumentSchema.Properties.Count <= 0)
        {
            throw new ArgumentException(
                "Projection document schema has no properties",
                indexDescriptor.ProjectionDocumentSchema.SchemaName
            );
        }

        document[nameof(ProjectionDocument.PartitionKey)] = partitionKey;
        document[nameof(ProjectionDocument.UpdatedAt)] = updatedAt;
        
        var propertiesToInsert = indexDescriptor.ProjectionDocumentSchema.Properties
            .Where(p => document.Keys.Contains(p.PropertyName)).ToList(); // document may not contain non-required properties,
                                                                          // we need to exclude them from query
        
        var propertyNames = propertiesToInsert
            .Select(p => p.PropertyName)
            .ToArray();

        await using var cmd = new NpgsqlCommand(
            $"INSERT INTO \"{indexDescriptor.IndexName}\" ({string.Join(',', propertyNames)}) " +
            $"VALUES ({string.Join(',', propertyNames.Select(p => $"@{p}"))}) " +
            $"ON CONFLICT ({indexDescriptor.ProjectionDocumentSchema.KeyColumnName}) " +
            $"DO UPDATE SET {string.Join(',', propertyNames.Select(p => $"{p} = @{p}"))} "
            , conn
        );

        foreach (var p in propertiesToInsert)
        {
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
            if (ex.SqlState == PostgresErrorCodes.UndefinedTable || ex.SqlState == PostgresErrorCodes.UndefinedColumn)
            {
                throw new InvalidProjectionSchemaException(ex);
            }
            else 
            {
                throw new Exception(
                    $"Something went terribly wrong while updating/inserting document in \"{TableName}\".",
                    ex
                );
            }
        }
    }
    
    protected override async Task<ProjectionQueryResult<Dictionary<string, object?>>> QueryInternal(
        ProjectionOperationIndexDescriptor indexDescriptor,
        ProjectionQuery projectionQuery,
        string? partitionKey = null,
        CancellationToken cancellationToken = default
    )
    {
        if (projectionQuery == null)
        {
            throw new ArgumentNullException(nameof(projectionQuery));
        }

        indexDescriptor.ProjectionDocumentSchema ??= ProjectionDocumentSchema;

        var properties = indexDescriptor.ProjectionDocumentSchema.Properties;

        var queryChunk = ConstructConditionFilters(projectionQuery.Filters, indexDescriptor.ProjectionDocumentSchema);

        // need to wrap whole filters where statement in brackets for all other ANDs to work properly
        // see partitionKey and search statements below
        //queryChunk.WhereChunk = $"({queryChunk.WhereChunk})";
        
        var fromStatements = new List<string>()
        {
            "\"" + indexDescriptor.IndexName + "\""
        };
        
        fromStatements.AddRange(queryChunk.AdditionalFromSelects);
        
        var sb = new StringBuilder();
        sb.Append("SELECT ");
        sb.AppendJoin(',', properties.Select(p => p.PropertyName));
        sb.Append(" FROM ");
        sb.Append(string.Join(", ", fromStatements));

        if (!string.IsNullOrEmpty(partitionKey))
        {
            queryChunk.WhereChunk += string.IsNullOrWhiteSpace(queryChunk.WhereChunk)
                ? $" {nameof(ProjectionDocument.PartitionKey)} = @partitionKey"
                : $" AND {nameof(ProjectionDocument.PartitionKey)} = @partitionKey";
            queryChunk.Parameters.Add(new("partitionKey", partitionKey));
        }

        if (!string.IsNullOrWhiteSpace(projectionQuery.SearchText) && projectionQuery.SearchText != "*")
        {
            (string searchQuery, NpgsqlParameter param) = ConstructSearchQuery(projectionQuery.SearchText, indexDescriptor.ProjectionDocumentSchema);
            queryChunk.WhereChunk += string.IsNullOrWhiteSpace(queryChunk.WhereChunk) ? $" {searchQuery}" : $" AND {searchQuery}";
            queryChunk.Parameters.Add(param);
        }

        if (!string.IsNullOrEmpty(queryChunk.WhereChunk))
        {
            sb.Append(" WHERE ");
            sb.Append(queryChunk.WhereChunk);
        }

        sb.Append(" GROUP BY id");

        // total count query
        string totalCountQuery = $"SELECT COUNT(*) FROM {string.Join(", ", fromStatements)}";
        if (!string.IsNullOrEmpty(queryChunk.WhereChunk))
        {
            totalCountQuery += $" WHERE {queryChunk.WhereChunk}";
        }
        
        NpgsqlParameter[] totalCountParams = new NpgsqlParameter[queryChunk.Parameters.Count];
        queryChunk.Parameters.CopyTo(totalCountParams);
        
        if (projectionQuery.OrderBy.Count > 0)
        {
            // NOTE: nested sorting is not implemented
            sb.Append(" ORDER BY ");
            sb.AppendJoin(',', projectionQuery.OrderBy.Select(kv => $"{kv.KeyPath} {kv.Order}"));
        }
        
        if (projectionQuery.Limit.HasValue)
        {
            sb.Append(" LIMIT @limit");
            queryChunk.Parameters.Add(new NpgsqlParameter("limit", projectionQuery.Limit.Value));
        }
        
        sb.Append(" OFFSET @offset");
        queryChunk.Parameters.Add(new NpgsqlParameter("offset", projectionQuery.Offset));

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        
        try
        {
            // calculate total count
            var totalCountCmd = new NpgsqlCommand(totalCountQuery, conn);
            totalCountCmd.Parameters.AddRange(totalCountParams);

            var totalCount = await totalCountCmd.ExecuteScalarAsync(cancellationToken) as long?;
            totalCountCmd.Parameters.Clear();
            totalCountCmd.Dispose();

            var cmd = new NpgsqlCommand(sb.ToString(), conn);
            cmd.Parameters.AddRange(queryChunk.Parameters.ToArray());

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
                        document[indexDescriptor.ProjectionDocumentSchema.Properties[i].PropertyName] = null;
                    }
                    // try to check whether the property is a json object or array
                    else if (
                        (indexDescriptor.ProjectionDocumentSchema.Properties[i].IsNestedObject || indexDescriptor.ProjectionDocumentSchema.Properties[i].IsNestedArray) 
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

            var debugInformation = "";

            if (_includeDebugInformation)
            {
                debugInformation += cmd.CommandText;
                
                foreach (NpgsqlParameter param in cmd.Parameters)
                {
                    var paramValue = "";

                    switch (param.NpgsqlDbType)
                    {
                        case NpgsqlDbType.Text:
                            paramValue = $"'{param.NpgsqlValue}'";
                            break;
                        default: 
                            paramValue = param.NpgsqlValue.ToString();
                            break;
                    }

                    debugInformation = debugInformation.Replace($"@{param.ParameterName}", paramValue);
                }
                
                debugInformation += $"\n\nOriginal command:\n{cmd.CommandText}; " +
                    $"{string.Join(',', cmd.Parameters.Select(p => $"@{p.ParameterName}:{p.NpgsqlDbType}={p.NpgsqlValue}"))}";
            }

            await reader.DisposeAsync();
            // clear previous command in order to prevent conflicts
            cmd.Parameters.Clear();
            cmd.Dispose();
            
            return new ProjectionQueryResult<Dictionary<string, object?>>
            {
                DebugInformation = _includeDebugInformation ? debugInformation : String.Empty, 
                IndexName = TableName,
                TotalRecordsFound = totalCount,
                Records = records.Select(x =>
                    new QueryResultDocument<Dictionary<string, object?>>
                    {
                        Document = x
                    }
                ).ToList()
            };
        }
        catch (NpgsqlException ex)
        {
            if (ex.SqlState == PostgresErrorCodes.UndefinedTable || ex.SqlState == PostgresErrorCodes.UndefinedColumn)
            {
                throw new InvalidProjectionSchemaException(ex);
            }
            else 
            {
                throw new Exception(
                    $"Something went terribly wrong while querying \"{TableName}\".",
                    ex
                );
            }
        }
    }
    
    private QueryChunk ConstructOneConditionFilter(Filter filter, ProjectionDocumentSchema schema)
    {
        var queryChunk = new QueryChunk();
        
        var filterOperator = "";
        var propertyName = filter.PropertyName;
        var propertyParameterName = filter.PropertyName;

        if (string.IsNullOrEmpty(propertyName) || propertyName == "*")
        {
            return queryChunk;
        }

        var nestedPath = propertyName.Split('.');

        var propertySchema = schema.Properties.FirstOrDefault(p => p.PropertyName == nestedPath.First());
        if (propertySchema == null)
        {
            Logger.LogWarning("Bad filter: schema {SchemaName} does not have property {PropertyName}", schema.SchemaName, propertyName);
            return queryChunk;
        }
        
        // Nested array check.
        // From query perspective both nested object and nested array item lookup look same: user.id = 1 or users.id = 1
        // so we need to use schema definition to find out whether it's an array. Because for arrays the query will be completely different. 
        var isArray = propertySchema.IsNestedArray;

        if (nestedPath.Length > 1)
        {
            propertySchema = propertySchema.NestedObjectProperties.First(p => p.PropertyName == nestedPath[1]);
            
            if (isArray == true)
            {
                // TODO: it's only working with one level of depth right now :(
                var fromSelect = $"jsonb_array_elements({nestedPath.First()}) with ordinality " +
                                 $"{nestedPath.First()}_array({nestedPath.First()}_array_item, position)";
                queryChunk.AdditionalFromSelects.Add(fromSelect);    
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
                filterOperator = filter.Value == null ? "IS" : "=";
                break;
            case FilterOperator.NotEqual:
                filterOperator = filter.Value == null ? "IS NOT" : "!=";
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
            case FilterOperator.StartsWith:
            case FilterOperator.EndsWith:
            case FilterOperator.Contains:
                filterOperator = "LIKE";
                break;
            case FilterOperator.StartsWithIgnoreCase:
            case FilterOperator.EndsWithIgnoreCase:
            case FilterOperator.ContainsIgnoreCase:
                filterOperator = "ILIKE";
                break;
            case FilterOperator.ArrayContains:
                filterOperator = "?";
                break;
        }
        
        var npgsqlParameter = new NpgsqlParameter(propertyParameterName, filter.Value ?? DBNull.Value);

        if (filter.Value is Guid)
        {
            propertyName = $"({propertyName})::uuid";
        } 
        else if (propertySchema.PropertyType == TypeCode.DateTime)
        {
            propertyName = $"({propertyName})::timestamp with time zone";
            npgsqlParameter.NpgsqlDbType = NpgsqlDbType.TimestampTz;
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
        
        queryChunk.WhereChunk = $"{propertyName} {filterOperator} ";

        if (filter.Value == null)
        {
            queryChunk.WhereChunk += "NULL";
        } 
        else
        {
            switch (filter.Operator)
            {
                case FilterOperator.StartsWithIgnoreCase:
                case FilterOperator.StartsWith:
                    queryChunk.WhereChunk += $"@{propertyParameterName} || '%'";
                    break;
                case FilterOperator.EndsWithIgnoreCase:
                case FilterOperator.EndsWith:
                    queryChunk.WhereChunk += $"'%' || @{propertyParameterName}";
                    break;
                case FilterOperator.ContainsIgnoreCase:
                case FilterOperator.Contains:
                    if (isArray == true)
                    {
                        // This is due to the fact that we don't always have access to the schema when building filters.
                        // For example, when generating linq expressions from filters it's not possible to know whether filterable property is array or string.
                        // Hence the need to have separate operators - `Contains` for strings and `ArrayContains` for arrays.  
                        throw new ArgumentException("Please use ArrayContains instead.");
                    }

                    queryChunk.WhereChunk += $"'%' || @{propertyParameterName} || '%'";
                    break;
                default:
                    queryChunk.WhereChunk += $"@{propertyParameterName}";
                    break;
            }

            queryChunk.Parameters.Add(npgsqlParameter);
        }

        return queryChunk;
    }

    private QueryChunk ConstructConditionFilter(Filter filter, ProjectionDocumentSchema schema)
    {
        var queryChunk = new QueryChunk();

        var q = ConstructOneConditionFilter(filter, schema);

        queryChunk.WhereChunk += q.WhereChunk;
        queryChunk.Parameters.AddRange(q.Parameters);
        queryChunk.AdditionalFromSelects.AddRange(q.AdditionalFromSelects);

        foreach (var f in filter.Filters)
        {
            if (!string.IsNullOrEmpty(queryChunk.WhereChunk))
            {
                queryChunk.WhereChunk += $" {f.Logic} ";
            }

            var wrapWithParentheses = f.Filter.Filters.Count > 0;

            if (wrapWithParentheses)
            {
                queryChunk.WhereChunk += "(";
            }

            var innerFilterQueryChunk = ConstructConditionFilter(f.Filter, schema);
            queryChunk.WhereChunk += innerFilterQueryChunk.WhereChunk;
            queryChunk.Parameters.AddRange(innerFilterQueryChunk.Parameters);

            if (wrapWithParentheses)
            {
                queryChunk.WhereChunk += ")";
            }
        }

        return queryChunk;
    }

    private QueryChunk ConstructConditionFilters(List<Filter> filters, ProjectionDocumentSchema schema)
    {
        var queryChunk = new QueryChunk();
        
        var whereClauses = new List<string>();
        
        foreach (var f in filters)
        {
            var filterQueryChunk = ConstructConditionFilter(f, schema);
            whereClauses.Add(filterQueryChunk.WhereChunk);
            queryChunk.Parameters.AddRange(filterQueryChunk.Parameters);
            //Don't add duplicates
            queryChunk.AdditionalFromSelects.AddRange(filterQueryChunk.AdditionalFromSelects
                .Except(queryChunk.AdditionalFromSelects));
        }

        queryChunk.WhereChunk = string.Join(" AND ", whereClauses);
        return queryChunk;
    }

    private (string, NpgsqlParameter) ConstructSearchQuery(string searchText, ProjectionDocumentSchema schema)
    {
        // TODO: add search inside nexted jsonb columns
        var searchableProperties = schema.Properties.Where(x => x.IsSearchable);

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

    private string ConstructCreateTableCommandText(string tableName, ProjectionDocumentSchema schema)
    {
        var commandText = new StringBuilder();
        commandText.AppendFormat("CREATE TABLE \"{0}\" (", tableName);

        var columnsSql = schema.Properties
            .Select(ConstructColumnCreateStatementForProperty);

        commandText.Append(string.Join(',', columnsSql));

        commandText.AppendFormat(")");
        
        // TODO: add indexes for properties that have IsFilterable = true

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
                TypeCode.DateTime => "timestamp with time zone", // This does not mean it stores timezone information, it only stores UTC,
                                                                 // read more here: https://www.npgsql.org/doc/types/datetime.html#timestamps-and-timezones
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