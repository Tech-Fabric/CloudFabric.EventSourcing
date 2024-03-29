using AutoMapper;
using ToDoList.Domain.Projections.TaskLists;
using ToDoList.Models.ViewModels.TaskLists;

namespace ToDoList.Services.Implementations.MappingProfiles;

public class TaskMappingProfile : Profile
{
    public TaskMappingProfile()
    {
        CreateMap<ToDoList.Domain.Task, TaskViewModel>();
        CreateMap<TaskProjectionItem, TaskViewModel>();
    }
}
