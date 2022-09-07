using CloudFabric.EventSourcing.EventStore;

namespace CloudFabric.Projections;

public interface IEventsObserver
{
    public Task StartAsync(string instanceName);

    public Task StopAsync();

    public void SetEventHandler(Func<IEvent, Task> eventHandler);

    public Task LoadAndHandleEventsForDocumentAsync(string documentId);

    // onCompleted has instanceName as a parameter, onError - instanceName and errorMessage
    public Task LoadAndHandleEventsAsync(string instanceName, DateTime? dateFrom, Func<string, Task> onCompleted, Func<string, string, Task> onError);
}