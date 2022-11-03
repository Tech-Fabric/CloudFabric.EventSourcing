using CloudFabric.Projections.Attributes;

namespace CloudFabric.EventSourcing.Tests.Domain.Projections.OrdersListProjection;

public class OrderListProjectionUserInfo
{
    [ProjectionDocumentProperty]
    public Guid UserId { get; set; }

    [ProjectionDocumentProperty(IsSearchable = true, Analyzer = "email-analyzer", SearchAnalyzer = "email-analyzer")]
    public string Email { get; set; }
}
