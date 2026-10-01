using Game.Runtime.GameMode.Poker.Player;
using Game.Runtime.Player;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// A player's eyes going red as they go under: the tint's weight is their hallucination rate over the
	// ceiling, nothing at zero and the full tint at the ceiling. Read off replicated state, so every screen
	// sees the same eyes.
	//
	// Written per submesh through a property block on the eyes alone (PlayerModel.Eyes), so the face around
	// them and whatever material PlayerVisual paints are left as they are; rewritten after every repaint.
	public class PokerHallucinationEyeTint : MonoBehaviour
	{
		[Tooltip("Colour the eyes turn. Its alpha scales how far they go at the ceiling.")]
		[SerializeField] private Color _tint = new(1f, 0.05f, 0.05f, 1f);

		[Tooltip("Colour properties tried in order on the eyes' material; the first it has is the one tinted.")]
		[SerializeField] private string[] _colorProperties = { "_Color", "_BaseColor" };

		[Header("References")]
		[SerializeField] private PokerPlayerData _data;
		[SerializeField] private PlayerVisual _visual;

		private MaterialPropertyBlock _block;

		private void Awake()
		{
			if (!_data) _data = GetComponentInParent<PokerPlayerData>();
			if (!_visual) _visual = GetComponentInParent<PlayerVisual>();
			_block = new MaterialPropertyBlock();
		}

		private void OnEnable()
		{
			if (_data) _data.OnHallucinationChanged += HandleHallucinationChanged;
			if (_visual) _visual.OnAppearanceChanged += Apply;

			Apply();
		}

		private void OnDisable()
		{
			if (_visual) _visual.OnAppearanceChanged -= Apply;
			if (_data) _data.OnHallucinationChanged -= HandleHallucinationChanged;

			Write(0f);
		}

		private void HandleHallucinationChanged(int previous, int current) => Apply();

		private void Apply()
		{
			var rate = _data ? _data.HallucinationRate.Value / (float)PokerPlayerData.MaxHallucination : 0f;
			Write(Mathf.Clamp01(rate));
		}

		private void Write(float weight)
		{
			var model = _visual ? _visual.Model : null;
			if (!model) return;

			foreach (var eye in model.Eyes)
			{
				if (!_visual.TryGetRenderer(eye.Slot, out var renderer) || !renderer) continue;

				var materials = renderer.sharedMaterials;
				if (eye.SubmeshIndex >= materials.Length || !materials[eye.SubmeshIndex]) continue;

				var material = materials[eye.SubmeshIndex];
				var property = ColorPropertyOf(material);

				_block.Clear();
				if (property != null)
				{
					var authored = material.GetColor(property);
					_block.SetColor(property, Color.Lerp(authored, _tint, weight * _tint.a));
				}

				renderer.SetPropertyBlock(_block, eye.SubmeshIndex);
			}
		}

		private string ColorPropertyOf(Material material)
		{
			foreach (var property in _colorProperties)
			{
				if (material.HasProperty(property)) return property;
			}

			return null;
		}
	}
}
