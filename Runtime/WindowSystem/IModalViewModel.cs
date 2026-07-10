// - todo: evaluate if we need a non-generic IModalViewModel for modals without returns

using Cysharp.Threading.Tasks;

namespace Game.UI.WindowSystem
{
    /// <summary>
    /// Applied to any ViewModel that represents an awaitable overlay window.
    /// </summary>
    /// <typeparam name="TResult">The data payload returned when the user completes the interaction.</typeparam>
    public interface IModalViewModel<TResult>
    {
        /// <summary>
        /// The task the WindowService will await. Complete this task via a UniTaskCompletionSource 
        /// when the user clicks 'Confirm' or 'Cancel'.
        /// </summary>
        UniTask<TResult> ResultTask { get; }
    }
}