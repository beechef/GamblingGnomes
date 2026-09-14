using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.Player
{
	// Paints this player in the colour their index was handed, so a table of identical gnomes can be told
	// apart at a glance. Where the colour is worn — which slot, which submesh of it, which shader property —
	// belongs to the model being drawn: the hat on a gnome, wherever art decides on the next body. So this
	// stays a mechanism about colour, and a body that changes model mid-hand wears it wherever the new one
	// says.
	//
	// Painted through a MaterialPropertyBlock, never by assigning a material: the outfit material is shared
	// by every gnome on the table, so writing to it would repaint all of them, and assigning a copy would
	// leak an instance per player and throw away the outline pass PlayerVisual appends. A block is
	// per-renderer, costs no instance, and leaves the material list untouched.
	public class PlayerColorVisual : NetworkBehaviour
	{
		[Header("References")]
		[Required]
		[SerializeField] private PlayerColorDatabase _database;

		[SerializeField] private PlayerData _data;
		[SerializeField] private PlayerVisual _visual;

		private readonly List<(Renderer Renderer, int SubmeshIndex)> _tinted = new();

		private MaterialPropertyBlock _block;

		public override void OnNetworkSpawn()
		{
			if (!_data) _data = NetworkObject.GetComponent<PlayerData>();
			if (!_visual) _visual = NetworkObject.GetComponent<PlayerVisual>();
			if (!_data || !_visual || !_database) return;

			_data.ColorIndex.OnValueChanged += HandleColorIndexChanged;
			_visual.OnAppearanceChanged += Apply;

			// The index is usually handed out before this client ever hears of the player, so there is no
			// change coming to wake this up — the value as it already stands is the whole story.
			Apply();
		}

		public override void OnNetworkDespawn()
		{
			if (_visual) _visual.OnAppearanceChanged -= Apply;
			if (_data) _data.ColorIndex.OnValueChanged -= HandleColorIndexChanged;
		}

		private void HandleColorIndexChanged(int previous, int current) => Apply();

		private void Apply()
		{
			// What the last model tinted comes off first, so a body switching to one that wears the colour
			// somewhere else does not keep it in both places.
			foreach (var (renderer, submeshIndex) in _tinted)
			{
				if (renderer) renderer.SetPropertyBlock(null, submeshIndex);
			}

			_tinted.Clear();

			var model = _visual.Model;
			if (!model) return;

			var color = _database.Get(_data.ColorIndex.Value);
			_block ??= new MaterialPropertyBlock();

			foreach (var tint in model.Tints)
			{
				if (string.IsNullOrEmpty(tint.ColorProperty) || !_visual.TryGetRenderer(tint.Slot, out var renderer)) continue;

				_block.Clear();
				_block.SetColor(tint.ColorProperty, color);
				renderer.SetPropertyBlock(_block, tint.SubmeshIndex);

				_tinted.Add((renderer, tint.SubmeshIndex));
			}
		}
	}
}
