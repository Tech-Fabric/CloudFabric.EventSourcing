using AutoMapper;
using ToDoList.Domain;
using ToDoList.Models.ViewModels.TaskLists;

namespace ToDoList.Services.Implementations.MappingProfiles;

public class TaskListMappingProfile : Profile
{
    public TaskListMappingProfile()
    {
        CreateMap<TaskList, TaskListViewModel>();
    }
}
