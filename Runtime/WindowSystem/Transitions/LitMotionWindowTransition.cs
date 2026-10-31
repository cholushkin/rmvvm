using System.Threading;
using Cysharp.Threading.Tasks;
using Game.UI.WindowSystem.Transitions;
using LitMotion;
using UnityEngine;

namespace Game.UI.WindowSystem
{
    public class LitMotionWindowTransition : MonoBehaviour, IWindowTransition
    {
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private float _duration = 0.3f;

        public async UniTask PlayShowAsync(CancellationToken ct)
        {
            if (_canvasGroup == null) return;

            _canvasGroup.alpha = 0f;
            await LMotion.Create(0f, 1f, _duration)
                .Bind(_canvasGroup, static (a, cg) => cg.alpha = a)
                .ToUniTask(cancellationToken: ct);
        }

        public async UniTask PlayHideAsync(CancellationToken ct)
        {
            if (_canvasGroup == null) return;

            _canvasGroup.alpha = 1f;
            await LMotion.Create(1f, 0f, _duration)
                .Bind(_canvasGroup, static (a, cg) => cg.alpha = a)
                .ToUniTask(cancellationToken: ct);
        }
    }
}
