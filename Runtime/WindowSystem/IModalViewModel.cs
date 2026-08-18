// - todo: evaluate if we need a non-generic IModalViewModel for modals without returns

using Cysharp.Threading.Tasks;

namespace Game.UI.WindowSystem
{
    /// Applied to any ViewModel that represents an awaitable overlay window.
    /// "TResult" The data payload returned when the user completes the interaction.
    public interface IModalViewModel<TResult>
    {
        /// The task the WindowService will await. Complete this task via a UniTaskCompletionSource 
        /// when the user clicks 'Confirm' or 'Cancel'.
        UniTask<TResult> ResultTask { get; }
    }
}