using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using ToDoList.Models;

namespace ToDoList.Ui.Shared.Components;

public class FormComponentServerProblemDetailsHandler : ComponentBase
{
    private ValidationMessageStore? _messageStore;

    [CascadingParameter] public EditContext? CurrentEditContext { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (CurrentEditContext == null)
        {
            throw new InvalidOperationException($"{nameof(FormComponentServerProblemDetailsHandler)} requires a cascading " +
                $"parameter of type {nameof(EditContext)}. For example, you can use {nameof(FormComponentServerProblemDetailsHandler)} " +
                $"inside an {nameof(EditForm)}.");
        }

        _messageStore = new ValidationMessageStore(CurrentEditContext);
        CurrentEditContext.OnValidationRequested += (s, e) => _messageStore.Clear();
        CurrentEditContext.OnFieldChanged += (s, e) => _messageStore.Clear(e.FieldIdentifier);
    }

    public void DisplayErrors(ServiceResultProblemDetails problemDetails)
    {
        if (_messageStore == null)
        {
            throw new InvalidOperationException($"{nameof(DisplayErrors)} should not be called before OnInitialized. MessageStore is empty.");
        }

        if (CurrentEditContext == null)
        {
            throw new InvalidOperationException($"{nameof(DisplayErrors)} should not be called before OnInitialized. CurrentEditContext is empty.");
        }

        if (problemDetails.InvalidParams != null && problemDetails.InvalidParams?.Count > 0)
        {
            foreach (var invalidParam in problemDetails.InvalidParams)
            {
                if (!string.IsNullOrEmpty(invalidParam.Name) && !string.IsNullOrEmpty(invalidParam.Reason))
                {
                    _messageStore.Add(CurrentEditContext.Field(invalidParam.Name), invalidParam.Reason);
                }
            }
        }

        CurrentEditContext.NotifyValidationStateChanged();
    }
}
