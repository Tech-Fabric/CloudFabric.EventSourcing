using AutoMapper;
using ToDoList.Domain;
using ToDoList.Domain.Projections.TaskLists;
using ToDoList.Models.ViewModels.TaskLists;

namespace ToDoList.Services.Implementations.MappingProfiles;

public class TaskListMappingProfile : Profile
{
    public TaskListMappingProfile()
    {
        CreateMap<TaskListProjectionItem, TaskListViewModel>();
    }
}
