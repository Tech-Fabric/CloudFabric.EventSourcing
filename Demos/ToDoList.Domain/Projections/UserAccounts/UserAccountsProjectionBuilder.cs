using ToDoList.Domain.Events.UserAccounts;
using CloudFabric.Projections;

namespace ToDoList.Domain.Projections.UserAccounts;


public class UserAccountsProjectionBuilder : ProjectionBuilder<UserAccountsProjectionItem>,
    IHandleEvent<UserAccountRegistered>,
    IHandleEvent<UserAccountEmailAddressChanged>,
    IHandleEvent<UserAccountEmailAddressConfirmed>,
    IHandleEvent<UserAccountEmailAssigned>
{
    public UserAccountsProjectionBuilder(IProjectionRepository<UserAccountsProjectionItem> repository) : base(repository)
    {
    }

    public async System.Threading.Tasks.Task On(UserAccountRegistered @event)
    {
        await UpsertDocument(new UserAccountsProjectionItem()
        {
            Id = @event.Id,
            FirstName = @event.FirstName,
        }, @event.PartitionKey);
    }

    public async System.Threading.Tasks.Task On(UserAccountEmailAddressChanged @event)
    {
        await UpdateDocument(
            @event.UserAccountId,
            @event.PartitionKey,
            (projectionDocument) =>
            {
                projectionDocument.EmailAddress = @event.NewEmail;
            }
        );
    }

    public async System.Threading.Tasks.Task On(UserAccountEmailAddressConfirmed @event)
    {
        await UpdateDocument(
            @event.UserAccountId,
            @event.PartitionKey,
            (projectionDocument) =>
            {
                projectionDocument.EmailConfirmedAt = DateTime.UtcNow;
            }
        );
    }

    public async System.Threading.Tasks.Task On(UserAccountEmailAssigned @event)
    {
        await UpdateDocument(
            @event.UserAccountId,
            @event.PartitionKey,
            (projectionDocument) =>
            {
                projectionDocument.EmailAddress = @event.EmailAddress;
            }
        );
    }
}
