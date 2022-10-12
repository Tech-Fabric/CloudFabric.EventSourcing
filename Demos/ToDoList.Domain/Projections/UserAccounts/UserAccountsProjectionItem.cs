using CloudFabric.Projections;
using CloudFabric.Projections.Attributes;

namespace ToDoList.Domain.Projections.UserAccounts;


[ProjectionDocument]
public class UserAccountsProjectionItem : ProjectionDocument
{
    [ProjectionDocumentProperty(IsSearchable = true)]
    public string? FirstName { get; set; }

    [ProjectionDocumentProperty(IsSearchable = true, IsFilterable = true)]
    public string? EmailAddress { get; set; }

    [ProjectionDocumentProperty(IsFilterable = true)]
    public DateTime PasswordUpdatedAt { get; set; }

    [ProjectionDocumentProperty(IsFilterable = true)]
    public DateTime? EmailConfirmedAt { get; set; }
}
