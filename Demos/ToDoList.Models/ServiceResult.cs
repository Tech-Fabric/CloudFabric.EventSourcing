using System.Text.Json.Serialization;

namespace ToDoList.Models;


public record ServiceResultProblemDetailsInvalidParam
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}

public record ServiceResultProblemDetails
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("detail")]
    public string? Detail { get; set; }

    [JsonPropertyName("instance")]
    public string? Instance { get; set; }

    [JsonPropertyName("invalid-params")]
    public List<ServiceResultProblemDetailsInvalidParam>? InvalidParams { get; set; }
}

public record ServiceResult<T> : ServiceResult
{
    public T? Result { get; set; }

    public static ServiceResult<T> Success(T? result)
    {
        return new ServiceResult<T>()
        {
            Succeed = true,
            Result = result
        };
    }
    
    public new static ServiceResult<T> ValidationFailedOneParam(string paramName, string reason)
    {
        return new ServiceResult<T>()
        {
            Succeed = false,
            Result = default,
            ProblemDetails = new ServiceResultProblemDetails()
            {
                Type = "validation_error",
                Title = "Request is not valid",
                InvalidParams = new List<ServiceResultProblemDetailsInvalidParam>()
                {
                    new ServiceResultProblemDetailsInvalidParam()
                    {
                        Name = paramName,
                        Reason = reason
                    }
                }
            }
        };
    }

    public new static ServiceResult<T> Failed(string problemType, string problemTitle, string? problemDetail = null, string? instance = null, List<ServiceResultProblemDetailsInvalidParam> invalidParams = null)
    {
        return new ServiceResult<T>()
        {
            Succeed = false,
            Result = default,
            ProblemDetails = new ServiceResultProblemDetails()
            {
                Type = problemType,
                Title = problemTitle,
                Detail = problemDetail,
                Instance = instance,
                InvalidParams = invalidParams
            }
        };
    }

    public new static ServiceResult<T> Failed(ServiceResultProblemDetails problemDetails)
    {
        return new ServiceResult<T>()
        {
            Succeed = false,
            Result = default,
            ProblemDetails = problemDetails
        };
    }
}

public record ServiceResult
{
    public bool Succeed { get; set; }

    public ServiceResultProblemDetails? ProblemDetails { get; set; }

    public static ServiceResult Success()
    {
        return new ServiceResult()
        {
            Succeed = true
        };
    }

    public static ServiceResult Failed(string problemType, string problemTitle, string? problemDetail = null, string? instance = null, List<ServiceResultProblemDetailsInvalidParam> invalidParams = null)
    {
        return new ServiceResult()
        {
            Succeed = false,
            ProblemDetails = new ServiceResultProblemDetails()
            {
                Type = problemType,
                Title = problemTitle,
                Detail = problemDetail,
                Instance = instance,
                InvalidParams = invalidParams
            }
        };
    }

    public static ServiceResult Failed(ServiceResultProblemDetails problemDetails)
    {
        return new ServiceResult()
        {
            Succeed = false,
            ProblemDetails = problemDetails
        };
    }
}