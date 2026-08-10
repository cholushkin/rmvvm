// todo: Implement Addressables asynchronous loading signature for WindowConfig.Prefab
// idea: Expose a way to dynamically register container priorities for hardware back routing

using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.UI.WindowSystem
{
    public interface IWindowService
    {
        // Accurately reflects both generic and non-generic WindowBuilders
        WindowBuilder Open(WindowConfig config);
        WindowBuilder<TViewModel> Open<TViewModel>(WindowConfig config) where TViewModel : class;
        
        UniTask ShowAsync<TViewModel>(WindowConfig config, CancellationToken cancellationToken = default) where TViewModel : class;
        
        UniTask ShowAsync<TViewModel>(WindowConfig config, TViewModel viewModel, StackMode stackMode = StackMode.Push, CancellationToken cancellationToken = default) where TViewModel : class;
        
        UniTask<TResult> ShowModalAsync<TViewModel, TResult>(WindowConfig config, TViewModel modalViewModel, string containerId = null, StackMode stackMode = StackMode.Push, CancellationToken cancellationToken = default) where TViewModel : class, IModalViewModel<TResult>;

        UniTask<TResult> ShowModalAsync<TViewModel, TResult>(WindowConfig config, Action<TViewModel> setup = null, string containerId = null, StackMode stackMode = StackMode.Push,
            CancellationToken cancellationToken = default) where TViewModel : class, IModalViewModel<TResult>;
        
        UniTask HideAsync<TViewModel>(TViewModel viewModel, CancellationToken cancellationToken = default) where TViewModel : class;
        
        UniTask HideAsync(string containerId, CancellationToken cancellationToken = default);

        bool RouteHardwareBack();
        
        CancellationToken GetOrCreateFlowToken(string flowId);
        
        void CancelFlow(string flowId);
    }
}