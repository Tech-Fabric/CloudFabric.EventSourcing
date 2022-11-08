using CloudFabric.Projections.Attributes;
using CloudFabric.Projections.Constants;

namespace CloudFabric.EventSourcing.Tests.Domain.Projections.OrdersListProjection;

public class OrderListProjectionUserInfo
{
    [ProjectionDocumentProperty]
    public Guid UserId { get; set; }

    [ProjectionDocumentProperty(IsSearchable = true, Analyzer = SearchAnalyzers.UrlEmailAnalyzer, SearchAnalyzer = SearchAnalyzers.UrlEmailAnalyzer)]
    public string Email { get; set; }
}
