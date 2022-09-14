using CloudFabric.Projections;
using CloudFabric.Projections.Attributes;

namespace CloudFabric.EventSourcing.Tests.Domain.Projections.OrdersListProjection;

[ProjectionDocument]
public class OrderListProjectionItem : ProjectionDocument
{
    [ProjectionDocumentProperty(IsSearchable = true)]
    public string Name { get; set; } = string.Empty;

    [ProjectionDocumentProperty(IsFilterable = true)]
    public long ItemsCount { get; set; } = 0;
}