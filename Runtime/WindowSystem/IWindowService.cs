// todo: Implement Addressables asynchronous loading signature for WindowConfig.Prefab
// idea: Expose a way to dynamically register container priorities for hardware back routing

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;

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

        /// Fires whenever a window is added to or removed from any container. Recomputable state
        /// (e.g. input gating) should derive from this rather than tracking pushes/pops itself.
        Observable<WindowStackChange> ObserveStackChanged();

        int GetActiveWindowCount(string containerId);

        /// Zero-alloc snapshot of the windows currently active in a container, topmost last.
        IReadOnlyList<WindowComposer> GetActiveWindows(string containerId);
    }
}