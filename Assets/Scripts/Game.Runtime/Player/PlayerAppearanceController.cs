using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.Player
{
	// Holds the requests to draw this body as another model or in another version, and hands the winners
	// to PlayerVisual — the same split PlayerMaterialOverrideController makes: how many callers there are
	// is a question about callers, and PlayerVisual only knows how to draw an answer. A caller names the
	// model or the version, never meshes or materials; the model resolves those. Local to this client,
	// because what one player is seeing is not something the table shares.
	//
	// Last one in wins on each axis, and dropping a request puts back what the caller before it asked for.
	public class PlayerAppearanceController : MonoBehaviour
	{
		[SerializeField] private PlayerVisual _visual;

		private readonly List<(object Handle, PlayerModel Model)> _models = new();
		private readonly List<(object Handle, PlayerLookVersion Version)> _versions = new();

		private void Awake()
		{
			if (!_visual) _visual = GetComponentInParent<PlayerVisual>();
		}

		private void OnDisable()
		{
			if (_models.Count == 0 && _versions.Count == 0) return;

			_models.Clear();
			_versions.Clear();
			Apply();
		}

		public void SetModel(object handle, PlayerModel model)
		{
			if (handle == null || !model) return;

			Remove(_models, handle);
			_models.Add((handle, model));

			Apply();
		}

		public void ClearModel(object handle)
		{
			if (handle == null || !Remove(_models, handle)) return;

			Apply();
		}

		public void SetVersion(object handle, PlayerLookVersion version)
		{
			if (handle == null) return;

			Remove(_versions, handle);
			_versions.Add((handle, version));

			Apply();
		}

		public void ClearVersion(object handle)
		{
			if (handle == null || !Remove(_versions, handle)) return;

			Apply();
		}

		private static bool Remove<T>(List<(object Handle, T Value)> requests, object handle)
		{
			for (var i = requests.Count - 1; i >= 0; i--)
			{
				if (!ReferenceEquals(requests[i].Handle, handle)) continue;

				requests.RemoveAt(i);
				return true;
			}

			return false;
		}

		private void Apply()
		{
			if (!_visual) return;

			_visual.SetModelOverride(_models.Count > 0 ? _models[^1].Model : null);
			_visual.SetVersion(_versions.Count > 0 ? _versions[^1].Version : PlayerLookVersion.Cartoon);
		}
	}
}
