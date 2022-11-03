using CloudFabric.Projections.Attributes;

namespace CloudFabric.EventSourcing.Tests.Domain.Projections.OrdersListProjection;
public class OrderListProjectionOrderItem
{
    [ProjectionDocumentProperty]
    public DateTime AddedAt { get; set; }

    [ProjectionDocumentProperty]
    public string Name { get; set; }

    [ProjectionDocumentProperty]
    public decimal Amount { get; set; }
}
