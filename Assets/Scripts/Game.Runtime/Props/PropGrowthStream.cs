using DG.Tweening;
using UnityEngine;

namespace Game.Runtime.Props
{
	// A stream mesh drawn out by its material's growth (0 none of it, 1 all of it, revealed from uv v = 1 down to
	// v = 0), poured out each time it is switched on. A point rides the leading edge, so whatever is carried on it
	// comes down the stream and rests where the stream ends.
	public class PropGrowthStream : MonoBehaviour
	{
		private static readonly int GrowthId = Shader.PropertyToID("_Growth");

		[Tooltip("The stream mesh; its material reads _Growth.")]
		[SerializeField] private MeshRenderer _renderer;

		[Tooltip("Carried along the stream's centre at its leading edge and left at its end. Keeps its own rotation.")]
		[SerializeField] private Transform _tip;

		[Tooltip("Seconds after the stream is switched on before it starts to pour: when the clip opens the mouth.")]
		[Min(0f)]
		[SerializeField] private float _delay;

		[Tooltip("Seconds from the first drop to the stream reaching its end.")]
		[Min(0.01f)]
		[SerializeField] private float _duration = 0.6f;

		[SerializeField] private Ease _ease = Ease.InQuad;

		[Tooltip("Points the stream's centre line is measured at; more follow its bends closer.")]
		[Min(2)]
		[SerializeField] private int _centreSamples = 12;

		private MaterialPropertyBlock _block;
		private Vector3[] _centre;
		private Tween _tween;

		private void Awake()
		{
			_block = new MaterialPropertyBlock();
			_centre = MeasureCentre();
		}

		private void OnEnable()
		{
			_tween?.Kill();
			SetGrowth(0f);
			_tween = DOVirtual.Float(0f, 1f, _duration, SetGrowth).SetDelay(_delay).SetEase(_ease);
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

			if (_tip && _centre != null) _tip.position = _renderer.transform.TransformPoint(CentreAt(1f - growth));
		}

		private Vector3 CentreAt(float v)
		{
			var at = Mathf.Clamp01(v) * (_centre.Length - 1);
			var index = Mathf.Min(Mathf.FloorToInt(at), _centre.Length - 2);
			return Vector3.Lerp(_centre[index], _centre[index + 1], at - index);
		}

		// The average of the vertices in each band of v, in the mesh's space; a band with none takes its neighbour's.
		private Vector3[] MeasureCentre()
		{
			var filter = _renderer ? _renderer.GetComponent<MeshFilter>() : null;
			var mesh = filter ? filter.sharedMesh : null;
			if (!mesh) return null;

			var vertices = mesh.vertices;
			var uvs = mesh.uv;
			var sums = new Vector3[_centreSamples];
			var counts = new int[_centreSamples];

			for (var i = 0; i < vertices.Length && i < uvs.Length; i++)
			{
				var band = Mathf.Clamp(Mathf.RoundToInt(uvs[i].y * (_centreSamples - 1)), 0, _centreSamples - 1);
				sums[band] += vertices[i];
				counts[band]++;
			}

			var centre = new Vector3[_centreSamples];
			for (var i = 0; i < _centreSamples; i++) centre[i] = counts[i] > 0 ? sums[i] / counts[i] : i > 0 ? centre[i - 1] : mesh.bounds.center;

			return centre;
		}
	}
}
