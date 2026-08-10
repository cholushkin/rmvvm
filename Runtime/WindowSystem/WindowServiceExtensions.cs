// todo: Add overloads that support passing localized string keys instead of raw strings

using System.Threading;
using Cysharp.Threading.Tasks;
using Game.UI.Dialogs;

namespace Game.UI.WindowSystem
{
    public static class WindowServiceExtensions
    {
        public static UniTask<bool> ShowConfirmAsync(
            this IWindowService windowService, 
            WindowConfig config, 
            string title, 
            string message, 
            string confirmText = "Confirm", 
            string cancelText = "Cancel", 
            CancellationToken ct = default)
        {
            return windowService.ShowModalAsync<ConfirmDialogViewModel, bool>(
                config,
                vm => vm.Setup(title, message, confirmText, cancelText), 
                cancellationToken: ct);
        }

        public static UniTask<bool> ShowAlertAsync(
            this IWindowService windowService, 
            WindowConfig config, 
            string title, 
            string message, 
            string okText = "OK", 
            CancellationToken ct = default)
        {
            return windowService.ShowModalAsync<AlertDialogViewModel, bool>(
                config,
                vm => vm.Setup(title, message, okText), 
                cancellationToken: ct);
        }

        public static UniTask ShowErrorAsync(
            this IWindowService windowService, 
            WindowConfig config, 
            string message, 
            CancellationToken ct = default)
        {
            return windowService.ShowAlertAsync(config, "Error", message, "Dismiss", ct);
        }
    }
}