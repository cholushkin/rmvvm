using System.Threading;
using Cysharp.Threading.Tasks;
using Game.UI.WindowSystem;

public interface IWindowService
{
    WindowBuilder Open(WindowConfig config);
    WindowBuilder<TViewModel> Open<TViewModel>(WindowConfig config) where TViewModel : class;

    UniTask<TResult> ShowModalAsync<TViewModel, TResult>(
        WindowConfig config, 
        TViewModel modalViewModel, 
        string containerId = null, 
        StackMode stackMode = StackMode.Push, 
        CancellationToken cancellationToken = default) 
        where TViewModel : class, IModalViewModel<TResult>;
    
    UniTask HideAsync<TViewModel>(TViewModel viewModel, CancellationToken cancellationToken = default) where TViewModel : class;
    UniTask HideAsync(string containerId, CancellationToken cancellationToken = default);
}