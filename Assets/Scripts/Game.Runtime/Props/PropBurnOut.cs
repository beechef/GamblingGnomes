using DG.Tweening;
using UnityEngine;

namespace Game.Runtime.Props
{
	// A prop that burns down once lit: its materials' burn parameter runs 0 to 1 over the duration, then it shrinks
	// away and is destroyed. Only a lit copy burns (Ignite); the template it was copied from never does.
	public class PropBurnOut : MonoBehaviour
	{
		[Tooltip("Float the prop's materials read: 0 whole, 1 burnt down. Renderers without it are unaffected.")]
		[SerializeField] private string _parameter = "_Burn";

		[Tooltip("Seconds from lit to burnt down.")]
		[Min(0.01f)]
		[SerializeField] private float _duration = 20f;

		[SerializeField] private Ease _ease = Ease.Linear;

		[Tooltip("Seconds it takes to shrink away once burnt down.")]
		[Min(0f)]
		[SerializeField] private float _shrinkDuration = 0.3f;

		[SerializeField] private Ease _shrinkEase = Ease.InBack;

		private Renderer[] _renderers;
		private MaterialPropertyBlock _block;
		private int _parameterId;
		private Sequence _sequence;

		private void Awake()
		{
			_renderers = GetComponentsInChildren<Renderer>(true);
			_block = new MaterialPropertyBlock();
			_parameterId = Shader.PropertyToID(_parameter);
		}

		private void OnDestroy() => _sequence?.Kill();

		public void Ignite()
		{
			_sequence?.Kill();
			SetBurn(0f);

			_sequence = DOTween.Sequence()
				.Append(DOVirtual.Float(0f, 1f, _duration, SetBurn).SetEase(_ease))
				.Append(transform.DOScale(Vector3.zero, _shrinkDuration).SetEase(_shrinkEase))
				.OnComplete(() => Destroy(gameObject))
				.SetLink(gameObject);
		}

		private void SetBurn(float burn)
		{
			foreach (var target in _renderers)
			{
				if (!target) continue;

				target.GetPropertyBlock(_block);
				_block.SetFloat(_parameterId, burn);
				target.SetPropertyBlock(_block);
			}
		}
	}
}
