namespace CloudFabric.Projections;

public interface IHandleEvent<TEvent>
{
    Task On(TEvent @event);
}