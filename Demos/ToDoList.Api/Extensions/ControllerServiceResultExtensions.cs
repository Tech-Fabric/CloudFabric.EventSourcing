using System.Net;
using ToDoList.Models;
using Microsoft.AspNetCore.Mvc;

namespace ToDoList.Api.Extensions;

public static class ControllerExtensions
{
    public static IActionResult ServiceResult<T>(this ControllerBase controller, ServiceResult<T> serviceResult)
    {
        if(serviceResult.Succeed) {
            return controller.Ok(serviceResult.Result);
        }

        return new ContentResult
        {
            StatusCode = (int)HttpStatusCode.BadRequest,
            Content = System.Text.Json.JsonSerializer.Serialize(serviceResult.ProblemDetails),
            ContentType = "application/problem+json"
        };
    }

    public static IActionResult ServiceResult(this ControllerBase controller, ServiceResult<object> serviceResult)
    {
        return controller.ServiceResult<object>(serviceResult);
    }
}