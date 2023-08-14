namespace CloudFabric.EventSourcing.EventStore;

/// <summary>
/// EventStore connection information. Should be extended by event store implementations.
/// </summary>
public class EventStoreConnectionInformation
{
    /// <summary>
    /// Unique connection identifier. Used to find correct connection based on application database load balancing rules.
    /// This can be thought of as a PartitionKey.
    /// For example, this may be a name of a tenant in multi-tenant application. In such case, IPostgresqlEventStoreConnectionInformationProvider will
    /// return same connection information for all calls with same connectionId argument.
    ///
    /// More detailed example: in multi-tenant application, we may want to design a custom IPostgresqlEventStoreConnectionInformationProvider which will
    /// return connection string with host=%tenant_name%.mydbhost.com, replacing %tenant_name% with tenant name from user's token claims or something else.
    /// This will allow the system to use multiple databases on multiple hosts but one application deployment. In this scenario, the application may need
    /// to restore the connection to tenant database in the background, which means the connection string will need to be stored somewhere and restored back
    /// when needed. For this, every time IEventStoreConnectionInformationProvider returns a connection information, it has to generate unique connection id
    /// with connection information but without secure data. Later, when needed, application will call the same GetConnectionInformation method but providing
    /// the connection id. Returned connection information should be the same for same connection id.
    /// </summary>
    public string ConnectionId { get; set; }
}

public interface IEventStoreConnectionInformationProvider
{
    /// <summary>
    /// Should return database connection information. It is assumed that derived class accepts something in it's constructor which helps constructing
    /// connection information. For example, a provider may accept HttpContext in it's constructor and then in this method extract tenant id or user name from
    /// user token claims and return database name equal to tenant id or user name. 
    /// </summary>
    /// <param name="connectionId">
    /// Encoded connection identifier. Used to re-construct previously returned connection information.
    /// When provided, the implementation should return the same connection information
    /// as it returned previously with this connectionId.</param>
    /// <returns></returns>
    EventStoreConnectionInformation GetConnectionInformation(string? connectionId = null);
}
