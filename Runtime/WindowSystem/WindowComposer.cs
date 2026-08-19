// todo: Cache IWindowTransition and CanvasGroup in Awake to avoid GetComponent allocations
// idea: Expose public events for OnWindowShown and OnWindowHidden

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.UI.WindowSystem.Transitions;
using UnityEngine;

public abstract class WindowComposer : MonoBehaviour
{
    public bool IsAnimating { get; private set; }

    public abstract Type GetViewModelType();
    public abstract void BindBoxed(object viewModel);
    public abstract bool HasViewModel(object viewModel);
    public abstract object GetViewModelBoxed();

    public virtual UniTask ShowAsync()
    {
        gameObject.SetActive(true);

        var transition = GetComponent<IWindowTransition>();
        if (transition == null)
        {
            return UniTask.CompletedTask;
        }

        return PlayTransitionAsync(transition, true, destroyCancellationToken);
    }

    public virtual UniTask HideAsync()
    {
        var transition = GetComponent<IWindowTransition>();
        if (transition == null)
        {
            if (gameObject != null) gameObject.SetActive(false);
            return UniTask.CompletedTask;
        }

        return HideWithTransitionAsync(transition);
    }


    private async UniTask PlayTransitionAsync(IWindowTransition transition, bool isShowing, CancellationToken ct)
    {
        IsAnimating = true;

        var canvasGroup = GetComponent<CanvasGroup>();
        bool originalInteractable = false;

        if (canvasGroup != null)
        {
            originalInteractable = canvasGroup.interactable;
            canvasGroup.interactable = false;
        }

        try
        {
            if (isShowing)
            {
                await transition.PlayShowAsync(ct);
            }
            else
            {
                await transition.PlayHideAsync(ct);
            }
        }
        finally
        {
            // Standard cleanup, assuming standard runtime execution.
            if (this != null) 
            {
                IsAnimating = false;

                if (canvasGroup != null)
                {
                    canvasGroup.interactable = originalInteractable;
                }
            }
        }
    }

    private async UniTask HideWithTransitionAsync(IWindowTransition transition)
    {
        await PlayTransitionAsync(transition, false, destroyCancellationToken);

        if (this != null && gameObject != null)
        {
            gameObject.SetActive(false);
        }
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

    public override object GetViewModelBoxed() => ViewModel;

    protected abstract void Bind(TViewModel viewModel);

    protected virtual void OnDestroy()
    {
        // R3 bindings hooked to destroyCancellationToken will clean up automatically.
    }
}