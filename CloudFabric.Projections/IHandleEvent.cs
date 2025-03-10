namespace CloudFabric.Projections;

public interface IHandleEvent<in TEvent>
{
    Task On(TEvent evt);
}