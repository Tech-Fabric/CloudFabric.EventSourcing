using AutoMapper;
using ToDoList.Domain;
using ToDoList.Models.ViewModels.UserAccounts;

namespace ToDoList.Services.Implementations.MappingProfiles;

public class UserAccountMappingProfile : Profile
{
    public UserAccountMappingProfile()
    {
        CreateMap<UserAccount, UserAccountPersonalViewModel>();
    }
}
