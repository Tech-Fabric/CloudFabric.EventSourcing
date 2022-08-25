using ToDoList.Models;
namespace ToDoList.Ui.Services;

public interface IServiceCommunicationProvider {
    Task<ServiceResult<TViewModel>> Get<TViewModel>(string path);
    Task<ServiceResult<TViewModel>> SendCommand<TViewModel>(string path, object command);
}