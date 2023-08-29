using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace ToDoList.Models.Validation;

public class NonEmptyGuid: ValidationAttribute
{
    private const string DefaultErrorMessage = "'{0}' does not contain a valid guid";

    public NonEmptyGuid() : base(DefaultErrorMessage)
    {
    }

    protected override ValidationResult? IsValid(object value, ValidationContext validationContext)
    {
        var input = Convert.ToString(value, CultureInfo.CurrentCulture);

        // let the Required attribute take care of this validation
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        Guid guid;
        if (!Guid.TryParse(input, out guid))
        {
            // not a validstring representation of a guid
            return new ValidationResult(FormatErrorMessage(validationContext.DisplayName));
        }

        // is the passed guid one we know about?
        return guid == Guid.Empty ?
            new ValidationResult(FormatErrorMessage(validationContext.DisplayName)) : null;
    }
}
