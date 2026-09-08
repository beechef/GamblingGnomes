using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Game.Runtime.GameMode.Poker.Hallucination
{
	// A hallucination drawn by a full-screen pass. The look lives in a Shader Graph and the strength is one
	// float on it, so retuning the wave is a graph edit and a number rather than any code at all.
	//
	// The pass is a renderer feature on the URP renderer, which is a project-wide thing switched off until
	// somebody is seeing this — a feature left on costs a blit every frame for a room nobody is hallucinating
	// in. What a viewer sees is still their own business: this only ever runs on their client.
	[CreateAssetMenu(fileName = "Hallucination_Fullscreen", menuName = "Game/Poker/Hallucination/Fullscreen")]
	public class PokerHallucinationFullscreenEffect : PokerHallucinationEffect
	{
		[Tooltip("The Full Screen Pass Renderer Feature that draws this. It lives on the renderer asset, so it is picked here rather than looked up.")]
		[Required]
		[SerializeField] private ScriptableRendererFeature _feature;

		[Tooltip("Material over the graph. It is cloned before anything is written, so the asset on disk is never touched.")]
		[Required]
		[SerializeField] private Material _material;

		[Tooltip("The float on the material that says how strong this is. Zero of it must look like nothing at all, which is what lets the effect fade in and out.")]
		[SerializeField] private string _strengthProperty = "_Amplitude";

		[SerializeField] private float _strength = 0.015f;

		[Tooltip("Seconds it takes to arrive. A hallucination that snaps on reads as a bug rather than as a symptom.")]
		[SerializeField] private float _fadeDuration = 1.5f;

		public ScriptableRendererFeature Feature => _feature;
		public Material Material => _material;
		public string StrengthProperty => _strengthProperty;
		public float Strength => _strength;
		public float FadeDuration => Mathf.Max(0f, _fadeDuration);

		protected override PokerHallucinationEffectBehaviour Attach(GameObject host) => host.AddComponent<PokerHallucinationFullscreenBehaviour>();
	}
}
