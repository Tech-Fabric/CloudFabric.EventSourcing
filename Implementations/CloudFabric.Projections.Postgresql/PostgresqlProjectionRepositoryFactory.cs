namespace CloudFabric.Projections.Postgresql;

public class PostgresqlProjectionRepositoryFactory: ProjectionRepositoryFactory
{
    private readonly string _connectionString;
    
    public PostgresqlProjectionRepositoryFactory(string connectionString)
    {
        _connectionString = connectionString;
    }
    
    public override IProjectionRepository<TProjectionDocument> GetProjectionRepository<TProjectionDocument>()
    {
        return new PostgresqlProjectionRepository<TProjectionDocument>(_connectionString);
    }

    public override IProjectionRepository GetProjectionRepository(ProjectionDocumentSchema projectionDocumentSchema)
    {
        return new PostgresqlProjectionRepository(_connectionString, projectionDocumentSchema);
    }
}
