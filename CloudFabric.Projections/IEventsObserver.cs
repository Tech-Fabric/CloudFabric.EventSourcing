using CloudFabric.EventSourcing.EventStore;

namespace CloudFabric.Projections;

public interface IEventsObserver
{
    public Task StartAsync(string instanceName);

    public Task StopAsync();

    public void SetEventHandler(Func<IEvent, Task> eventHandler);

    public Task LoadAndHandleEventsForDocumentAsync(Guid documentId, string partitionKey);

    // onCompleted has instanceName and partitionKey as parameters, onError - instanceName, partitionKey and errorMessage
    public Task LoadAndHandleEventsAsync(string instanceName, string partitionKey, DateTime? dateFrom, Func<string, string, Task> onCompleted, Func<string, string, string, Task> onError);
}