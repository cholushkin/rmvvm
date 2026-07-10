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
    
    // Upgraded to track a true stack of windows per container to prevent memory leaks/ghosting
    private readonly Dictionary<string, List<WindowComposer>> _activeWindows = new();

    [Inject]
    public void Construct(IObjectResolver resolver)
    {
        _resolver = resolver;
        
        foreach (var mapping in _containerMappings)
        {
            if (!string.IsNullOrEmpty(mapping.ContainerId) && mapping.Root != null)
            {
                _containers[mapping.ContainerId] = mapping.Root;
                _activeWindows[mapping.ContainerId] = new List<WindowComposer>();
            }
        }
    }

    public async UniTask StartAsync(CancellationToken cancellation)
    {
        foreach (var startupConfig in _startupWindows)
        {
            if (startupConfig.Config != null)
            {
                await ExecuteShowAsync(startupConfig.Config, null, startupConfig.ContainerId, startupConfig.StackMode, cancellation);
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

    public async UniTask ShowAsync(WindowConfig config, string containerId = null, StackMode? stackMode = null, CancellationToken cancellationToken = default)
    {
        await ExecuteShowAsync(config, null, containerId, stackMode, cancellationToken);
    }

    internal async UniTask ExecuteShowAsync(WindowConfig config, object prebuiltViewModel, string containerId, StackMode? stackMode, CancellationToken cancellationToken)
    {
        var targetContainerId = string.IsNullOrEmpty(containerId) ? config.DefaultContainerId : containerId;

        if (!_containers.TryGetValue(targetContainerId, out var containerRoot))
        {
            Debug.LogError($"[WindowService] Container ID '{targetContainerId}' not found.");
            return;
        }

        var mode = stackMode ?? config.DefaultStackMode;
        var windowList = _activeWindows[targetContainerId];

        // 1. Process Stack Mode Intent
        if (mode == StackMode.Clear)
        {
            foreach (var window in windowList)
            {
                await window.HideAsync();
                if (window != null && window.gameObject != null) Destroy(window.gameObject);
            }
            windowList.Clear();
        }
        else if (mode == StackMode.Replace)
        {
            if (windowList.Count > 0)
            {
                var topWindow = windowList[windowList.Count - 1];
                await topWindow.HideAsync();
                if (topWindow != null && topWindow.gameObject != null) Destroy(topWindow.gameObject);
                windowList.RemoveAt(windowList.Count - 1);
            }
        }

        // 2. Resolve & Instantiate
        object viewModel = prebuiltViewModel;

        if (viewModel == null)
        {
            var viewModelType = config.ViewModelType;
            if (viewModelType == null)
            {
                Debug.LogError($"[WindowService] Config '{config.name}' does not map to a valid ViewModel type.");
                return;
            }

            try
            {
                viewModel = _resolver.Resolve(viewModelType);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WindowService] Failed to resolve ViewModel '{viewModelType.Name}'. Exception: {ex.Message}");
                return;
            }
        }

        var prefabInstance = Instantiate(config.Prefab, containerRoot);
        var composer = prefabInstance.GetComponent<WindowComposer>();

        if (composer == null)
        {
            Debug.LogError($"[WindowService] Prefab '{config.Prefab.name}' is missing a WindowComposer component.");
            Destroy(prefabInstance);
            return;
        }

        composer.BindBoxed(viewModel);
        
        // 3. Track & Show
        windowList.Add(composer);
        await composer.ShowAsync();
    }

    public async UniTask<TResult> ShowModalAsync<TViewModel, TResult>(
        WindowConfig config, 
        TViewModel modalViewModel, 
        string containerId = null, 
        StackMode stackMode = StackMode.Push, 
        CancellationToken cancellationToken = default) 
        where TViewModel : class, IModalViewModel<TResult>
    {
        await ExecuteShowAsync(config, modalViewModel, containerId, stackMode, cancellationToken);
        return await modalViewModel.ResultTask;
    }

    public async UniTask HideAsync<TViewModel>(TViewModel viewModel, CancellationToken cancellationToken = default) where TViewModel : class
    {
        foreach (var kvp in _activeWindows)
        {
            var list = kvp.Value;
            // Iterate top-down to handle popped modals cleanly
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].HasViewModel(viewModel))
                {
                    var window = list[i];
                    await window.HideAsync();
                    if (window != null && window.gameObject != null) Destroy(window.gameObject);
                    list.RemoveAt(i);
                    return; // Break immediately once target is eliminated
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
}