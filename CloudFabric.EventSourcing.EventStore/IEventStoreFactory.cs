namespace CloudFabric.EventSourcing.EventStore;

public interface IEventStoreFactory
{
    IEventStore CreateEventStore(
        IEventStoreConnectionInformationProvider connectionInformationProvider,
        string? connectionId = null
    );
}
