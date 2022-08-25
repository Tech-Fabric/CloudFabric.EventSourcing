using CloudFabric.EventSourcing.EventStore.Persistence;

namespace ToDoList.Api.Extensions;

public static class EventUserInfoServiceCollectionExtensions
{
    public static IServiceCollection AddUserInfoProvider(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddScoped(sp => {
            return new EventUserInfo() {

            };
        });

        return serviceCollection;
    }
}