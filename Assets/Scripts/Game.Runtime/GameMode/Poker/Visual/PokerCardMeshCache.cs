using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.GameMode.Poker.Visual
{
	// The card faces live in one sprite atlas, and every card draws through one shared material, so what
	// tells two cards apart has to travel with the mesh: each (front, back) pair gets a copy of the card's
	// box whose UV2 holds the atlas rect of whichever picture that face shows — the front on the face that
	// looks down -Z, the back everywhere else. The card shaders sample the global `_CardAtlas` at
	// `uv0 * rect.zw + rect.xy`, so no MaterialPropertyBlock is needed and the SRP Batcher can batch every
	// card, and a material that replaces the card's (`Card_Wave`) still finds the right picture.
	public static class PokerCardMeshCache
	{
		private const int RectChannel = 2;

		private static readonly Dictionary<(Mesh, Sprite, Sprite), Mesh> Meshes = new();
		private static readonly List<Vector3> Normals = new();
		private static readonly List<Vector4> Rects = new();
		private static readonly int CardAtlasId = Shader.PropertyToID("_CardAtlas");

		private static Texture _atlas;
		private static bool _warnedUnpacked;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetStatics()
		{
			foreach (var mesh in Meshes.Values)
			{
				if (mesh) Object.Destroy(mesh);
			}

			Meshes.Clear();
			_atlas = null;
			_warnedUnpacked = false;
		}

		public static Mesh Get(Mesh baseMesh, Sprite front, Sprite back)
		{
			if (!baseMesh || !front || !back) return baseMesh;

			BindAtlas(front, back);

			var key = (baseMesh, front, back);
			if (Meshes.TryGetValue(key, out var cached) && cached) return cached;

			var mesh = Object.Instantiate(baseMesh);
			mesh.name = $"{baseMesh.name} ({front.name} / {back.name})";
			mesh.hideFlags = HideFlags.DontSave;

			var frontRect = RectOf(front);
			var backRect = RectOf(back);

			baseMesh.GetNormals(Normals);
			Rects.Clear();

			foreach (var normal in Normals) Rects.Add(normal.z < 0f ? frontRect : backRect);

			mesh.SetUVs(RectChannel, Rects);
			Meshes[key] = mesh;

			return mesh;
		}

		// Normalised against the texture the sprite is actually drawn from, which is the atlas page once it is
		// packed. Tight packing and rotation are off on the atlas, or this rect would not describe the picture.
		private static Vector4 RectOf(Sprite sprite)
		{
			var texture = sprite.texture;
			var rect = sprite.textureRect;

			return new Vector4(rect.x / texture.width, rect.y / texture.height, rect.width / texture.width, rect.height / texture.height);
		}

		private static void BindAtlas(Sprite front, Sprite back)
		{
			var texture = front.texture;

			// Two pictures on two textures means the faces are not packed together, and one global texture can
			// only hold one of them.
			if (texture != back.texture && !_warnedUnpacked)
			{
				_warnedUnpacked = true;
				Debug.LogWarning($"[{nameof(PokerCardMeshCache)}] '{front.name}' and '{back.name}' are on different textures; the card sprites must be packed into one atlas (Cards.spriteatlasv2, Sprite Packer Mode: Sprite Atlas V2).");
			}

			if (_atlas == texture) return;

			_atlas = texture;
			Shader.SetGlobalTexture(CardAtlasId, texture);
		}
	}
}
