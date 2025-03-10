using System.Security.Claims;
using CloudFabric.EventSourcing.EventStore.Persistence;

namespace ToDoList.Web.Extensions;

public static class EventUserInfoServiceCollectionExtensions
{
    public static IServiceCollection AddUserInfoProvider(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddHttpContextAccessor();
        serviceCollection.AddScoped(
            sp =>
            {
                var httpContext = sp.GetRequiredService<IHttpContextAccessor>();

                if (httpContext?.HttpContext?.User?.Identity?.IsAuthenticated == true)
                {
                    var userAccountIdClaim = httpContext.HttpContext.User.Claims.First(c => c.Type == ClaimTypes.PrimarySid);
                    return new EventUserInfo(Guid.Parse(userAccountIdClaim.Value));
                }
                
                return new EventUserInfo();
            }
        );

        return serviceCollection;
    }
}