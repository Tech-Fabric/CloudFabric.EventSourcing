namespace CloudFabric.Projections;

public class ProjectionDocumentSchema
{
    public string SchemaName { get; set; }

    /// <summary>
    /// Allows to narrow down
    /// the amount of event store events to process when rebuilding the projection.
    ///
    /// When set, projection rebuild will only process events for given partition key. 
    /// </summary>
    public string? SourcePartitionKey { get; set; }

    public string KeyColumnName
    {
        get
        {
            var columnName = Properties.FirstOrDefault(p => p.IsKey)?.PropertyName;
            if (string.IsNullOrEmpty(columnName))
            {
                throw new Exception(
                    $"ProjectionDocumentSchema {SchemaName} does not define a property with IsKey = true. Property with IsKey = true should always exist on projection documents."
                );
            }

            return columnName;
        }
    }

    public List<ProjectionDocumentPropertySchema> Properties { get; set; } = new();
}