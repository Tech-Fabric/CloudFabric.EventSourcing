namespace CloudFabric.Projections;

public abstract class ProjectionRepositoryFactory
{
    public abstract IProjectionRepository<TProjectionDocument> GetProjectionRepository<TProjectionDocument>()
        where TProjectionDocument : ProjectionDocument;

    public abstract IProjectionRepository GetProjectionRepository(ProjectionDocumentSchema projectionDocumentSchema);
}
