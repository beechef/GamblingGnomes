// Blends an overlay onto the image it is drawn on, by sampling both — the answer to everything the
// blend state cannot express. Because the backdrop is a texture the fragment holds rather than a
// framebuffer it is handed, this gets the whole Photoshop set including the modes that read the
// destination (Overlay, Soft Light, Colour Dodge/Burn), the maths can be done in the space Photoshop
// works in, and the effect is confined to this image's own pixels — a clipping mask, not a stain that
// spreads to whatever happens to lie under the rect.
//
// It goes on the BASE image, not on a separate overlay object: uGUI binds the Image's own sprite to
// _MainTex, so a button that swaps its art per state keeps working, where a material holding the button
// as a texture would go stale the moment the sprite changed. The overlay is placed through the standard
// tiling/offset on _OverlayTex.
//
// The overlay UV is the base UV, which is 0..1 across the rect only for a Simple, un-atlased sprite —
// both true here. Sliced art would carry its nine sub-quads into the overlay and tear it.
//
// Use UI/Blend instead when the thing behind is not one image: a full screen tint over the HUD and the
// table has no single backdrop texture to sample, and the blend state is the right tool for it.
Shader "UI/Blend Overlay"
{
	Properties
	{
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		_Color ("Tint", Color) = (1,1,1,1)

		[Header(Overlay)]
		_OverlayTex ("Overlay", 2D) = "white" {}
		_OverlayColor ("Overlay Tint", Color) = (1,1,1,1)
		_OverlayOpacity ("Overlay Opacity", Range(0,1)) = 1

		// Drawn by Game.Editor.UI.UIBlendModeDrawer; its enum order is the branch order below.
		[UIBlendMode] _BlendMode ("Blend Mode", Float) = 1

		[Toggle] _BlendInGamma ("Match Photoshop (blend in gamma)", Float) = 1

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
		Blend One OneMinusSrcAlpha

		Pass
		{
			Name "Default"

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0

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
				float2 overlayTexcoord : TEXCOORD1;
				float4 worldPosition : TEXCOORD2;
				UNITY_VERTEX_OUTPUT_STEREO
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			sampler2D _OverlayTex;
			float4 _OverlayTex_ST;
			fixed4 _Color;
			fixed4 _OverlayColor;
			half _OverlayOpacity;
			half _BlendMode;
			half _BlendInGamma;
			fixed4 _TextureSampleAdd;
			float4 _ClipRect;

			static const half Epsilon = 0.0001;

			half3 ColorBurn(half3 b, half3 s) { return 1.0 - min(1.0, (1.0 - b) / max(s, Epsilon)); }
			half3 ColorDodge(half3 b, half3 s) { return min(1.0, b / max(1.0 - s, Epsilon)); }

			half3 SoftLight(half3 b, half3 s)
			{
				half3 d = lerp(sqrt(b), ((16.0 * b - 12.0) * b + 4.0) * b, step(b, 0.25));
				half3 dark = b - (1.0 - 2.0 * s) * b * (1.0 - b);
				half3 light = b + (2.0 * s - 1.0) * (d - b);
				return lerp(dark, light, step(0.5, s));
			}

			half3 Blend(half3 b, half3 s)
			{
				if (_BlendMode < 0.5) return s;
				if (_BlendMode < 1.5) return min(b, s);
				if (_BlendMode < 2.5) return b * s;
				if (_BlendMode < 3.5) return ColorBurn(b, s);
				if (_BlendMode < 4.5) return b + s - 1.0;
				if (_BlendMode < 5.5) return max(b, s);
				if (_BlendMode < 6.5) return b + s - b * s;
				if (_BlendMode < 7.5) return ColorDodge(b, s);
				if (_BlendMode < 8.5) return b + s;
				if (_BlendMode < 9.5) return lerp(2.0 * b * s, 1.0 - 2.0 * (1.0 - b) * (1.0 - s), step(0.5, b));
				if (_BlendMode < 10.5) return SoftLight(b, s);
				if (_BlendMode < 11.5) return lerp(2.0 * s * b, 1.0 - 2.0 * (1.0 - s) * (1.0 - b), step(0.5, s));
				if (_BlendMode < 12.5) return lerp(ColorBurn(b, 2.0 * s), ColorDodge(b, 2.0 * s - 1.0), step(0.5, s));
				if (_BlendMode < 13.5) return b + 2.0 * s - 1.0;
				if (_BlendMode < 14.5) return lerp(min(b, 2.0 * s), max(b, 2.0 * s - 1.0), step(0.5, s));
				if (_BlendMode < 15.5) return abs(b - s);
				if (_BlendMode < 16.5) return b + s - 2.0 * b * s;
				if (_BlendMode < 17.5) return b - s;
				return min(1.0, b / max(s, Epsilon));
			}

			v2f vert(appdata_t v)
			{
				v2f OUT;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

				OUT.worldPosition = v.vertex;
				OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
				OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
				OUT.overlayTexcoord = TRANSFORM_TEX(v.texcoord, _OverlayTex);
				OUT.color = v.color * _Color;

				return OUT;
			}

			fixed4 frag(v2f IN) : SV_Target
			{
				half4 baseColor = tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd;
				half4 overlay = tex2D(_OverlayTex, IN.overlayTexcoord) * _OverlayColor;

				// The tint belongs to the layer being blended onto, so it lands before the blend rather
				// than being smeared over the result.
				half3 b = baseColor.rgb * IN.color.rgb;
				half3 s = overlay.rgb;

				// Photoshop blends the 8 bit sRGB values it stores. The project renders Linear, so matching
				// it means stepping back into gamma for the blend and returning afterwards — measured to
				// land on Photoshop's number for Multiply, which linear blending misses by ~0.013.
				if (_BlendInGamma > 0.5)
				{
					b = LinearToGammaSpace(b);
					s = LinearToGammaSpace(s);
				}

				half3 blended = saturate(Blend(b, s));
				half3 rgb = lerp(b, blended, overlay.a * _OverlayOpacity);

				if (_BlendInGamma > 0.5) rgb = GammaToLinearSpace(rgb);

				// The base's own alpha is the mask: the overlay can darken this image but never extend it.
				half4 color = half4(rgb, baseColor.a * IN.color.a);

				#ifdef UNITY_UI_CLIP_RECT
				color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
				#endif

				#ifdef UNITY_UI_ALPHACLIP
				clip(color.a - 0.001);
				#endif

				color.rgb *= color.a;

				return color;
			}
			ENDCG
		}
	}
}
