using System.Security.Claims;

namespace ToDoList.Models.ViewModels.UserAccounts;

public record UserAccountPersonalViewModel
{
    public Guid Id { get; init; }
    public string FirstName { get; init; }

    public string Email { get; init; }

    public List<Claim> Claims
    {
        get
        {
            var list = new List<Claim>()
            {
                new Claim("sub", Id.ToString(), ClaimValueTypes.String),
                new Claim(ClaimTypes.PrimarySid, Id.ToString(), ClaimValueTypes.Integer),
                new Claim(ClaimTypes.Name, FirstName, ClaimValueTypes.String),
            };

            if (!string.IsNullOrEmpty(Email)) list.Add(new Claim(ClaimTypes.Email, Email, ClaimValueTypes.String));

            return list;
        }
    }

    public UserAccountPersonalViewModel()
    {
    }

    public UserAccountPersonalViewModel(Guid Id, string FirstName, string Email)
    {
        Id = Id;
        FirstName = FirstName;
        Email = Email;
    }
}