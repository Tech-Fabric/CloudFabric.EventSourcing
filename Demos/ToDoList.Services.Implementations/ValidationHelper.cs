using System.ComponentModel.DataAnnotations;

using ToDoList.Models;

namespace ToDoList.Services.Implementations;

public class ValidationHelper
{
    public static ServiceResultProblemDetails? Validate(object instance)
    {
        var validationContext = new ValidationContext(instance);
        var validationResults = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(instance, validationContext, validationResults, true);

        if(isValid) {
            return null;
        }

        var problemDetails = new ServiceResultProblemDetails() {
            Type = "validatioin_error",
            Title = "Request is not valid",
        };

        if(validationResults.Count > 0) {
            problemDetails.InvalidParams ??= new List<ServiceResultProblemDetailsInvalidParam>();

            foreach(var validationResult in validationResults) {
                foreach (var validationMember in validationResult.MemberNames) {
                    problemDetails.InvalidParams.Add(new ServiceResultProblemDetailsInvalidParam() {
                        Name = validationMember,
                        Reason = validationResult.ErrorMessage
                    });
                }
            }
        }

        return problemDetails;
    }
}