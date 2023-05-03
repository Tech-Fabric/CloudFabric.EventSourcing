using AutoMapper;

using CloudFabric.EventSourcing.Domain;
using CloudFabric.EventSourcing.EventStore.Persistence;
using CloudFabric.Projections;
using CloudFabric.Projections.Queries;
using ToDoList.Domain;
using ToDoList.Domain.Projections.UserAccounts;
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
    private readonly IProjectionRepository<UserAccountsProjectionItem> _userAccountsProjectionRepository;

    private readonly IUserAccessTokensService _userAccessTokensService;
    

    public UserAccountsService(
        IMapper mapper,
        EventUserInfo userInfo,
        AggregateRepository<UserAccount> userRepository,
        AggregateRepository<UserAccountEmailAddress> userAccountEmailAddressesRepository,
        ProjectionRepositoryFactory projectionRepositoryFactory,
        IUserAccessTokensService userAccessTokensService
    )
    {
        _mapper = mapper;
        _userInfo = userInfo;
        _userAccountsRepository = userRepository;
        _userAccountEmailAddressesRepository = userAccountEmailAddressesRepository;
        _userAccountsProjectionRepository = projectionRepositoryFactory.GetProjectionRepository<UserAccountsProjectionItem>();
        _userAccessTokensService = userAccessTokensService;
    }

    public async Task<ServiceResult<UserAccountPersonalViewModel>> RegisterNewUserAccount(RegisterNewUserAccountRequest request, CancellationToken ct)
    {
        var validationProblemDetails = ValidationHelper.Validate(request);

        if(validationProblemDetails != null) {
            return ServiceResult<UserAccountPersonalViewModel>.Failed(validationProblemDetails);
        }

        var emailAlreadyExists = (await _userAccountsProjectionRepository.Query(
                ProjectionQueryExpressionExtensions.Where<UserAccountsProjectionItem>(x => x.EmailAddress == request.Email)
        ))
        .Records
        .FirstOrDefault()
        ?.Document;

        // it may happen that email record was created but then something went wrong and email was left unatached.
        UserAccountEmailAddress? userAccountEmail;
        if (emailAlreadyExists != null)
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
        else
        {
            userAccountEmail = await _userAccountEmailAddressesRepository.LoadAsync(emailAlreadyExists.Id.Value, PartitionKeys.GetUserAccountEmailAddressPartitionKey());
        }

        var userId = Guid.NewGuid();
        userAccountEmail ??= new UserAccountEmailAddress(userId, request.Email);

        await _userAccountEmailAddressesRepository.SaveAsync(new EventUserInfo(), userAccountEmail, ct);

        var userAccount = new UserAccount(Guid.NewGuid(), request.FirstName, PasswordHelper.HashPassword(request.Password));

        await _userAccountsRepository.SaveAsync(new EventUserInfo(), userAccount, ct);

        userAccountEmail.AssignUserAccount(userAccount.Id);
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

        var userAccount = await _userAccountsRepository.LoadAsync(request.UserAccountId, request.UserAccountId.ToString(), cancellationToken);

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

        var userAccountEmailAddress = (await _userAccountsProjectionRepository.Query(
                ProjectionQueryExpressionExtensions.Where<UserAccountsProjectionItem>(x => x.EmailAddress == request.Email),
            PartitionKeys.GetUserAccountEmailAddressPartitionKey()
        ))
        .Records
        .FirstOrDefault()
        ?.Document;

        if(userAccountEmailAddress == null) {
            return ServiceResult<UserAccessTokenViewModel>.Failed("invalid_credentials", "Credentials were invalid");
        }

        var userAccount = await _userAccountsRepository.LoadAsync(
            userAccountEmailAddress.Id.Value,
            userAccountEmailAddress.Id.ToString(),
            cancellationToken
        );

        if(userAccount == null) {
            return ServiceResult<UserAccessTokenViewModel>.Failed("invalid_credentials", "Credentials were invalid");
        }

        var tokenServiceResult = _userAccessTokensService.GenerateAccessTokenForUser(userAccountEmailAddress.Id.Value, userAccount.FirstName);

        return tokenServiceResult;
    }
}