namespace CloudFabric.Projections.Exceptions;

public class InvalidProjectionSchemaException : Exception
{
    public InvalidProjectionSchemaException(Exception innerException)
        : base(
            "Projection is in invalid state: either index is not created or missing some fields. " +
            "Make sure to run EnsureIndex() method on projection repository",
            innerException
        )
    {
    }
}
