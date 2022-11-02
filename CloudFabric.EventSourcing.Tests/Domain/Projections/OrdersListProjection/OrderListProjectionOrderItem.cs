using CloudFabric.Projections.Attributes;

namespace CloudFabric.EventSourcing.Tests.Domain.Projections.OrdersListProjection;
public class OrderListProjectionOrderItem
{
    [ProjectionDocumentProperty]
    public DateTime TimeAdded { get; set; }

    [ProjectionDocumentProperty(IsSearchable = true)]
    public string Name { get; set; }

    [ProjectionDocumentProperty]
    public decimal Amount { get; set; }
}
