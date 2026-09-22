using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Video;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// A full-screen pass with a looping video under it: the graph blends the clip over the room, and the clip
	// plays for as long as the effect is on. How the two are blended is the graph's own business.
	[CreateAssetMenu(fileName = "Hallucination_Video", menuName = "Game/Poker/Hallucination/Video")]
	public class PokerHallucinationVideoEffect : PokerHallucinationFullscreenEffect
	{
		[Header("Video")]
		[Required]
		[SerializeField] private VideoClip _clip;

		[Tooltip("The texture on the material the clip is played into.")]
		[SerializeField] private string _textureProperty = "_VideoTex";

		[MinValue(0.01f)]
		[SerializeField] private float _playbackSpeed = 1f;

		public VideoClip Clip => _clip;
		public string TextureProperty => _textureProperty;
		public float PlaybackSpeed => _playbackSpeed;

		protected override PokerHallucinationEffectBehaviour Attach(GameObject host) => host.AddComponent<PokerHallucinationVideoBehaviour>();
	}
}
