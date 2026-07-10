// idea: Add .WithAnimation(TransitionType) later if you implement custom UI animations

using System.Threading;
using Cysharp.Threading.Tasks;
using Game.UI.WindowSystem;

// 1. Non-Generic Builder (For standard, purely data-driven screens)
public struct WindowBuilder
{
    private readonly WindowService _service;
    private readonly WindowConfig _config;
    
    private string _containerIdOverride;
    private StackMode? _modeOverride;

    internal WindowBuilder(WindowService service, WindowConfig config)
    {
        _service = service;
        _config = config;
        _containerIdOverride = null;
        _modeOverride = null;
    }

    public WindowBuilder InContainer(string containerId)
    {
        _containerIdOverride = containerId;
        return this;
    }

    public WindowBuilder WithMode(StackMode mode)
    {
        _modeOverride = mode;
        return this;
    }

    public UniTask ExecuteAsync(CancellationToken ct = default)
    {
        return _service.ExecuteShowAsync(_config, null, _containerIdOverride, _modeOverride, ct);
    }
}

// 2. Generic Builder (For context-driven screens requiring VContainer Factories)
public struct WindowBuilder<TViewModel> where TViewModel : class
{
    private readonly WindowService _service;
    private readonly WindowConfig _config;
    
    private TViewModel _prebuiltViewModel;
    private string _containerIdOverride;
    private StackMode? _modeOverride;

    internal WindowBuilder(WindowService service, WindowConfig config)
    {
        _service = service;
        _config = config;
        _prebuiltViewModel = null;
        _containerIdOverride = null;
        _modeOverride = null;
    }

    public WindowBuilder<TViewModel> WithViewModel(TViewModel viewModel)
    {
        _prebuiltViewModel = viewModel;
        return this;
    }

    public WindowBuilder<TViewModel> InContainer(string containerId)
    {
        _containerIdOverride = containerId;
        return this;
    }

    public WindowBuilder<TViewModel> WithMode(StackMode mode)
    {
        _modeOverride = mode;
        return this;
    }

    public UniTask ExecuteAsync(CancellationToken ct = default)
    {
        return _service.ExecuteShowAsync(_config, _prebuiltViewModel, _containerIdOverride, _modeOverride, ct);
    }
}