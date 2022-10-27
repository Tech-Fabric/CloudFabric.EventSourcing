namespace CloudFabric.EventSourcing.Tests.Domain.Projections.OrdersListProjection;
public class OrderListProjectionOrderItem
{
    public DateTime TimeAdded { get; set; }

    public string Name { get; set; }

    public decimal Amount { get; set; }
}
