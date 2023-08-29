using ToDoList.Domain.Events.UserAccounts;
using CloudFabric.Projections;

namespace ToDoList.Domain.Projections.UserAccounts;


public class UserAccountsProjectionBuilder : ProjectionBuilder<UserAccountsProjectionItem>,
    IHandleEvent<UserAccountRegistered>,
    IHandleEvent<UserAccountEmailAddressChanged>,
    IHandleEvent<UserAccountEmailAddressConfirmed>,
    IHandleEvent<UserAccountEmailAssigned>
{
    public UserAccountsProjectionBuilder(
        ProjectionRepositoryFactory projectionRepositoryFactory,
        ProjectionOperationIndexSelector indexSelector) : base(projectionRepositoryFactory, indexSelector)
    {
    }

    public async System.Threading.Tasks.Task On(UserAccountRegistered @event)
    {
        await UpsertDocument(
            new UserAccountsProjectionItem()
            {
                Id = @event.AggregateId,
                FirstName = @event.FirstName,
            },
            @event.PartitionKey,
            @event.Timestamp
        );
    }

    public async System.Threading.Tasks.Task On(UserAccountEmailAddressChanged @event)
    {
        await UpdateDocument(
            @event.AggregateId,
            @event.PartitionKey,
            @event.Timestamp,
            (projectionDocument) =>
            {
                projectionDocument.EmailAddress = @event.NewEmail;
            }
        );
    }

    public async System.Threading.Tasks.Task On(UserAccountEmailAddressConfirmed @event)
    {
        await UpdateDocument(
            @event.AggregateId,
            @event.PartitionKey,
            @event.Timestamp,
            (projectionDocument) =>
            {
                projectionDocument.EmailConfirmedAt = DateTime.UtcNow;
            }
        );
    }

    public async System.Threading.Tasks.Task On(UserAccountEmailAssigned @event)
    {
        await UpdateDocument(
            @event.AggregateId,
            @event.PartitionKey,
            @event.Timestamp,
            (projectionDocument) =>
            {
                projectionDocument.EmailAddress = @event.EmailAddress;
            }
        );
    }
    
    public async System.Threading.Tasks.Task On(AggregateUpdatedEvent<UserAccount> @event)
    {
        await SetDocumentUpdatedAt(@event.AggregateId, @event.PartitionKey, @event.UpdatedAt);
    }
}