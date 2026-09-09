using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.Player
{
	// Resizes bones without asking the Animator's permission. Every gnome clip carries a scale curve for
	// all 83 animated bones, all of them constant 1, so the Animator writes scale over the whole skeleton
	// every frame: a resize done once is gone by the next one, the same trap already written down for bone
	// positions. This writes in LateUpdate, after the Animator and before the Cinemachine brain samples,
	// because a scaled head moves the camera hanging off it.
	//
	// A bone is a base and a list of modifiers, and both appear only when somebody asks for them: a rig
	// nobody is scaling holds no state and its LateUpdate returns on the first line. The base is the scale
	// the bone was authored with, read once as the list is created — every frame rebuilds from that rather
	// than from the value read back, so a bone the clips do not animate cannot compound its own last write
	// into the next one.
	//
	// Modifiers **compose**: an effect that doubles a head and an effect that halves it both apply, and the
	// head comes out its authored size. The answer this replaced was last-caller-wins, where a shrink
	// arriving during a swell did not fight it but silently erased it — two effects the hallucination
	// ladder is free to draw together, one of them quietly doing nothing.
	//
	// Multiplies are gathered apart from adds, so **the result does not depend on the order they arrived
	// in**: `base × ∏multipliers + ∑adds`. Two effects registering either way round give the same bone.
	//
	// It carries no game meaning. What a swollen head means is the caller's business.
	[DefaultExecutionOrder(50)]
	public class PlayerBoneScaleController : MonoBehaviour
	{
		// A bone's own stack. Built the first time that bone is modified and thrown away with the last
		// modifier on it, so what this component costs is what is actually being scaled right now.
		private class BoneStack
		{
			public Vector3 Base;
			public readonly List<PlayerBoneScaleModifier> Modifiers = new();
		}

		private readonly Dictionary<Transform, BoneStack> _bones = new();

		public int BoneCount => _bones.Count;

		// The caller keeps what comes back and writes into it. Nothing is removed by handing in the same
		// arguments again — two calls are two modifiers, which is what lets one effect stack with itself
		// across two rungs without either of them knowing about the other.
		public PlayerBoneScaleModifier Add(Transform bone, Vector3 value, PlayerBoneScaleMode mode = PlayerBoneScaleMode.Multiply)
		{
			if (!bone) return null;

			if (!_bones.TryGetValue(bone, out var stack))
			{
				// The authored scale, read at the one moment it is still there to read: after this the bone
				// carries whatever the stack resolves to.
				stack = new BoneStack { Base = bone.localScale };
				_bones[bone] = stack;
			}

			var modifier = new PlayerBoneScaleModifier
			{
				Value = value,
				Mode = mode,
				Owner = this,
				Bone = bone
			};

			stack.Modifiers.Add(modifier);
			return modifier;
		}

		// Called through PlayerBoneScaleModifier.Remove, which is where a caller reaches for it.
		internal void Remove(PlayerBoneScaleModifier modifier)
		{
			if (modifier == null || !modifier.Bone) return;
			if (!_bones.TryGetValue(modifier.Bone, out var stack)) return;

			stack.Modifiers.Remove(modifier);
			if (stack.Modifiers.Count > 0) return;

			// The last one off puts the bone back where it was authored and takes the stack with it.
			if (modifier.Bone) modifier.Bone.localScale = stack.Base;
			_bones.Remove(modifier.Bone);
		}

		// What this bone comes out at with everything currently on it. Public because the answer is worth
		// reading while tuning two effects that are meant to work together.
		public Vector3 Resolve(Transform bone)
		{
			if (!bone) return Vector3.one;

			return _bones.TryGetValue(bone, out var stack) ? Resolve(stack) : bone.localScale;
		}

		private static Vector3 Resolve(BoneStack stack)
		{
			var product = Vector3.one;
			var sum = Vector3.zero;

			foreach (var modifier in stack.Modifiers)
			{
				if (modifier.Mode == PlayerBoneScaleMode.Multiply) product = Vector3.Scale(product, modifier.Value);
				else sum += modifier.Value;
			}

			return Vector3.Scale(stack.Base, product) + sum;
		}

		private void OnDisable()
		{
			foreach (var pair in _bones)
			{
				if (pair.Key) pair.Key.localScale = pair.Value.Base;

				// Orphaned rather than left pointing here: a modifier outliving this component must not put
				// a bone back through a stack that no longer exists.
				foreach (var modifier in pair.Value.Modifiers) modifier.Owner = null;
			}

			_bones.Clear();
		}

		// Nothing to do at all while nothing is scaled, which is the common case for every player at the
		// table who is not hallucinating.
		private void LateUpdate()
		{
			if (_bones.Count == 0) return;

			foreach (var pair in _bones)
			{
				if (!pair.Key) continue;

				pair.Key.localScale = Resolve(pair.Value);
			}
		}
	}
}
