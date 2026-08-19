// todo: Inject or configure containerPriorities array in RouteHardwareBack instead of hardcoding it
// idea: Object pooling for WindowComposers instead of absolute Object.Destroy()

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.UI.WindowSystem;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public sealed class WindowService : MonoBehaviour, IWindowService, IAsyncStartable
{
    [Serializable]
    public struct ContainerMapping
    {
        [Tooltip("The string ID used to route screens to this container (e.g., 'MainScreen', 'ModalOverlay').")]
        public string ContainerId;
        public RectTransform Root;
    }

    [Serializable]
    public struct StartupWindowMapping
    {
        public WindowConfig Config;
        public string ContainerId;
        public StackMode StackMode;
    }

    [Header("Startup Configuration")]
    [Tooltip("Windows to automatically open when the game boots.")]
    [SerializeField] private List<StartupWindowMapping> _startupWindows = new();

    [Header("Containers")]
    [SerializeField] private List<ContainerMapping> _containerMappings = new();

    private IObjectResolver _resolver;
    private readonly Dictionary<string, RectTransform> _containers = new();
    private readonly Dictionary<string, List<WindowComposer>> _activeWindows = new();
    private readonly Dictionary<string, CancellationTokenSource> _flowTokens = new();

    [Inject]
    public void Construct(IObjectResolver resolver)
    {
        _resolver = resolver;

        foreach (var mapping in _containerMappings)
        {
            if (string.IsNullOrEmpty(mapping.ContainerId) || mapping.Root == null)
            {
                Debug.LogWarning("[WindowService] Invalid container mapping found. Skipping.");
                continue;
            }

            _containers[mapping.ContainerId] = mapping.Root;
            _activeWindows[mapping.ContainerId] = new List<WindowComposer>();
        }
    }

    public async UniTask StartAsync(CancellationToken cancellation)
    {
        foreach (var startupWindow in _startupWindows)
        {
            if (startupWindow.Config == null) continue;

            var viewModelType = startupWindow.Config.ViewModelType;
            if (viewModelType != null)
            {
                var viewModel = _resolver.Resolve(viewModelType);
                await ExecuteShowAsync(startupWindow.Config, viewModel, startupWindow.ContainerId, startupWindow.StackMode, cancellation);
            }
            else
            {
                Debug.LogError($"[WindowService] Startup WindowConfig '{startupWindow.Config.name}' is missing a mapped ViewModel Type.");
            }
        }
    }

    public WindowBuilder Open(WindowConfig config)
    {
        return new WindowBuilder(this, config);
    }

    public WindowBuilder<TViewModel> Open<TViewModel>(WindowConfig config) where TViewModel : class
    {
        return new WindowBuilder<TViewModel>(this, config);
    }

    public UniTask ShowAsync<TViewModel>(WindowConfig config, CancellationToken cancellationToken = default) where TViewModel : class
    {
        var viewModel = _resolver.Resolve<TViewModel>();
        return ExecuteShowAsync(config, viewModel, null, null, cancellationToken);
    }

    public UniTask ShowAsync<TViewModel>(WindowConfig config, TViewModel viewModel, StackMode stackMode = StackMode.Push, CancellationToken cancellationToken = default) where TViewModel : class
    {
        return ExecuteShowAsync(config, viewModel, null, stackMode, cancellationToken);
    }

    public async UniTask<TResult> ShowModalAsync<TViewModel, TResult>(WindowConfig config, TViewModel modalViewModel, string containerId = null, StackMode stackMode = StackMode.Push, CancellationToken cancellationToken = default)
        where TViewModel : class, IModalViewModel<TResult>
    {
        await ExecuteShowAsync(config, modalViewModel, containerId, stackMode, cancellationToken);
        var result = await modalViewModel.ResultTask;
        await HideAsync(modalViewModel, cancellationToken);
        return result;
    }
    
    public async UniTask<TResult> ShowModalAsync<TViewModel, TResult>(
        WindowConfig config, 
        Action<TViewModel> setup = null, 
        string containerId = null, 
        StackMode stackMode = StackMode.Push, 
        CancellationToken cancellationToken = default)
        where TViewModel : class, IModalViewModel<TResult>
    {
        var viewModel = _resolver.Resolve<TViewModel>();
        
        // Apply the specific string parameters for the popup
        setup?.Invoke(viewModel);

        await ExecuteShowAsync(config, viewModel, containerId, stackMode, cancellationToken);
        var result = await viewModel.ResultTask;
        await HideAsync(viewModel, cancellationToken);
        return result;
    }

    internal async UniTask ExecuteShowAsync(WindowConfig config, object viewModel, string containerIdOverride, StackMode? modeOverride, CancellationToken cancellationToken)
    {
        var targetContainer = string.IsNullOrEmpty(containerIdOverride) ? config.DefaultContainerId : containerIdOverride;
        var stackMode = modeOverride ?? config.DefaultStackMode;

        if (!_containers.TryGetValue(targetContainer, out var parentRect))
        {
            Debug.LogError($"[WindowService] Container ID '{targetContainer}' not found. Cannot show window.");
            return;
        }

        if (!_activeWindows.TryGetValue(targetContainer, out var list))
        {
            list = new List<WindowComposer>();
            _activeWindows[targetContainer] = list;
        }

        if (stackMode == StackMode.Replace || stackMode == StackMode.Clear)
        {
            await HideAsync(targetContainer, cancellationToken);
        }

        if (viewModel == null && config.ViewModelType != null)
        {
            viewModel = _resolver.Resolve(config.ViewModelType);
        }

        var instance = Instantiate(config.Prefab, parentRect, false);
        var composer = instance.GetComponent<WindowComposer>();

        list.Add(composer);

        if (viewModel != null)
        {
            composer.BindBoxed(viewModel);
        }
        else
        {
            Debug.LogWarning($"[WindowService] No ViewModel provided or mapped for '{config.name}'. Binding skipped.");
        }

        await composer.ShowAsync();

        if (cancellationToken != default && cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() =>
            {
                if (viewModel != null)
                {
                    HideAsync(viewModel).Forget();
                }
                else
                {
                    HideAsync(targetContainer).Forget();
                }
            });
        }
    }

    public async UniTask HideAsync<TViewModel>(TViewModel viewModel, CancellationToken cancellationToken = default) where TViewModel : class
    {
        foreach (var kvp in _activeWindows)
        {
            var list = kvp.Value;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].HasViewModel(viewModel))
                {
                    var window = list[i];
                    await window.HideAsync();
                    if (window != null && window.gameObject != null) Destroy(window.gameObject);
                    list.RemoveAt(i);
                    return;
                }
            }
        }
    }

    public async UniTask HideAsync(string containerId, CancellationToken cancellationToken = default)
    {
        if (_activeWindows.TryGetValue(containerId, out var list) && list.Count > 0)
        {
            var topWindow = list[list.Count - 1];
            await topWindow.HideAsync();
            if (topWindow != null && topWindow.gameObject != null) Destroy(topWindow.gameObject);
            list.RemoveAt(list.Count - 1);
        }
    }

    public CancellationToken GetOrCreateFlowToken(string flowId)
    {
        if (!_flowTokens.TryGetValue(flowId, out var cts) || cts.IsCancellationRequested)
        {
            cts = new CancellationTokenSource();
            _flowTokens[flowId] = cts;
        }
        return cts.Token;
    }

    public void CancelFlow(string flowId)
    {
        if (_flowTokens.TryGetValue(flowId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
            _flowTokens.Remove(flowId);
        }
    }

    public bool RouteHardwareBack()
    {
        string[] containerPriorities = { "ModalOverlay", "MainScreen", "Background" };

        foreach (var containerId in containerPriorities)
        {
            if (_activeWindows.TryGetValue(containerId, out var list) && list.Count > 0)
            {
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var windowComposer = list[i];

                    if (windowComposer.IsAnimating)
                    {
                        return true; 
                    }

                    if (windowComposer.GetViewModelBoxed() is IHardwareBackHandler backHandler)
                    {
                        bool consumed = backHandler.OnBackRequested();
                        if (consumed) return true;
                    }
                }
            }
        }

        return false;
    }
}