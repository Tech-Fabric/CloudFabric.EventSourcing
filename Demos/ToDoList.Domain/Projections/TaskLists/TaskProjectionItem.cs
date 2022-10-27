using CloudFabric.Projections;
using CloudFabric.Projections.Attributes;

namespace ToDoList.Domain.Projections.TaskLists;


[ProjectionDocument]
public class TaskProjectionItem : ProjectionDocument
{
    [ProjectionDocumentProperty(IsFilterable = true)]
    public Guid UserAccountId { get; set; }

    [ProjectionDocumentProperty(IsSearchable = true, IsFilterable = true)]
    public Guid TaskListId { get; set; }

    [ProjectionDocumentProperty(IsSearchable = true)]
    public string? Title { get; set; }

    [ProjectionDocumentProperty(IsSearchable = true)]
    public string? Description { get; set; }

    [ProjectionDocumentProperty(IsSearchable = true, IsFilterable = true)]
    public bool IsCompleted { get; set; }
}
