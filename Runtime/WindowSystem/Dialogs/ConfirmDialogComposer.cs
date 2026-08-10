using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.UI.WindowSystem;

namespace Game.UI.Dialogs
{
    public class ConfirmDialogComposer : WindowComposer<ConfirmDialogViewModel>
    {
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _messageText;
        [SerializeField] private TMP_Text _confirmButtonText;
        [SerializeField] private TMP_Text _cancelButtonText;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private Button _cancelButton;

        protected override void Bind(ConfirmDialogViewModel viewModel)
        {
            if (_titleText != null) _titleText.text = viewModel.Title;
            if (_messageText != null) _messageText.text = viewModel.Message;
            if (_confirmButtonText != null) _confirmButtonText.text = viewModel.ConfirmText;
            if (_cancelButtonText != null) _cancelButtonText.text = viewModel.CancelText;

            _confirmButton.onClick.RemoveAllListeners();
            _confirmButton.onClick.AddListener(viewModel.Confirm);

            _cancelButton.onClick.RemoveAllListeners();
            _cancelButton.onClick.AddListener(viewModel.Cancel);
        }
    }
}