using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.UI.WindowSystem;

namespace Game.UI.Dialogs
{
    public class AlertDialogComposer : WindowComposer<AlertDialogViewModel>
    {
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _messageText;
        [SerializeField] private TMP_Text _okButtonText;
        [SerializeField] private Button _okButton;

        protected override void Bind(AlertDialogViewModel viewModel)
        {
            if (_titleText != null) _titleText.text = viewModel.Title;
            if (_messageText != null) _messageText.text = viewModel.Message;
            if (_okButtonText != null) _okButtonText.text = viewModel.OkText;

            _okButton.onClick.RemoveAllListeners();
            _okButton.onClick.AddListener(viewModel.Acknowledge);
        }
    }
}