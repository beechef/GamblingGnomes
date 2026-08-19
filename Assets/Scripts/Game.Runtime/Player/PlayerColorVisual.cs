using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

namespace Game.Runtime.Player
{
	// Paints this player in the colour their index was handed, so a table of identical gnomes can be told
	// apart at a glance. Which renderers wear it is authored — the hat today, whatever else the art grows
	// tomorrow — so this stays a mechanism about colour rather than a component that knows what a hat is.
	//
	// Painted through a MaterialPropertyBlock, never by assigning a material: the outfit material is shared
	// by every gnome on the table, so writing to it would repaint all of them, and assigning a copy would
	// leak an instance per player and throw away the outline pass PlayerVisual appends. A block is
	// per-renderer, costs no instance, and leaves the material list untouched.
	public class PlayerColorVisual : NetworkBehaviour
	{
		[Header("Paint")]
		[Tooltip("The renderers wearing this player's colour, on both rigs — everyone else's gnome and the owner's own hands are two different sets of meshes showing the same player.")]
		[SerializeField] private List<Renderer> _renderers = new();

		[Tooltip("Which material slot on those renderers takes the colour. The outline pass is appended after the authored ones, so a slot index stays put.")]
		[MinValue(0)]
		[SerializeField] private int _materialIndex;

		[Tooltip("Shader property to write. URP's Lit and Unlit both call it _BaseColor.")]
		[SerializeField] private string _colorProperty = "_BaseColor";

		[Header("References")]
		[Required]
		[SerializeField] private PlayerColorDatabase _database;

		[SerializeField] private PlayerData _data;

		private MaterialPropertyBlock _block;
		private int _colorPropertyId;

		public override void OnNetworkSpawn()
		{
			if (!_data) _data = NetworkObject.GetComponent<PlayerData>();
			if (!_data || !_database) return;

			_colorPropertyId = Shader.PropertyToID(_colorProperty);

			_data.ColorIndex.OnValueChanged += HandleColorIndexChanged;

			// The index is usually handed out before this client ever hears of the player, so there is no
			// change coming to wake this up — the value as it already stands is the whole story.
			Apply(_data.ColorIndex.Value);
		}

		public override void OnNetworkDespawn()
		{
			if (_data) _data.ColorIndex.OnValueChanged -= HandleColorIndexChanged;
		}

		private void HandleColorIndexChanged(int previous, int current) => Apply(current);

		private void Apply(int index)
		{
			var color = _database.Get(index);

			_block ??= new MaterialPropertyBlock();

			foreach (var renderer in _renderers)
			{
				if (!renderer) continue;

				// Read back first: another block may already be carrying something on this slot, and
				// replacing it outright would quietly drop whatever that was.
				renderer.GetPropertyBlock(_block, _materialIndex);
				_block.SetColor(_colorPropertyId, color);
				renderer.SetPropertyBlock(_block, _materialIndex);
			}
		}
	}
}
