// todo: Consider exposing an OnTransitionStarted event if other components need to sync with UI
// idea: Implement a custom inspector drawer to preview transitions in edit mode

using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.UI.WindowSystem.Transitions
{
    public interface IWindowTransition
    {
        UniTask PlayShowAsync(CancellationToken ct);
        UniTask PlayHideAsync(CancellationToken ct);
    }
}