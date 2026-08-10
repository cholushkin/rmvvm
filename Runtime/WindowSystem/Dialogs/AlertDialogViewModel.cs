// todo: Support RichText tags filtering in case the message comes from an untrusted source
// idea: Allow mapping different severities (Info, Warning, Error) to change the popup theme

using Cysharp.Threading.Tasks;
using Game.UI.WindowSystem;

namespace Game.UI.Dialogs
{
    public class AlertDialogViewModel : IModalViewModel<bool>
    {
        public string Title { get; private set; }
        public string Message { get; private set; }
        public string OkText { get; private set; }

        private UniTaskCompletionSource<bool> _tcs;

        public UniTask<bool> ResultTask => _tcs.Task;

        public void Setup(string title, string message, string okText)
        {
            Title = title;
            Message = message;
            OkText = okText;
            
            _tcs = new UniTaskCompletionSource<bool>();
        }

        public void Acknowledge() => _tcs.TrySetResult(true);
    }
}