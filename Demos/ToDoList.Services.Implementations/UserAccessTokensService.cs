using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using ToDoList.Models;
using ToDoList.Models.ViewModels.UserAccounts;
using ToDoList.Services.Interfaces;
using ToDoList.Services.Interfaces.Options;

namespace ToDoList.Services.Implementations;

public class UserAccessTokensService : IUserAccessTokensService
{
    private readonly IOptions<UserAccessTokensServiceOptions> _options;

    public UserAccessTokensService(IOptions<UserAccessTokensServiceOptions> options)
    {
        _options = options;
    }

    public ServiceResult<UserAccessTokenViewModel> GenerateAccessTokenForUser(List<Claim> userClaims)
    {
        var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Value.TokenSigningKey));

        var token = new JwtSecurityToken(
            issuer: _options.Value.Issuer,
            audience: _options.Value.Audience,
            expires: DateTime.Now.AddDays(_options.Value.TokenLifetimeDays),
            claims: userClaims,
            signingCredentials: new Microsoft.IdentityModel.Tokens.SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
        );

        var handler = new JwtSecurityTokenHandler();
        handler.InboundClaimTypeMap.Clear();
        handler.OutboundClaimTypeMap.Clear();

        return ServiceResult<UserAccessTokenViewModel>.Success(new UserAccessTokenViewModel()
        {
            AccessToken = handler.WriteToken(token),
            ExpiresIn = (int)(token.ValidTo - DateTime.UtcNow).TotalSeconds
        });
    }
}