using Microsoft.AspNetCore.Mvc.ModelBinding;
using ToDoList.Models;

namespace ToDoList.Web.Extensions;

public static class ModelStateServiceResultErrorsExtension
{
    public static void AddModelErrorsFromServiceResult(
        this ModelStateDictionary modelState, 
        ServiceResultProblemDetails? problemDetails
    )
    {
        if (problemDetails == null)
        {
            return;
        }

        if (problemDetails.InvalidParams != null)
        {
            foreach (var invalidParam in problemDetails.InvalidParams)
            {
                if (!string.IsNullOrEmpty(invalidParam.Name) && !string.IsNullOrEmpty(invalidParam.Reason))
                {
                    modelState.AddModelError(invalidParam.Name, invalidParam.Reason);
                }
            }
        }

        if (!string.IsNullOrEmpty(problemDetails.Title))
        {
            modelState.AddModelError("ProblemDetails.Title", problemDetails.Title);
        }

        if (!string.IsNullOrEmpty(problemDetails.Detail))
        {
            modelState.AddModelError("ProblemDetails.Detail", problemDetails.Detail);
        }
    }
}