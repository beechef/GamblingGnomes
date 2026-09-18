using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// A body that grows something the artist sculpted into the mesh. Written through
	// PlayerBlendShapeController rather than onto the renderer, because PlayerVisual hands renderers new
	// meshes whenever a model or an outfit changes and a weight written here would go with them.
	//
	// A shape rather than a variant: the art shipped this as one blend shape on the outfit mesh, so there
	// is no second mesh to swap to and nothing for PropVariantController to switch on. Naming the shape is
	// the whole of what this effect decides — how far to push it and how long it takes are the two numbers
	// worth retuning.
	[CreateAssetMenu(fileName = "Hallucination_BlendShape", menuName = "Game/Poker/Hallucination/Blend Shape")]
	public class PokerHallucinationBlendShapeEffect : PokerHallucinationEffect
	{
		[Tooltip("Whose body. Others is the usual answer: the viewer renders their own rig as hands alone.")]
		[Required]
		[SerializeField] private PokerHallucinationTarget _target;

		[Tooltip("The shape as the mesh names it, prefix and all. Offered from what the art actually carries, but a shape the artist has not exported yet can still be typed: a name with no mesh behind it is warned about and skipped.")]
		[ValueDropdown(nameof(ShapeNames), AppendNextDrawer = true)]
		[SerializeField] private string _shape = "blendShape.outfit_mocvu";

		[Tooltip("How far it is pushed once it has finished arriving. 100 is the shape as it was sculpted.")]
		[PropertyRange(0f, 100f)]
		[SerializeField] private float _weight = 100f;

		[Tooltip("Seconds it takes to grow. Snapping reads as a bug rather than as a symptom.")]
		[MinValue(0f)]
		[SerializeField] private float _duration = 0.7f;

		[SerializeField] private Ease _ease = Ease.OutBack;

		public PokerHallucinationTarget Target => _target;
		public string Shape => _shape;
		public float Weight => _weight;
		public float Duration => Mathf.Max(0f, _duration);
		public Ease Ease => _ease;

		protected override PokerHallucinationEffectBehaviour Attach(GameObject host) => host.AddComponent<PokerHallucinationBlendShapeBehaviour>();

		// Every shape the character art carries, so the one name that has to match a mesh exactly is picked
		// rather than typed. Editor only, and only while the dropdown is open.
		private static IEnumerable<string> ShapeNames()
		{
#if UNITY_EDITOR
			var names = new SortedSet<string>();

			foreach (var guid in AssetDatabase.FindAssets("t:Mesh", new[] { "Assets/Art" }))
			{
				var path = AssetDatabase.GUIDToAssetPath(guid);

				foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
				{
					if (asset is not Mesh mesh) continue;

					for (var i = 0; i < mesh.blendShapeCount; i++) names.Add(mesh.GetBlendShapeName(i));
				}
			}

			return names;
#else
			return System.Array.Empty<string>();
#endif
		}
	}
}
