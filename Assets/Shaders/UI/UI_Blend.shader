// A uGUI image that blends with whatever was drawn under it, through the hardware blend state rather
// than by reading the backdrop — which is the only thing that works on a Screen Space Overlay canvas,
// since URP has no grab pass and the UI is drawn straight to the back buffer after the pipeline is done.
// "Under it" therefore means earlier in the canvas' own draw order: an overlay parented to a button
// blends with that button, a sibling drawn after the background blends with the background.
//
// The blend state is driven by material properties so one shader covers every mode; UIBlendShaderGUI
// owns the friendly dropdown and writes them. Never set _SrcBlend/_DstBlend/_BlendOp/_BlendNeutral by
// hand — a mode is picked, not typed.
//
// Alpha fades every mode uniformly through one line in the fragment: the source is lerped toward the
// value that leaves the backdrop untouched. That is black for the modes whose source is scaled by One
// (a zero contribution), and white for Darken, whose Min leaves the destination alone only against the
// brightest possible source. Without it a Multiply or Darken image would ignore its own alpha and its
// CanvasGroup entirely, which reads as a fade that does nothing.
Shader "UI/Blend"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		_Color ("Tint", Color) = (1,1,1,1)

		[HideInInspector] _BlendMode ("Blend Mode", Float) = 0
		[HideInInspector] _BlendNeutral ("Blend Neutral", Color) = (0,0,0,0)
		[HideInInspector] _SrcBlend ("Src Blend", Float) = 1
		[HideInInspector] _DstBlend ("Dst Blend", Float) = 10
		[HideInInspector] _BlendOp ("Blend Op", Float) = 0

		_StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		_StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255

		_ColorMask ("Color Mask", Float) = 15

		[Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
	}

	SubShader
	{
		Tags
		{
			"Queue" = "Transparent"
			"IgnoreProjector" = "True"
			"RenderType" = "Transparent"
			"PreviewType" = "Plane"
			"CanUseSpriteAtlas" = "True"
		}

		// Kept so Mask still clips this image; UNITY_UI_CLIP_RECT below is the RectMask2D half.
		Stencil
		{
			Ref [_Stencil]
			Comp [_StencilComp]
			Pass [_StencilOp]
			ReadMask [_StencilReadMask]
			WriteMask [_StencilWriteMask]
		}

		Cull Off
		Lighting Off
		ZWrite Off
		ZTest [unity_GUIZTestMode]
		ColorMask [_ColorMask]

		// Only the colour side is a mode. Alpha is always straight premultiplied coverage, because Min and
		// Max apply to every channel they are given — Darken would otherwise take min(src.a, dst.a) and
		// punch the backdrop's alpha out wherever the overlay is transparent, which is invisible on the
		// back buffer and destroys the image the moment the canvas renders into a RenderTexture.
		BlendOp [_BlendOp], Add
		Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha

		Pass
		{
			Name "Default"

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 2.0

			#include "UnityCG.cginc"
			#include "UnityUI.cginc"

			#pragma multi_compile_local _ UNITY_UI_CLIP_RECT
			#pragma multi_compile_local _ UNITY_UI_ALPHACLIP

			struct appdata_t
			{
				float4 vertex : POSITION;
				float4 color : COLOR;
				float2 texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				fixed4 color : COLOR;
				float2 texcoord : TEXCOORD0;
				float4 worldPosition : TEXCOORD1;
				UNITY_VERTEX_OUTPUT_STEREO
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			fixed4 _Color;
			fixed4 _TextureSampleAdd;
			float4 _ClipRect;
			fixed4 _BlendNeutral;

			v2f vert(appdata_t v)
			{
				v2f OUT;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

				OUT.worldPosition = v.vertex;
				OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
				OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
				OUT.color = v.color * _Color;

				return OUT;
			}

			fixed4 frag(v2f IN) : SV_Target
			{
				half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

				#ifdef UNITY_UI_CLIP_RECT
				color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
				#endif

				#ifdef UNITY_UI_ALPHACLIP
				clip(color.a - 0.001);
				#endif

				color.rgb = lerp(_BlendNeutral.rgb, color.rgb, color.a);

				return color;
			}
			ENDCG
		}
	}

	CustomEditor "Game.Editor.UI.UIBlendShaderGUI"
}
