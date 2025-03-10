using System.Security.Claims;
using AutoMapper;

using CloudFabric.EventSourcing.Domain;
using CloudFabric.EventSourcing.EventStore.Persistence;
using CloudFabric.Projections;
using CloudFabric.Projections.Queries;
using ToDoList.Domain;
using ToDoList.Domain.Projections.UserAccounts;
using ToDoList.Models;
using ToDoList.Models.RequestModels.TaskLists;
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

    private readonly ITaskListsService _taskListsService;
    

    public UserAccountsService(
        IMapper mapper,
        EventUserInfo userInfo,
        AggregateRepository<UserAccount> userRepository,
        AggregateRepository<UserAccountEmailAddress> userAccountEmailAddressesRepository,
        ProjectionRepositoryFactory projectionRepositoryFactory,
        IUserAccessTokensService userAccessTokensService,
        ITaskListsService taskListsService
    )
    {
        _mapper = mapper;
        _userInfo = userInfo;
        _userAccountsRepository = userRepository;
        _userAccountEmailAddressesRepository = userAccountEmailAddressesRepository;
        _userAccountsProjectionRepository = projectionRepositoryFactory.GetProjectionRepository<UserAccountsProjectionItem>();
        _userAccessTokensService = userAccessTokensService;
        _taskListsService = taskListsService;
    }

    public async Task<ServiceResult<UserAccountPersonalViewModel>> RegisterNewUserAccount(RegisterNewUserAccountRequest request, CancellationToken ct)
    {
        var validationProblemDetails = ValidationHelper.Validate(request);

        if(validationProblemDetails != null) {
            return ServiceResult<UserAccountPersonalViewModel>.Failed(validationProblemDetails);
        }
        
        var userAccountEmail = new UserAccountEmailAddress(request.Email);
        
        var emailAlreadyExists = await _userAccountEmailAddressesRepository.LoadAsync(userAccountEmail.Id, userAccountEmail.PartitionKey, ct);

        var userId = Guid.NewGuid();

        // it may happen that email record was created but then something went wrong and email was left unatached.
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
            userAccountEmail.AssignUserAccount(userId);
        }
        
        await _userAccountEmailAddressesRepository.SaveAsync(new EventUserInfo(userId), userAccountEmail, ct);

        var userAccount = new UserAccount(userId, request.FirstName, PasswordHelper.HashPassword(request.Password));

        await _userAccountsRepository.SaveAsync(new EventUserInfo(userId), userAccount, ct);

        await _taskListsService.CreateTaskList(
            new CreateTaskListRequest()
            {
                Name = "To Do"
            }, 
            ct
        );
        
        await _taskListsService.CreateTaskList(
            new CreateTaskListRequest()
            {
                Name = "In Progress"
            }, 
            ct
        );
        
        await _taskListsService.CreateTaskList(
            new CreateTaskListRequest()
            {
                Name = "Done"
            }, 
            ct
        );

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

    public async Task<ServiceResult<UserAccountPersonalViewModel>> AuthenticateUser(
        AuthenticateUserRequest request,
        CancellationToken cancellationToken
    )
    {
        var validationProblemDetails = ValidationHelper.Validate(request);

        if(validationProblemDetails != null) {
            return ServiceResult<UserAccountPersonalViewModel>.Failed(validationProblemDetails);
        }
        
        var userAccountEmail = new UserAccountEmailAddress(request.Email!);
        var userAccountEmailAddress = await _userAccountEmailAddressesRepository.LoadAsync(userAccountEmail.Id, userAccountEmail.PartitionKey, cancellationToken);

        if(userAccountEmailAddress == null) {
            return ServiceResult<UserAccountPersonalViewModel>.Failed("invalid_credentials", "Credentials were invalid");
        }

        var userAccount = await _userAccountsRepository.LoadAsync(
            userAccountEmailAddress.UserAccountId,
            userAccountEmailAddress.UserAccountId.ToString(),
            cancellationToken
        );

        if(userAccount == null) {
            return ServiceResult<UserAccountPersonalViewModel>.Failed("invalid_credentials", "Incorrect email or password");
        }
        
        return ServiceResult<UserAccountPersonalViewModel>.Success(_mapper.Map<UserAccountPersonalViewModel>(userAccount));
    }

    public async Task<ServiceResult<UserAccessTokenViewModel>> GenerateAccessTokenForUser(
        AuthenticateUserRequest request,
        CancellationToken cancellationToken
    )
    {
        var authenticationResult = await AuthenticateUser(request, cancellationToken);

        if (!authenticationResult.Succeed)
        {
            return ServiceResult<UserAccessTokenViewModel>.Failed(authenticationResult.ProblemDetails!);
        }
        
        var tokenServiceResult = _userAccessTokensService.GenerateAccessTokenForUser(authenticationResult.Result!.Claims);

        return tokenServiceResult;
    }
}