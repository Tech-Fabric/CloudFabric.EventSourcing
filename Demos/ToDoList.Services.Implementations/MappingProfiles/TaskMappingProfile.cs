using AutoMapper;
using ToDoList.Models.ViewModels.TaskLists;

namespace ToDoList.Services.Implementations.MappingProfiles;

public class TaskMappingProfile : Profile
{
    public TaskMappingProfile()
    {
        CreateMap<Domain.Task, TaskViewModel>();
    }
}
