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

    public async System.Threading.Tasks.Task On(UserAccountRegistered @event, string partitionKey)
    {
        await UpsertDocument(new UserAccountsProjectionItem()
        {
            Id = @event.Id,
            FirstName = @event.FirstName,
        }, partitionKey);
    }

    public System.Threading.Tasks.Task On(UserAccountEmailAddressChanged @event, string partitionKey)
    {
        throw new NotImplementedException();
    }

    public System.Threading.Tasks.Task On(UserAccountEmailAddressConfirmed @event, string partitionKey)
    {
        throw new NotImplementedException();
    }

    public async System.Threading.Tasks.Task On(UserAccountEmailAssigned @event, string partitionKey)
    {
        await UpdateDocument(
            @event.UserAccountId,
            partitionKey,
            (projectionDocument) =>
            {
                projectionDocument.EmailAddress = @event.EmailAddress;
            }
        );
    }

    public async System.Threading.Tasks.Task On(UserAccountEmailRegistered @event, string partitionKey)
    {
        throw new NotImplementedException();
    }

    public async System.Threading.Tasks.Task On(UserAccountPasswordUpdated @event, string partitionKey)
    {
        throw new NotImplementedException();
    }
}
