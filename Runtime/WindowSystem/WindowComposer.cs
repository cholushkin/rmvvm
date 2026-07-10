// todo: Abstract LitMotion logic into a base transition class
// todo: Add 'OnRefresh' hook for ViewModel updates without reopening

using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

public abstract class WindowComposer : MonoBehaviour
{
    public abstract Type GetViewModelType();
    public abstract void BindBoxed(object viewModel);
    public abstract bool HasViewModel(object viewModel);

    public virtual UniTask ShowAsync()
    {
        gameObject.SetActive(true);
        return UniTask.CompletedTask;
    }

    public virtual UniTask HideAsync()
    {
        gameObject.SetActive(false);
        return UniTask.CompletedTask;
    }
}

public abstract class WindowComposer<TViewModel> : WindowComposer where TViewModel : class
{
    protected TViewModel ViewModel { get; private set; }

    public override Type GetViewModelType() => typeof(TViewModel);

    public override void BindBoxed(object viewModel)
    {
        if (viewModel is not TViewModel typedViewModel)
        {
            Debug.LogError($"[WindowComposer] Binding failed. Expected {typeof(TViewModel).Name}, received {viewModel?.GetType().Name}.");
            return;
        }

        ViewModel = typedViewModel;
        Bind(ViewModel);
    }

    public override bool HasViewModel(object viewModel)
    {
        return ViewModel == viewModel;
    }

    protected abstract void Bind(TViewModel viewModel);

    protected virtual void OnDestroy()
    {
        // R3 bindings hooked to destroyCancellationToken will natively dispose here.
    }
}