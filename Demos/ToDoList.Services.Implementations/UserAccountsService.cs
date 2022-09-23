using AutoMapper;

using CloudFabric.EventSourcing.Domain;
using CloudFabric.EventSourcing.EventStore.Persistence;

using ToDoList.Domain;
using ToDoList.Models;
using ToDoList.Models.RequestModels.UserAccounts;
using ToDoList.Models.ViewModels.UserAccounts;
using ToDoList.Services.Interfaces;

namespace ToDoList.Services.Implementations;

public class UserAccountsService : IUserAccountsService
{
    private readonly IMapper _mapper;
    private readonly EventUserInfo _userInfo;

    private readonly AggregateRepository<UserAccount> _userAccountsRepository;
    private readonly AggregateRepository<UserAccountEmailAddress> _userAccountEmailAddressesRepository;

    private readonly IUserAccessTokensService _userAccessTokensService;
    

    public UserAccountsService(
        IMapper mapper,
        EventUserInfo userInfo,
        AggregateRepository<UserAccount> userRepository,
        AggregateRepository<UserAccountEmailAddress> userAccountEmailAddressesRepository,
        IUserAccessTokensService userAccessTokensService
    )
    {
        _mapper = mapper;
        _userInfo = userInfo;
        _userAccountsRepository = userRepository;
        _userAccountEmailAddressesRepository = userAccountEmailAddressesRepository;
        _userAccessTokensService = userAccessTokensService;
    }

    public async Task<ServiceResult<UserAccountPersonalViewModel>> RegisterNewUserAccount(RegisterNewUserAccountRequest request, CancellationToken ct)
    {
        var validationProblemDetails = ValidationHelper.Validate(request);

        if(validationProblemDetails != null) {
            return ServiceResult<UserAccountPersonalViewModel>.Failed(validationProblemDetails);
        }

        var emailAlreadyExists = await _userAccountEmailAddressesRepository.LoadAsync(request.Email, PartitionKeys.GetUserAccountEmailAddressPartitionKey(), ct);

        // it may happen that email record was created but then something went wrong and email was left unatached.
        if (emailAlreadyExists != null && emailAlreadyExists?.UserAccountId != null)
        {
            return ServiceResult<UserAccountPersonalViewModel>.Failed(
                "email_already_registered", 
                "User with provided email address already exists",
                "", "", new List<ServiceResultProblemDetailsInvalidParam>() {
                    new ServiceResultProblemDetailsInvalidParam() {
                        Name = nameof(request.Email),
                        Reason = "Please try different email address"
                    }
                }
            );
        }

        var userAccountEmail = emailAlreadyExists ?? new UserAccountEmailAddress(request.Email);

        await _userAccountEmailAddressesRepository.SaveAsync(new EventUserInfo(), userAccountEmail, ct);

        var userAccount = new UserAccount(Guid.NewGuid().ToString(), request.FirstName, PasswordHelper.HashPassword(request.Password));

        await _userAccountsRepository.SaveAsync(new EventUserInfo(), userAccount, ct);

        userAccountEmail.AssignUserAccount(userAccount.Id.ToString());
        await _userAccountEmailAddressesRepository.SaveAsync(new EventUserInfo(userAccount.Id), userAccountEmail, ct);

        return ServiceResult<UserAccountPersonalViewModel>.Success(_mapper.Map<UserAccountPersonalViewModel>(userAccount));
    }

    public async Task<ServiceResult> UpdateUserAccountPassword(
        UpdateUserAccountPasswordRequest request, CancellationToken cancellationToken
    ) {
        var validationProblemDetails = ValidationHelper.Validate(request);

        if(validationProblemDetails != null) {
            return ServiceResult.Failed(validationProblemDetails);
        }

        var userAccount = await _userAccountsRepository.LoadAsync(request.UserAccountId, PartitionKeys.GetUserAccountPartitionKey(), cancellationToken);

        if(userAccount == null) {
            return ServiceResult.Failed("user_not_found", "User was not found");
        }

        if(!PasswordHelper.VerifyHashedPassword(userAccount.HashedPassword, request.OldPassword)) {
            
        }

        userAccount.UpdatePassword(PasswordHelper.HashPassword(request.NewPassword));

        await _userAccountsRepository.SaveAsync(_userInfo, userAccount, cancellationToken);

        return ServiceResult.Success();
    }

    public async Task<ServiceResult<UserAccessTokenViewModel>> GenerateAccessTokenForUser(
        GenerateNewAccessTokenRequest request,
        CancellationToken cancellationToken
    ) {
        var validationProblemDetails = ValidationHelper.Validate(request);

        if(validationProblemDetails != null) {
            return ServiceResult<UserAccessTokenViewModel>.Failed(validationProblemDetails);
        }

        var userAccountEmailAddress = await _userAccountEmailAddressesRepository.LoadAsync(
            request.Email,
            PartitionKeys.GetUserAccountEmailAddressPartitionKey(),
            cancellationToken
        );

        if(userAccountEmailAddress == null) {
            return ServiceResult<UserAccessTokenViewModel>.Failed("invalid_credentials", "Credentials were invalid");
        }

        var userAccount = await _userAccountsRepository.LoadAsync(
            userAccountEmailAddress.UserAccountId,
            PartitionKeys.GetUserAccountPartitionKey(),
            cancellationToken
        );

        if(userAccount == null) {
            return ServiceResult<UserAccessTokenViewModel>.Failed("invalid_credentials", "Credentials were invalid");
        }

        var tokenServiceResult = _userAccessTokensService.GenerateAccessTokenForUser(userAccountEmailAddress.UserAccountId, userAccount.FirstName);

        return tokenServiceResult;
    }
}