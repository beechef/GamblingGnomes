using DG.Tweening;
using UnityEngine;

namespace Game.Runtime.Props
{
	// A stream mesh drawn out by its material's growth (0 none of it, 1 all of it, revealed from uv v = 1 down to
	// v = 0): poured from empty each time it is switched on.
	public class PropGrowthStream : MonoBehaviour
	{
		private static readonly int GrowthId = Shader.PropertyToID("_Growth");

		[Tooltip("The stream mesh; its material reads _Growth.")]
		[SerializeField] private MeshRenderer _renderer;

		[Tooltip("Seconds from the first drop to the stream reaching its end.")]
		[Min(0.01f)]
		[SerializeField] private float _duration = 0.6f;

		[SerializeField] private Ease _ease = Ease.InQuad;

		private MaterialPropertyBlock _block;
		private Tween _tween;

		private void Awake() => _block = new MaterialPropertyBlock();

		private void OnEnable()
		{
			_tween?.Kill();
			SetGrowth(0f);
			_tween = DOVirtual.Float(0f, 1f, _duration, SetGrowth).SetEase(_ease);
		}

		private void OnDisable()
		{
			_tween?.Kill();
			_tween = null;
		}

		private void SetGrowth(float growth)
		{
			if (!_renderer) return;

			_renderer.GetPropertyBlock(_block);
			_block.SetFloat(GrowthId, growth);
			_renderer.SetPropertyBlock(_block);
		}
	}
}
