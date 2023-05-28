namespace CloudFabric.Projections.Exceptions;

public class IndexNotReadyException : Exception
{
    public IndexNotReadyException(ProjectionIndexState indexState)
        : base(
            "Projection index is not ready yet. Please try again later. "
        )
    {
        Data["IndexState"] = indexState;
    }
}
