namespace ToDoList.Services.Interfaces.Options;

public class UserAccessTokensServiceOptions
{
    public string Issuer { get; set; }
    public string Audience { get; set; }
    public int TokenLifetimeDays { get; set; }
    public string TokenSigningKey { get; set; }
}
