namespace CloudFabric.Projections.InMemory;

public class InMemoryProjectionRepositoryFactory : ProjectionRepositoryFactory
{
    public InMemoryProjectionRepositoryFactory()
    {
    }

    public override IProjectionRepository<TProjectionDocument> GetProjectionRepository<TProjectionDocument>()
    {
        return new InMemoryProjectionRepository<TProjectionDocument>();
    }

    public override IProjectionRepository GetProjectionRepository(ProjectionDocumentSchema projectionDocumentSchema)
    {
        return new InMemoryProjectionRepository(projectionDocumentSchema);
    }
}
