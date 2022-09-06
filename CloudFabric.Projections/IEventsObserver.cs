using CloudFabric.EventSourcing.EventStore;

namespace CloudFabric.Projections;

public interface IEventsObserver
{
    public Task StartAsync(string instanceName);

    public Task StopAsync();

    public void SetEventHandler(Func<IEvent, Task> eventHandler);

    public Task LoadAndHandleEventsForDocumentAsync(string documentId);

    public Task LoadAndHandleEventsAsync(string instanceName, DateTime? dateFrom);
}