using System.Threading;
using Cysharp.Threading.Tasks;
using Game.UI.WindowSystem.Transitions;
using UnityEngine;

namespace Game.UI.WindowSystem
{
    public class AnimatorWindowTransition : MonoBehaviour, IWindowTransition
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private string _showStateName = "Show";
        [SerializeField] private string _hideStateName = "Hide";

        public async UniTask PlayShowAsync(CancellationToken ct)
        {
            if (_animator == null) return;
            
            _animator.Play(_showStateName);
            await UniTask.WaitUntil(() => StateFinished(_showStateName), cancellationToken: ct);
        }

        public async UniTask PlayHideAsync(CancellationToken ct)
        {
            if (_animator == null) return;
            
            _animator.Play(_hideStateName);
            await UniTask.WaitUntil(() => StateFinished(_hideStateName), cancellationToken: ct);
        }
        
        private bool StateFinished(string stateName)
        {
            var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            return stateInfo.IsName(stateName) && stateInfo.normalizedTime >= 1.0f;
        }
    }
}