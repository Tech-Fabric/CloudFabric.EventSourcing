using CloudFabric.EventSourcing.EventStore;

namespace CloudFabric.Projections;

public interface IEventsObserver
{
    public Task StartAsync(string instanceName);

    public Task StopAsync();

    public void SetEventHandler(Func<IEvent, Task<bool>> eventHandler);
}