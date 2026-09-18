using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.Player
{
	// Pushes a blend shape on whichever of this player's meshes carries it. A shape is asked for by name
	// and never by index: PlayerVisual hands renderers new meshes whenever the model, the outfit or the
	// version changes, and an index captured against one mesh means something else on the next.
	//
	// That same swap is why this re-resolves on OnAppearanceChanged. Assigning sharedMesh resets every
	// weight on the renderer to zero, so a shape worn through a model change would come off on its own
	// while whatever asked for it had not moved at all — the silent half of a bug nobody would trace back.
	//
	// Modifiers **compose**, the way PlayerBoneScaleController's do: several callers pushing one shape add
	// up rather than the last one deciding, so two effects the hallucination ladder drew together cannot
	// erase each other. A shape is a base and a list of modifiers, and both appear only when somebody asks
	// — a player nobody is reshaping holds no state and its LateUpdate returns on the first line.
	//
	// It carries no game meaning. What a shape means is the caller's business.
	public class PlayerBlendShapeController : MonoBehaviour
	{
		// One mesh carrying the shape, and the weight it was authored with. Several renderers can answer to
		// one name — the body rig and the hand-only one are two meshes with the same shapes on them, and
		// whichever this client draws is the one that shows.
		private struct Target
		{
			public SkinnedMeshRenderer Renderer;
			public int Index;
			public float Base;
		}

		// A shape's own stack. Built the first time that shape is asked for and thrown away with the last
		// modifier on it.
		private class ShapeStack
		{
			public readonly List<PlayerBlendShapeModifier> Modifiers = new();
			public readonly List<Target> Targets = new();
		}

		[Tooltip("The one writer of what this player's renderers draw. Its OnAppearanceChanged is what says the meshes moved, and a shape resolved against the old ones has to be resolved again.")]
		[SerializeField] private PlayerVisual _visual;

		private readonly Dictionary<string, ShapeStack> _shapes = new();

		private readonly List<SkinnedMeshRenderer> _renderers = new();

		public int ShapeCount => _shapes.Count;

		// The caller keeps what comes back and writes into it. Nothing is removed by asking for the same
		// shape again — two calls are two modifiers, which is what lets one effect stack with itself across
		// two rungs without either of them knowing about the other.
		public PlayerBlendShapeModifier Add(string shape, float weight = 0f)
		{
			if (string.IsNullOrEmpty(shape)) return null;

			if (!_shapes.TryGetValue(shape, out var stack))
			{
				stack = new ShapeStack();
				_shapes[shape] = stack;

				Resolve(shape, stack);

				// Loud, because a body carrying no mesh with this shape is a setup that cannot possibly work,
				// and skipping it quietly is how an effect runs for weeks resolving bodies and moving nothing.
				if (stack.Targets.Count == 0) Debug.LogWarning($"[{name}] No mesh on this player carries the blend shape '{shape}'; nothing will be reshaped.", this);
			}

			var modifier = new PlayerBlendShapeModifier
			{
				Weight = weight,
				Owner = this,
				Shape = shape
			};

			stack.Modifiers.Add(modifier);
			return modifier;
		}

		// Called through PlayerBlendShapeModifier.Remove, which is where a caller reaches for it.
		internal void Remove(PlayerBlendShapeModifier modifier)
		{
			if (modifier == null || string.IsNullOrEmpty(modifier.Shape)) return;
			if (!_shapes.TryGetValue(modifier.Shape, out var stack)) return;

			stack.Modifiers.Remove(modifier);
			if (stack.Modifiers.Count > 0) return;

			// The last one off puts the shape back where it was authored and takes the stack with it.
			Write(stack, 0f);
			_shapes.Remove(modifier.Shape);
		}

		// What this shape comes out at with everything currently on it. Public because the answer is worth
		// reading while tuning two effects that are meant to work together.
		public float Resolve(string shape)
		{
			if (string.IsNullOrEmpty(shape) || !_shapes.TryGetValue(shape, out var stack)) return 0f;

			return Sum(stack);
		}

		private void OnEnable()
		{
			if (_visual) _visual.OnAppearanceChanged += Rebind;
		}

		private void OnDisable()
		{
			if (_visual) _visual.OnAppearanceChanged -= Rebind;

			foreach (var pair in _shapes)
			{
				Write(pair.Value, 0f);

				// Orphaned rather than left pointing here: a modifier outliving this component must not push a
				// shape through a stack that no longer exists.
				foreach (var modifier in pair.Value.Modifiers) modifier.Owner = null;
			}

			_shapes.Clear();
		}

		// Nothing to do at all while nothing is reshaped, which is the common case for every player at the
		// table who is not hallucinating.
		private void LateUpdate()
		{
			if (_shapes.Count == 0) return;

			foreach (var pair in _shapes)
			{
				Write(pair.Value, Sum(pair.Value));
			}
		}

		// The meshes moved under us. The old renderers have already had their weights reset by the swap that
		// caused this, so there is nothing to put back — only the new set to find.
		private void Rebind()
		{
			foreach (var pair in _shapes)
			{
				pair.Value.Targets.Clear();
				Resolve(pair.Key, pair.Value);
			}
		}

		private void Resolve(string shape, ShapeStack stack)
		{
			var root = _visual ? _visual.transform : transform;
			root.GetComponentsInChildren(true, _renderers);

			foreach (var renderer in _renderers)
			{
				var mesh = renderer.sharedMesh;
				if (!mesh) continue;

				var index = FindShapeIndex(mesh, shape);
				if (index < 0) continue;

				// The authored weight, read at the one moment it is still there to read: after this the
				// renderer carries whatever the stack resolves to.
				stack.Targets.Add(new Target
				{
					Renderer = renderer,
					Index = index,
					Base = renderer.GetBlendShapeWeight(index)
				});
			}
		}

		// The FBX importer names a shape "<blendShape node>.<channel>", and the node name is whatever Maya
		// numbered it on that export (blendShape → blendShape1), so a re-export renames every shape without
		// the art changing. The channel is what the artist actually named; matched on that when the full
		// name misses.
		private static int FindShapeIndex(Mesh mesh, string shape)
		{
			var index = mesh.GetBlendShapeIndex(shape);
			if (index >= 0) return index;

			var channel = ChannelOf(shape);

			for (var i = 0; i < mesh.blendShapeCount; i++)
			{
				if (ChannelOf(mesh.GetBlendShapeName(i)) == channel) return i;
			}

			return -1;
		}

		private static string ChannelOf(string shape)
		{
			var dot = shape.LastIndexOf('.');
			return dot < 0 ? shape : shape.Substring(dot + 1);
		}

		private static float Sum(ShapeStack stack)
		{
			var total = 0f;

			foreach (var modifier in stack.Modifiers) total += modifier.Weight;

			return total;
		}

		private static void Write(ShapeStack stack, float weight)
		{
			foreach (var target in stack.Targets)
			{
				if (!target.Renderer) continue;

				target.Renderer.SetBlendShapeWeight(target.Index, Mathf.Clamp(target.Base + weight, 0f, 100f));
			}
		}
	}
}
