using Cysharp.Threading.Tasks;
using Game.UI.WindowSystem;

namespace Game.UI.Dialogs
{
    public class AlertDialogViewModel : IModalViewModel<bool>
    {
        public string Title { get; private set; }
        public string Message { get; private set; }
        public string OkText { get; private set; }

        
        private readonly UniTaskCompletionSource<bool> _tcs = new();

        public UniTask<bool> ResultTask => _tcs.Task;

        public void Setup(string title, string message, string okText)
        {
            Title = title;
            Message = message;
            OkText = okText;
        }

        public void Acknowledge()
        {            
            _tcs.TrySetResult(true);
        }
    }
}