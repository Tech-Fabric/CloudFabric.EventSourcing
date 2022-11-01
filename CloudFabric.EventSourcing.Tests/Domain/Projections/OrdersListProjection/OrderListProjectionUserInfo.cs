using CloudFabric.Projections.Attributes;

namespace CloudFabric.EventSourcing.Tests.Domain.Projections.OrdersListProjection;

public class OrderListProjectionUserInfo
{
    [ProjectionDocumentProperty]
    public Guid UserId { get; set; }
}
