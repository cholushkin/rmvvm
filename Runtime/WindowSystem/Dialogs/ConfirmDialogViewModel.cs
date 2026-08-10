// todo: Add support for timeout cancellation
// idea: Expose an IObservable for countdown ticks if a timeout is active

using Cysharp.Threading.Tasks;
using Game.UI.WindowSystem;

namespace Game.UI.Dialogs
{
    public class ConfirmDialogViewModel : IModalViewModel<bool>
    {
        public string Title { get; private set; }
        public string Message { get; private set; }
        public string ConfirmText { get; private set; }
        public string CancelText { get; private set; }

        private UniTaskCompletionSource<bool> _tcs;

        public UniTask<bool> ResultTask => _tcs.Task;

        public void Setup(string title, string message, string confirmText, string cancelText)
        {
            Title = title;
            Message = message;
            ConfirmText = confirmText;
            CancelText = cancelText;
            
            _tcs = new UniTaskCompletionSource<bool>();
        }

        public void Confirm() => _tcs.TrySetResult(true);
        public void Cancel() => _tcs.TrySetResult(false);
    }
}