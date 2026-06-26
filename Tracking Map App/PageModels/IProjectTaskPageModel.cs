using CommunityToolkit.Mvvm.Input;
using Tracking_Map_App.Models;

namespace Tracking_Map_App.PageModels
{
    public interface IProjectTaskPageModel
    {
        IAsyncRelayCommand<ProjectTask> NavigateToTaskCommand { get; }
        bool IsBusy { get; }
    }
}