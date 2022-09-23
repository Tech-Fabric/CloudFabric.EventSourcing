using ToDoList.Domain.Events.UserAccounts;
using CloudFabric.Projections;

namespace ToDoList.Domain.Projections.UserAccounts;


public class UserAccountsProjectionBuilder : ProjectionBuilder<UserAccountsProjectionItem>,
    IHandleEvent<UserAccountRegistered>,
    IHandleEvent<UserAccountEmailAddressChanged>,
    IHandleEvent<UserAccountEmailAddressConfirmed>,
    IHandleEvent<UserAccountEmailAssigned>,
    IHandleEvent<UserAccountEmailRegistered>,
    IHandleEvent<UserAccountPasswordUpdated>
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

    public System.Threading.Tasks.Task On(UserAccountEmailAddressChanged @event)
    {
        throw new NotImplementedException();
    }

    public System.Threading.Tasks.Task On(UserAccountEmailAddressConfirmed @event)
    {
        throw new NotImplementedException();
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

    public async System.Threading.Tasks.Task On(UserAccountEmailRegistered @event)
    {
        throw new NotImplementedException();
    }

    public async System.Threading.Tasks.Task On(UserAccountPasswordUpdated @event)
    {
        throw new NotImplementedException();
    }
}
