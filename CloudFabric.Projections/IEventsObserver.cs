using CloudFabric.EventSourcing.EventStore;

namespace CloudFabric.Projections;

public interface IEventsObserver
{
    public Task StartAsync(string instanceName);

    public Task StopAsync();

    public void SetEventHandler(Func<IEvent, Task> eventHandler);

    public Task LoadAndHandleEventsForDocumentAsync(string documentId, string partitionKey);

    // onCompleted has instanceName as a parameter, onError - instanceName and errorMessage
    public Task LoadAndHandleEventsAsync(string instanceName, string partitionKey, DateTime? dateFrom, Func<string, string, Task> onCompleted, Func<string, string, string, Task> onError);
}