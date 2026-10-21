using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Runtime.Props
{
	// Arms and legs folded away inside a prop: held at zero scale and switched off until the prop wears the look,
	// then grown out and flailed at random until the look is dropped. How each limb may move is its own
	// (PropLimb); this only times it. Listens to PropVariantController and never asks for a variant itself.
	public class PropLimbs : MonoBehaviour
	{
		private struct Joint
		{
			public Transform Bone;
			public Quaternion Rest;
			public PropLimb Limb;
			public bool IsRoot;
		}

		[Required]
		[SerializeField] private PropVariantController _variants;

		[Tooltip("The variant that grows the limbs.")]
		[SerializeField] private PropVariant _variant = PropVariant.Limbs;

		[Tooltip("Each limb, authored at full size and placed where it grows from.")]
		[SerializeField] private List<PropLimb> _limbs = new();

		[Header("Grow")]
		[Min(0f)]
		[SerializeField] private float _growDuration = 0.5f;

		[SerializeField] private Ease _growEase = Ease.OutBack;

		[Min(0f)]
		[SerializeField] private float _shrinkDuration = 0.3f;

		[SerializeField] private Ease _shrinkEase = Ease.InBack;

		[Header("Flail")]
		[Tooltip("Seconds one swing or bend takes, picked at random between the two.")]
		[MinMaxSlider(0.05f, 3f, true)]
		[SerializeField] private Vector2 _moveDuration = new(0.25f, 0.7f);

		[SerializeField] private Ease _moveEase = Ease.InOutSine;

		private readonly List<Joint> _joints = new();

		private Vector3[] _fullScales;
		private Tween[] _scaleTweens;
		private TweenCallback[] _onShrunk;
		private Tween[] _moveTweens;
		private TweenCallback[] _onMoved;
		private bool _grown;

		private void Awake()
		{
			if (!_variants) _variants = GetComponentInParent<PropVariantController>();

			_fullScales = new Vector3[_limbs.Count];
			_scaleTweens = new Tween[_limbs.Count];
			_onShrunk = new TweenCallback[_limbs.Count];

			for (var i = 0; i < _limbs.Count; i++)
			{
				var limb = _limbs[i];
				if (!limb) continue;

				_fullScales[i] = limb.transform.localScale;
				_onShrunk[i] = () => limb.gameObject.SetActive(false);

				CollectJoints(limb);
			}

			_moveTweens = new Tween[_joints.Count];
			_onMoved = new TweenCallback[_joints.Count];
			for (var i = 0; i < _joints.Count; i++)
			{
				var index = i;
				_onMoved[i] = () => Move(index);
			}

			Fold();
		}

		private void OnEnable()
		{
			if (!_variants) return;

			_variants.OnVariantChanged += HandleVariantChanged;

			// A room switched on while the look is already worn arrives with its limbs out.
			if (_variants.Current == _variant) Grow(false);
		}

		private void OnDisable()
		{
			if (_variants) _variants.OnVariantChanged -= HandleVariantChanged;

			Fold();
		}

		// The root bone is the one whose parent is not part of the skin; leaf bones only mark where a chain ends.
		private void CollectJoints(PropLimb limb)
		{
			foreach (var skin in limb.GetComponentsInChildren<SkinnedMeshRenderer>(true))
			{
				var bones = skin.bones;
				foreach (var bone in bones)
				{
					if (!bone || bone.childCount == 0) continue;

					_joints.Add(new Joint
					{
						Bone = bone,
						Rest = bone.localRotation,
						Limb = limb,
						IsRoot = System.Array.IndexOf(bones, bone.parent) < 0
					});
				}
			}
		}

		private void HandleVariantChanged(PropVariant current)
		{
			if (current == _variant) Grow(true);
			else Shrink();
		}

		private void Grow(bool animate)
		{
			if (_grown) return;

			_grown = true;

			for (var i = 0; i < _limbs.Count; i++)
			{
				var limb = _limbs[i];
				if (!limb) continue;

				_scaleTweens[i]?.Kill();
				limb.gameObject.SetActive(true);

				if (!animate)
				{
					limb.transform.localScale = _fullScales[i];
					continue;
				}

				_scaleTweens[i] = limb.transform.DOScale(_fullScales[i], _growDuration).SetEase(_growEase);
			}

			for (var i = 0; i < _joints.Count; i++) Move(i);
		}

		private void Shrink()
		{
			if (!_grown) return;

			_grown = false;
			StopMoving();

			for (var i = 0; i < _limbs.Count; i++)
			{
				var limb = _limbs[i];
				if (!limb) continue;

				_scaleTweens[i]?.Kill();
				_scaleTweens[i] = limb.transform.DOScale(Vector3.zero, _shrinkDuration).SetEase(_shrinkEase).OnComplete(_onShrunk[i]);
			}
		}

		// Put away at once: no tween left running on a prop that is off or gone.
		private void Fold()
		{
			_grown = false;
			StopMoving();

			for (var i = 0; i < _limbs.Count; i++)
			{
				var limb = _limbs[i];
				if (!limb) continue;

				_scaleTweens[i]?.Kill();
				limb.transform.localScale = Vector3.zero;
				limb.gameObject.SetActive(false);
			}

			foreach (var joint in _joints)
			{
				if (joint.Bone) joint.Bone.localRotation = joint.Rest;
			}
		}

		private void StopMoving()
		{
			for (var i = 0; i < _moveTweens.Length; i++)
			{
				_moveTweens[i]?.Kill();
				_moveTweens[i] = null;
			}
		}

		private void Move(int index)
		{
			var joint = _joints[index];
			if (!joint.Bone) return;

			var target = joint.Rest * (joint.IsRoot ? joint.Limb.RandomSwing() : joint.Limb.RandomBend());
			var duration = Random.Range(_moveDuration.x, _moveDuration.y);

			_moveTweens[index] = joint.Bone.DOLocalRotateQuaternion(target, duration).SetEase(_moveEase).OnComplete(_onMoved[index]);
		}
	}
}
