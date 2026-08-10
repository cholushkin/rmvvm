using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.UI.WindowSystem.Transitions;
using UnityEngine;

namespace Game.UI.WindowSystem
{
    public class DOTweenWindowTransition : MonoBehaviour, IWindowTransition
    {
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private float _duration = 0.3f;

        public async UniTask PlayShowAsync(CancellationToken ct)
        {
            if (_canvasGroup == null) return;
            
            _canvasGroup.alpha = 0f;
            await DOTween.To(() => _canvasGroup.alpha, x => _canvasGroup.alpha = x, 1f, _duration)
                .WithCancellation(ct);
        }

        public async UniTask PlayHideAsync(CancellationToken ct)
        {
            if (_canvasGroup == null) return;
            
            _canvasGroup.alpha = 1f;
            await DOTween.To(() => _canvasGroup.alpha, x => _canvasGroup.alpha = x, 0f, _duration)
                .WithCancellation(ct);
        }
    }
}